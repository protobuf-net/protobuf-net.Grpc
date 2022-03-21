using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IStreamOwner
{
    void Remove(ushort id);
    CancellationToken Shutdown { get; }
}
interface IStream
{
    ushort Id { get; }
    ushort NextSequenceId();
    MethodType MethodType { get; }
    string Method { get; }
    bool TryAcceptFrame(in Frame frame);
    CancellationToken CancellationToken { get; }
    void Cancel();
}

internal enum StreamState
{
    Uninitialized = 0,
    ExpectHeaders = 1 | AcceptHeader | AcceptPayload | AcceptTrailer,
    ExpectBody = 2 | AcceptPayload | AcceptTrailer,
    ExpectTrailers = 3 | AcceptTrailer,
    Completed = 5,
    Cancelled = 6,
    Faulted = 7, // bad things, Mikey

    // high bits are flags
    AcceptHeader = 1 << 28,
    AcceptPayload = 1 << 29,
    AcceptTrailer = 1 << 30,
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
internal abstract class LiteStream<TSend, TReceive> : IStream, IWorker, IAsyncStreamReader<TReceive>, IValueTaskSource, IDisposable where TSend : class where TReceive : class
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
        CancellationTokenRegistration ctr = this.RegisterCancellation(cancellationToken);
        var pending = MoveNextPayloadAsync();
        if (pending.IsCompletedSuccessfully)
        {
            ctr.Dispose();
            return GetNextItem().HasValue ? Utilities.AsyncTrue : Utilities.AsyncFalse;
        }
        return Awaited(this, pending, ctr);
        async Task<bool> Awaited(LiteStream<TSend, TReceive> stream, ValueTask pending, CancellationTokenRegistration ctr)
        {
            using (ctr)
            {
                await pending;
                return GetNextItem().HasValue;
            }
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
        _workerStateNeedsSync = IsClient ? WorkerState.Active : WorkerState.NotStarted; // for clients, the caller is the initial executor
    }
    IStreamOwner? _owner;
    protected void SetOwner(IStreamOwner? owner) => _owner = owner;

    string IStream.Method => Method!.FullName;
    protected RefCountedMemoryPool<byte> MemoryPool => RefCountedMemoryPool<byte>.Shared;

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

    private CancellationTokenSource? _cancellation;
    public CancellationToken CancellationToken { get; private set; }

    private CancellationToken _streamSpecificCancellation;

    protected CancellationTokenRegistration _onCancelRegistration;

    public virtual void Dispose() => _onCancelRegistration.Dispose();

    [Flags]
    private enum CancellationScenerio : byte
    {
        None = 0,
        OwnerShutdown = 1 << 0,
        StreamSpecific = 1 << 1,
        Deadline = 1 << 2,
    }
    internal void RegisterForCancellation(CancellationToken streamSpecificCancellation, DateTime? deadline)
    {
        CancellationScenerio scenario = CancellationScenerio.None;
        var ownerShutdown = _owner?.Shutdown ?? CancellationToken.None;
        if (ownerShutdown.CanBeCanceled)
        {
            ownerShutdown.ThrowIfCancellationRequested();
            scenario |= CancellationScenerio.OwnerShutdown;
        }
        if (streamSpecificCancellation.CanBeCanceled)
        {
            _streamSpecificCancellation = streamSpecificCancellation;
            streamSpecificCancellation.ThrowIfCancellationRequested();
            scenario |= CancellationScenerio.StreamSpecific;
        }
        TimeSpan timeout = default;
        if (deadline.HasValue)
        {
            var val = deadline.GetValueOrDefault();
            if (val != DateTime.MaxValue)
            {
                timeout = val - DateTime.UtcNow;
                if (timeout <= TimeSpan.Zero) throw new TimeoutException("Deadline exceeded before call");
                scenario |= CancellationScenerio.Deadline;
            }
        }
        Logger.Debug(scenario, static (state, _) => $"Cancellation mode: {state}");
        // 8 possibilities
        switch (scenario)
        {
            case CancellationScenerio.None:
                break; // nothing to do
            case CancellationScenerio.OwnerShutdown:
                CancellationToken = ownerShutdown;
                break;
            case CancellationScenerio.StreamSpecific:
                CancellationToken = streamSpecificCancellation;
                break;
            case CancellationScenerio.Deadline:
                _cancellation = new CancellationTokenSource();
                break; // deadline and StreamCancellation dealt with below
            case CancellationScenerio.OwnerShutdown | CancellationScenerio.StreamSpecific:
            case CancellationScenerio.OwnerShutdown | CancellationScenerio.StreamSpecific | CancellationScenerio.Deadline:
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(ownerShutdown, streamSpecificCancellation);
                break; // deadline and StreamCancellation dealt with below
            case CancellationScenerio.OwnerShutdown | CancellationScenerio.Deadline:
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(ownerShutdown);
                break; // deadline and StreamCancellation dealt with below
            case CancellationScenerio.StreamSpecific | CancellationScenerio.Deadline:
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(streamSpecificCancellation);
                break; // deadline and StreamCancellation dealt with below

            default:
                throw new InvalidOperationException($"Unexpected cancellation scenario: {scenario}");
        }
        if (_cancellation is not null)
        {
            CancellationToken = _cancellation.Token;
            if ((scenario & CancellationScenerio.Deadline) != 0)
            {
                try
                {
                    _cancellation.CancelAfter(timeout);
                }
                catch (Exception ex)
                {
                    ReportFault(ex);
                    throw;
                }
            }
        }
        _onCancelRegistration = this.RegisterCancellation(CancellationToken);
    }

    void IStream.Cancel()
    {
        try
        {
            try
            {
                // try to give the most meaningful cancellation
                _streamSpecificCancellation.ThrowIfCancellationRequested();
                _owner?.Shutdown.ThrowIfCancellationRequested();
                throw new TimeoutException(); // that leaves a deadline, then
            }
            catch (Exception ex)
            {
                ReportFault(ex);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
    }

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
            Logger.Debug((frame, frames: _backlogFrames), static (state, _) => $"added backlog frame: {state.frame}; total frames: {state.frames?.Count ?? 1}");
            if (header.IsFinal)
            {
                StartWorkerAsNeeded();
            }
            return true;
        }
    }

    internal async ValueTask<TReceive> AssertSingleAsync()
    {
        await MoveNextPayloadAsync();
        if (!GetNextItem().TryGetValue(out var request))
            throw new InvalidOperationException("No payload received by " + ClientServerLabel);
        await MoveNextPayloadAsync();
        if (GetNextItem().HasValue)
            throw new InvalidOperationException("Unexpected payload received by " + ClientServerLabel);
        return request;
    }
    internal async ValueTask<TReceive> AssertNextAsync()
    {
        await MoveNextPayloadAsync();
        if (!GetNextItem().TryGetValue(out var request))
            throw new InvalidOperationException("No payload received by " + ClientServerLabel);
        return request;
    }
    internal async ValueTask AssertNoMoreAsync()
    {
        await MoveNextPayloadAsync();
        if (GetNextItem().HasValue)
            throw new InvalidOperationException("Unexpected payload received by " + ClientServerLabel);
    }

    private string ClientServerLabel => IsClient ? "client" : "server";

    protected async ValueTask MoveNextPayloadAsync()
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
                static long CountBytes(ReadOnlyMemory<Frame> frames)
                {
                    long total = 0;
                    foreach (var frame in frames.Span)
                    {
                        total += frame.GetPayload().Length;
                    }
                    return total;
                }
                Logger.Debug((nextGroup, kind, streamState), static (state, ex) => $"got {CountBytes(state.nextGroup)} payload bytes in {state.nextGroup.Length} buffers; {state.kind}, {state.streamState}");
                switch (kind)
                {
                    case FrameKind.None:

                        switch (streamState)
                        {
                            case StreamState.ExpectHeaders:
                            case StreamState.ExpectBody:
                                await SuspendWorkerAndAwaitReactivationAsync();
                                Logger.Debug("worker resumed");
                                continue; // and try again when we resume
                            default:
                                lock (SyncLock)
                                {
                                    _nextItemNeedsSync = Maybe<TReceive>.NoValue;
                                }
                                return; // there will never be anything else
                        }
                    case FrameKind.Header:
                        _unprocessedHeaders = FrameSequenceSegment.Create(nextGroup.Span);
                        Release(ref nextGroup, releasePayload: false);
                        continue;
                    case FrameKind.Trailer:
                        _unprocessedTrailers = FrameSequenceSegment.Create(nextGroup.Span);
                        Release(ref nextGroup, releasePayload: false);
                        lock (SyncLock)
                        {   // there will never be another value, so...
                            _nextItemNeedsSync = Maybe<TReceive>.NoValue;
                        }
                        return; // not expecting any more
                    case FrameKind.Payload:
                        var value = DeserializePayload(ref nextGroup);
                        lock (SyncLock)
                        {
                            _nextItemNeedsSync = new Maybe<TReceive>(value);
                        }
                        return;
                    default:
                        Logger.Information(kind, (state, _) => $"unexpected {state} frame-group received");
                        break;
                }
            }
            finally
            {
                Release(ref nextGroup, releasePayload: true);
            }
        }
    }

    private ReadOnlySequence<byte> _unprocessedHeaders, _unprocessedTrailers;

    ValueTask SuspendWorkerAndAwaitReactivationAsync() // IMPORTANT; this is how the stream goes to sleep and waits to be activated by a new invocation
    {
        Logger.Debug("suspending worker...");
        lock (SyncLock)
        {
            ChangeState(WorkerState.Active, WorkerState.Suspended);
            return new ValueTask(this, _suspendedContinuationPoint.Version);
        }
    }

    private TReceive DeserializePayload(ref ReadOnlyMemory<Frame> frameGroup)
    {
        Debug.Assert(!frameGroup.IsEmpty, "frame group should not be empty");
        var span = frameGroup.Span;
        var lastHeader = span[span.Length - 1].GetHeader();
        Debug.Assert(lastHeader.Kind == FrameKind.Payload, $"expected payload, got {lastHeader.Kind}");

        // run the deserializer
        var payload = FrameSequenceSegment.Create(span);
        Logger.Debug(payload, static (state, _) => $"deserializing payload, {state.Length} bytes, {(state.IsSingleSegment ? "single segment" : "multiple segments")}...");
        var ctx = Pool<SingleBufferDeserializationContext>.Get();
        ctx.Initialize(payload);
        var value = Deserializer(ctx);
        Logger.Debug(value, static (state, _) => $"deserialized: {state}");
        ctx.Recycle();

        // we can release the original buffers now, before we push the value onwards
        Release(ref frameGroup, releasePayload: true);
        return value;
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
        Logger.SetSource(IsClient ? LogKind.Client : LogKind.Server, "executor");
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
        Logger.Debug((expectedOldState, newState), static (state, _) => $"changing worker state from {state.expectedOldState} to {state.newState}");
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

    private void Release(ref ReadOnlyMemory<Frame> frames, bool releasePayload)
    {
        if (frames.IsEmpty) return;
        try
        {
            if (releasePayload)
            {
                // release the individual frames
                foreach (var frame in frames.Span)
                {
                    frame.Release();
                }
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsFinal(in Frame frame, out FrameKind kind)
        {
            var header = frame.GetHeader();
            kind = header.Kind;
            return header.IsFinal;
        }
        static void ThrowMismatchedGroup(FrameKind group, FrameKind item) => throw new InvalidOperationException($"An unexpected {item} frame was encountered in the backlog while reading a {group} group");

        if (_backlogFrames is not null)
        {
            if (_backlogFrames.TryPeek(out var frame) && IsFinal(frame, out kind))
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
                    if (IsFinal(frame, out kind))
                    {
                        count = 1;
                    }
                    else
                    {
                        count = -1; // use sign to track EOF; if we end up with a -ve number, we didn't find an end
                        while (iter.MoveNext())
                        {
                            bool terminated = IsFinal(iter.Current, out var nextKind);
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
            count = _backlogFrame.HasValue && IsFinal(_backlogFrame, out kind) ? 1 : 0;
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
    public ushort NextSequenceId() => Utilities.IncrementToUInt32(ref _sequenceId);

    protected IMethod Method { get; }

    protected IFrameConnection Output => _output;
    protected abstract Action<TSend, SerializationContext> Serializer { get; }
    protected abstract Func<DeserializationContext, TReceive> Deserializer { get; }

    public ValueTask SendHeaderAsync(string? host, in CallOptions options)
    {
        var ctx = PayloadFrameSerializationContext.Get(this, MemoryPool, FrameKind.Header, 0);
        try
        {
            MetadataEncoder.WriteHeader(ctx, IsClient, Method.FullName, host, options);
            ctx.Complete();
        }
        catch
        {
            ctx.Recycle();
            throw;
        }
        return WriteAndRecycleAsync(ctx);
    }
    public ValueTask SendTrailerAsync(Metadata? metadata, Status? status)
    {
        var ctx = PayloadFrameSerializationContext.Get(this, MemoryPool, FrameKind.Trailer, 0);
        try
        {
            ctx.Complete(); // always empty for now
        }
        catch
        {
            ctx.Recycle();
            throw;
        }
        return WriteAndRecycleAsync(ctx);
    }
    private ValueTask WriteAndRecycleAsync(PayloadFrameSerializationContext ctx)
    {
        var pending = ctx.WritePayloadAsync(Output, CancellationToken);
        if (pending.IsCompleted)
        {
            ctx.Recycle();
            pending.GetAwaiter().GetResult(); // required for IVTS and to propagate exceptions
            return default;
        }
        else
        {
            return Awaited(pending, ctx);
        }
        static async ValueTask Awaited(ValueTask pending, PayloadFrameSerializationContext ctx)
        {
            try
            {
                await pending;
            }
            finally
            {
                ctx.Recycle();
            }
        }
    }


    internal async Task SendSingleAsync(string? host, CallOptions options, TSend request)
    {
        try
        {
            await SendHeaderAsync(host, options);
            await SendAsync(request);
            await SendTrailerAsync(null, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
    }

    public async ValueTask SendAsync(TSend value)
    {
        PayloadFrameSerializationContext? serializationContext = null;
        CancellationToken.ThrowIfCancellationRequested();
        try
        {
            Logger.Debug(value, static (state, ex) => $"serializing {state}...");
            serializationContext = PayloadFrameSerializationContext.Get(this, MemoryPool, FrameKind.Payload, 0);
            Serializer(value, serializationContext);
            Logger.Debug(serializationContext, static (state, _) => $"serialized; {state}");
            await serializationContext.WritePayloadAsync(Output, CancellationToken);
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