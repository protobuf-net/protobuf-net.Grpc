using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IStreamOwner
{
    void Remove(ushort id);
}
interface IStream
{
    ushort Id { get; }
    ushort NextSequenceId();
    MethodType MethodType { get; }
    string Method { get; }
    bool TryAcceptFrame(in Frame frame);
}

internal enum StreamState
{
    Uninitialized = 0,
    ExpectHeaders = 1 | AcceptHeader | AcceptPayload | AcceptTrailer | AcceptStatus,
    ExpectBody = 2 | AcceptPayload | AcceptTrailer | AcceptStatus,
    ExpectTrailers = 3 | AcceptTrailer | AcceptStatus,
    ExpectStatus = 4 | AcceptStatus,
    Completed = 5,
    Cancelled = 6,
    Faulted = 7, // bad things, Mikey

    // high bits are flags
    AcceptHeader = 1 << 27,
    AcceptPayload = 1 << 28,
    AcceptTrailer = 1 << 29,
    AcceptStatus = 1 << 30,
}

internal enum WorkerState
{
    NotStarted,
    ActivatingFirstTime,
    Active,
    Suspended,
    RanToCompletion,
    Faulted,
}
internal abstract class LiteStream<TSend, TReceive> : IStream, IWorker, IAsyncStreamReader<TReceive>, IValueTaskSource where TSend : class where TReceive : class
{

    private Maybe<TReceive> _nextItemNeedsSync;

    private Maybe<TReceive> GetNextItem()
    {
        lock (SyncLock)
        {
            return _nextItemNeedsSync;
        }
    }
    TReceive IAsyncStreamReader<TReceive>.Current => GetNextItem().Value!;
    Task<bool> IAsyncStreamReader<TReceive>.MoveNext(CancellationToken cancellationToken)
    {
        var pending = MoveNextPayloadAsync(cancellationToken);
        if (pending.IsCompletedSuccessfully)
        {
            return GetNextItem().HasValue ? Utilities.AsyncTrue : Utilities.AsyncFalse;
        }
        return Awaited(this, pending);
        async Task<bool> Awaited(LiteStream<TSend, TReceive> stream, ValueTask pending)
        {
            await pending;
            return GetNextItem().HasValue;
        }
    }

    protected LiteStream(IMethod method, IFrameConnection output, IStreamOwner? owner)
    {
        Method = method;
        _output = output;
        _streamId = ushort.MaxValue; // will be updated after construction
        _sequenceId = ushort.MaxValue; // so first is zero
        StreamState = StreamState.ExpectHeaders;
        _owner = owner;
    }
    IStreamOwner? _owner;
    protected void SetOwner(IStreamOwner? owner) => _owner = owner;

    string IStream.Method => Method!.FullName;
    protected FrameBufferManager BufferManager => FrameBufferManager.Shared;

    private ushort _streamId;
    public ILogger? Logger { get; set; }
    protected void SetOutput(IFrameConnection output)
        => _output = output;
    IFrameConnection _output;

    // IMPORTANT: state and backlog changes should be synchronized
    private Queue<Frame>? _backlogFrames;
    Frame _backlogFrame;

    // don't need volatile read on the StreamState; if we care about correctness, we'll be reading inside a lock; if
    // we don't care about correctness, then *we don't care about correctness*
    public StreamState StreamState { get; private set; }

    private WorkerState _workerStateNeedsSync;

    protected CancellationToken StreamCancellation => default;

    private object SyncLock => this;

    bool IStream.TryAcceptFrame(in Frame frame)
    {
        var header = frame.GetHeader();
        lock (SyncLock)
        {
            var need = header.Kind switch
            {
                FrameKind.Header => StreamState.AcceptHeader,
                FrameKind.Payload => StreamState.AcceptPayload,
                FrameKind.Trailer => StreamState.AcceptTrailer,
                FrameKind.Status => StreamState.AcceptStatus,
                _ => default,
            };
            if ((StreamState & need) == 0) return false; // not expecting that

            if (_backlogFrames is not null)
            {
                _backlogFrames.Enqueue(frame);
            }
            else if (_backlogFrame.HasValue)
            {
                // we now have two frames; start a queue
                _backlogFrames = new Queue<Frame>();
                _backlogFrames.Enqueue(_backlogFrame);
                _backlogFrames.Enqueue(frame);
                _backlogFrame = default;
            }
            else
            {
                _backlogFrame = frame;
            }
            // we only accept header/payload/trailer/status, and all of those use PayloadFlags,
            // so we can check if we're done
            if ((header.KindFlags & (byte)PayloadFlags.EndItem) != 0)
            {
                StartWorkerAsNeeded();
            }
            return true;
        }
    }

    internal async ValueTask<TReceive> AssertSingleAsync(CancellationToken cancellationToken)
    {
        await MoveNextPayloadAsync(cancellationToken);
        if (!GetNextItem().TryGetValue(out var request))
            throw new InvalidOperationException("No payload received");
        if (GetNextItem().HasValue)
            throw new InvalidOperationException("Unexpected payload received");
        return request;
    }
    internal async ValueTask<TReceive> AssertNextAsync(CancellationToken cancellationToken)
    {
        await MoveNextPayloadAsync(cancellationToken);
        if (!GetNextItem().TryGetValue(out var request))
            throw new InvalidOperationException("No payload received");
        return request;
    }
    internal async ValueTask AssertNoMoreAsync(CancellationToken cancellationToken)
    {
        await MoveNextPayloadAsync(cancellationToken);
        if (GetNextItem().HasValue)
            throw new InvalidOperationException("Unexpected payload received");
    }

    protected async ValueTask MoveNextPayloadAsync(CancellationToken cancellationToken)
    {
        lock (SyncLock)
        {
            _nextItemNeedsSync = default!;
        }
        while (true)
        {
            ReadOnlyMemory<Frame> nextGroup = default;
            FrameKind kind;
            try
            {
                StreamState streamState;
                Logger.Debug("reading next frame group...");
                lock (SyncLock)
                {   // need to syncrhonize access to backlog
                    nextGroup = ReadNextFrameGroupLocked(out kind);
                    streamState = StreamState;
                }
                Logger.Debug((nextGroup, kind, streamState), static (state, ex) => $"got {state.nextGroup.Length} bytes, {state.kind}, {state.streamState}");
                switch (kind)
                {
                    case FrameKind.None:

                        switch (streamState)
                        {
                            case StreamState.ExpectHeaders:
                            case StreamState.ExpectBody:
                                ValueTask pending;
                                Logger.Debug("suspending worker, waiting for payload...");
                                lock (SyncLock)
                                {
                                    ChangeState(WorkerState.Active, WorkerState.Suspended);
                                    pending = new ValueTask(this, _suspendedContinuationPoint.Version);
                                }
                                await pending; // IMPORTANT; this is how the stream goes to sleep and waits to be activated by a new invocation
                                Logger.Debug("worker resumed");
                                continue; // and try again when we resume
                            default:
                                return; // there will never be anything else
                        }
                    case FrameKind.Header:
                    case FrameKind.Trailer:
                        // drop them on the floor for now
                        break;
                    case FrameKind.Payload:
                        var maybe = TryDeserializePayload(ref nextGroup);
                        if (maybe.HasValue)
                        {
                            lock (SyncLock)
                            {
                                _nextItemNeedsSync = maybe;
                            }
                            return; // a value has been provided; yay for us!
                        }
                        break; // try again
                    default:
                        Logger.Information(kind, (state, _) => $"unexpected {state} frame-group received");
                        break;
                }
            }
            finally
            {
                Release(ref nextGroup);
            }
        }
    }

    private Maybe<TReceive> TryDeserializePayload(ref ReadOnlyMemory<Frame> frameGroup)
    {
        Debug.Assert(!frameGroup.IsEmpty, "frame group should not be empty");
        var span = frameGroup.Span;
        var lastHeader = span[span.Length - 1].GetHeader();
        Debug.Assert(lastHeader.Kind == FrameKind.Payload, $"expected payload, got {lastHeader.Kind}");

        // check the state flags
        var flags = (PayloadFlags)lastHeader.KindFlags;
        var isFinal = (flags & PayloadFlags.FinalItem) != 0;
        if (isFinal)
        {
            lock (SyncLock) // record that we're not expecting anything more
            {
                var state = StreamState;
                switch (state)
                {
                    case StreamState.ExpectHeaders:
                    case StreamState.ExpectBody:
                        StreamState = StreamState.ExpectTrailers;
                        break;
                }
            }
        }
        if ((flags & PayloadFlags.NoItem) != 0) return default; // don't run the deserializer if they're telling is "no item" (caller will release the buffers)

        // run the deserializer
        var payload = FrameSequenceSegment.Create(span);
        Logger.Debug(payload, static (state, _) => $"deserializing payload, {state.Length} bytes, {(state.IsSingleSegment ? "single segment" : "multiple segments")}...");
        var ctx = Pool<SingleBufferDeserializationContext>.Get();
        ctx.Initialize(payload);
        var value = Deserializer(ctx);
        Logger.Debug(value, static (state, _) => $"deserialized: {state}");
        ctx.Recycle();

        // we can release the original buffers now, before we push the value onwards
        Release(ref frameGroup);
        return new Maybe<TReceive>(value);
    }


    private void StartWorkerAsNeeded()
    {
        lock (SyncLock)
        {
            var state = _workerStateNeedsSync;
            switch (state)
            {
                case WorkerState.NotStarted:
                    _workerStateNeedsSync = WorkerState.ActivatingFirstTime;
                    this.StartWorker();
                    break;
                case WorkerState.Suspended:
                    this.StartWorker();
                    break;
            }
        }
    }

    private void ReleaseBacklogLocked()
    {
        _backlogFrame.Release();
        if (_backlogFrames is not null)
        {
            while (_backlogFrames.TryDequeue(out var frame))
            {
                frame.Release();
            }
        }
    }

    private void ReportFault(Exception ex)
    {
        try
        {
            Logger.Critical(ex);
            lock (SyncLock)
            {
                // normally when setting state we'd need to preserve the active flag,
                // but we're exiting, so...
                StreamState = StreamState.Faulted;
                ReleaseBacklogLocked();
            }
        }
        catch (Exception innerEx)
        {   // ok, even our fault handler is faulting; try one last log
            Logger.Critical(innerEx);
        }

        try
        {
            _suspendedContinuationPoint.SetException(ex);
        }
        catch (Exception innerEx)
        {
            Logger.Critical(innerEx);
        }
    }
    public void Execute()
    {
        Logging.SetSource((IsClient ? Logging.ClientPrefix : Logging.ServerPrefix) + "executor");
        try
        {
            WorkerState state;
            lock (SyncLock)
            {
                state = _workerStateNeedsSync;
                switch (state)
                {
                    case WorkerState.ActivatingFirstTime:
                    case WorkerState.Suspended:
                        _workerStateNeedsSync = WorkerState.Active;
                        break;
                }
            }
            // now do the real code outside of the lock
            switch (state)
            {
                case WorkerState.ActivatingFirstTime:
                    _ = ExecuteCoreAsync();
                    break;
                case WorkerState.Suspended:
                    _suspendedContinuationPoint.SetResult(false); // the value is irrelevant
                    break;
                default:
                    throw new InvalidOperationException($"unexpected worker state '{state}' when attempting to activate worker");
            }
        }
        catch (Exception ex)
        {
            ReportFault(ex); // this never throws
        }
    }

    private bool ChangeState(WorkerState expectedOldState, WorkerState newState)
    {
        Logger.Debug((expectedOldState, newState), (state, _) => $"changing worker state from {expectedOldState} to {newState}");
        WorkerState actualOld;
        lock (SyncLock)
        {   // this *might* be fine with interlocked, but frankly; I don't want to have to think about the subtle behaviours
            actualOld = _workerStateNeedsSync;
            if (actualOld == expectedOldState)
            {
                _workerStateNeedsSync = newState;
            }
        }
        if (actualOld != expectedOldState) // log outside the lock
        {
            Logger.Critical((expectedOldState, newState, actualOld), (state, _) => $"unexpected worker state '{state.actualOld}' when attempting to move from '{state.expectedOldState}' to '{state.newState}'");
            return false;
        }
        return true;
    }

    protected virtual ValueTask ExecuteAsync() => default;
    private async Task ExecuteCoreAsync()
    {
        try
        {
            await ExecuteAsync();
            ChangeState(WorkerState.Active, WorkerState.RanToCompletion);
        }
        catch (Exception ex)
        {
            ReportFault(ex);
        }
        finally
        {
            Logger.Debug(Id, static (state, _) => $"removing stream {state}");
            _owner?.Remove(Id);
        }
    }

    private void Release(ref ReadOnlyMemory<Frame> frames)
    {
        if (frames.IsEmpty) return;
        try
        {
            // release the individual frames
            foreach (var frame in frames.Span)
            {
                frame.Release();
            }
            // and return the array we used for the bundle
            if (MemoryMarshal.TryGetArray(frames, out var segment))
            {
                ArrayPool<Frame>.Shared.Return(segment.Array!);
            }
        }
        catch (Exception ex)
        {
            Logger.Critical(ex);
        }
        finally
        {
            frames = default; // so we don't release anything twice
        }
    }

    private ReadOnlyMemory<Frame> ReadNextFrameGroupLocked(out FrameKind kind)
    {
        int count;
        kind = FrameKind.None;
        static bool IsTerminated(in Frame frame, out FrameKind kind)
        {
            var header = frame.GetHeader();
            kind = header.Kind;
            switch (kind)
            {
                case FrameKind.Status:
                case FrameKind.Header:
                case FrameKind.Payload:
                case FrameKind.Trailer:
                    return (header.KindFlags & (byte)(PayloadFlags.EndItem | PayloadFlags.NoItem)) != 0;
                default:
                    return ThrowInvalidKind(kind);
            }

        }
        static bool ThrowInvalidKind(FrameKind kind) => throw new InvalidOperationException($"An unexpected {kind} frame was encountered in the backlog");
        static void ThrowMismatchedGroup(FrameKind group, FrameKind item) => throw new InvalidOperationException($"An unexpected {item} frame was encountered in the backlog while reading a {group} group");

        if (_backlogFrames is not null)
        {
            if (_backlogFrames.TryPeek(out var frame) && IsTerminated(frame, out kind))
            {   // optimize for single-frame scenarions
                count = 1;
            }
            else if (_backlogFrames.Count == 0)
            {
                count = 0;
            }
            else // multiple items, presumably; we need to find how many are in this group, and validate that they make a consistent group
            {
                var iter = _backlogFrames.GetEnumerator(); // doesn't dequeue - this is a peek cursor, effectively
                if (iter.MoveNext())
                {
                    if (IsTerminated(frame, out kind))
                    {
                        count = 1;
                    }
                    else
                    {
                        count = -1; // use sign to track EOF; if we end up with a -ve number, we didn't find an end
                        while (iter.MoveNext())
                        {
                            bool terminated = IsTerminated(iter.Current, out var nextKind);
                            if (nextKind != kind) ThrowMismatchedGroup(kind, nextKind); // make sure all items in the group share a kind
                            if (terminated)
                            {
                                count = -count;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    count = 0;
                }
                // note queue iterator doesn't need Dispose() called
            }
        }
        else
        {
            count = _backlogFrame.HasValue && IsTerminated(_backlogFrame, out kind) ? 1 : 0;
        }
        if (count <= 0) return default;

        Frame[] oversized = ArrayPool<Frame>.Shared.Rent(count);
        if (_backlogFrames is not null)
        {
            for (int i = 0; i < count; i++)
            {
                oversized[i] = _backlogFrames.Dequeue();
            }
        }
        else
        {
            oversized[0] = _backlogFrame;
            _backlogFrame = default;
        }
        return new ReadOnlyMemory<Frame>(oversized, 0, count);
    }

    public ushort Id
    {
        get => _streamId;
        set => _streamId = value;
    }
    private int _sequenceId;

    protected abstract bool IsClient { get; }
    public ushort NextSequenceId()
    {
        // MSB bit is always off for clients, and always on for servers
        var next = Utilities.IncrementToUInt32(ref _sequenceId) & 0x7FFF;
        return (ushort)(IsClient ? next : next | 0x8000);
    }
    protected IMethod Method { get; }

    protected IFrameConnection Output => _output;
    protected abstract Action<TSend, SerializationContext> Serializer { get; }
    protected abstract Func<DeserializationContext, TReceive> Deserializer { get; }

    public ValueTask SendInitializeAsync(string? host, CallOptions options)
        => Output.WriteAsync(GetInitializeFrame(host), options.CancellationToken);

    internal Frame GetInitializeFrame(string? host)
    {
        var fullName = Method.FullName;
        if (string.IsNullOrEmpty(fullName)) ThrowMissingMethod();
        if (!string.IsNullOrEmpty(host)) ThrowNotSupported(); // in future: delimit?
        var length = Encoding.UTF8.GetByteCount(fullName);
        if (length > FrameHeader.MaxPayloadSize) ThrowMethodTooLarge(length);

        var slab = BufferManager.Rent(length);
        try
        {
            var header = new FrameHeader(FrameKind.NewStream, 0, Id, NextSequenceId(), (ushort)length);
            slab.Advance(Encoding.UTF8.GetBytes(fullName, slab.ActiveBuffer.Span));
            return slab.CreateFrameAndInvalidate(header, updateHeaderLength: false); // this will validate etc
        }
        finally
        {
            slab?.Return();
        }

        static void ThrowMissingMethod() => throw new ArgumentOutOfRangeException(nameof(fullName), "No method name was specified");
        static void ThrowNotSupported() => throw new ArgumentOutOfRangeException(nameof(host), "Non-empty hosts are not currently supported");
        static void ThrowMethodTooLarge(int length) => throw new InvalidOperationException($"The method name is too large at {length} bytes");
    }

    internal async Task SendSingleAsync(string? host, CallOptions options, TSend request)
    {
        try
        {
            await SendInitializeAsync(host, options);
            await SendAsync(request, PayloadFlags.FinalItem, options.CancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
    }

    public async ValueTask SendAsync(TSend value, PayloadFlags flags, CancellationToken cancellationToken)
    {
        PayloadFrameSerializationContext? serializationContext = null;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Logger.Debug(value, (state, ex) => $"serializing {state}...");
            serializationContext = PayloadFrameSerializationContext.Get(this, BufferManager, Id, flags);
            Serializer(value, serializationContext);
            Logger.Debug(serializationContext, (state, ex) => $"serialized; {serializationContext}");
            await serializationContext.WritePayloadAsync(Output, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
            throw;
        }
        finally
        {
            serializationContext?.Recycle();
        }
    }

    ManualResetValueTaskSourceCore<bool> _suspendedContinuationPoint;
    void IValueTaskSource.GetResult(short token)
    {
        lock (SyncLock)
        {
            try
            {
                _suspendedContinuationPoint.GetResult(token);
            }
            finally
            {
                if (token == _suspendedContinuationPoint.Version) _suspendedContinuationPoint.Reset();
            }
        }
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
    {
        lock (SyncLock)
        {
            return _suspendedContinuationPoint.GetStatus(token);
        }
    }

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _suspendedContinuationPoint.OnCompleted(continuation, state, token, flags);

    MethodType IStream.MethodType => Method!.Type;
}