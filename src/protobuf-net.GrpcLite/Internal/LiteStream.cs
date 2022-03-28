using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IStream
{
    ushort Id { get; }
    ushort NextSequenceId();
    MethodType MethodType { get; }
    string Method { get; }
    bool TryAcceptFrame(in Frame frame);
    CancellationToken CancellationToken { get; }
    void Cancel();
    IConnection? Connection { get; }
    WriteOptions WriteOptions { get; set; }
    bool IsActive { get; }
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
    Unsuspending,
    RanToCompletion,
    Faulted,
}
internal abstract class LiteStream<TSend, TReceive> : IStream, IWorker, IAsyncStreamReader<TReceive>, IValueTaskSource<bool>, IDisposable where TSend : class where TReceive : class
{
    private volatile bool _isActive = true;
    public bool IsActive
    {
        get => _isActive;
        private set => _isActive = value;
    }
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
        // note that RegisterCancellation may not do anything if it recognizes this as ctx.CancellationToken, etc
        CancellationTokenRegistration ctr = cancellationToken.CanBeCanceled ? this.RegisterCancellation(cancellationToken) : default;
        try
        {
            var pending = MoveNextPayloadAsync();
            if (pending.IsCompletedSuccessfully)
            {
                return GetNextItem().HasValue ? Utilities.AsyncTrue : Utilities.AsyncFalse;
            }
            else
            {
                var tmp = ctr;
                ctr = default; // to disable cancel
                return Awaited(this, pending, tmp);
            }
        }
        finally
        {
            ctr.SafeDispose();
        }
        
        async Task<bool> Awaited(LiteStream<TSend, TReceive> stream, ValueTask pending, CancellationTokenRegistration ctr)
        {
            try
            {
                await pending;
                return GetNextItem().HasValue;
            }
            finally
            {
                ctr.SafeDispose();
            }
        }
    }

    protected LiteStream(IMethod method, IConnection owner)
    {
        Method = method;
        _streamId = ushort.MaxValue; // will be updated after construction
        _sequenceId = ushort.MaxValue; // so first is zero
        StreamState = StreamState.ExpectHeaders;
        _connection = owner;
        _workerStateNeedsSync = IsClient ? WorkerState.Active : WorkerState.NotStarted; // for clients, the caller is the initial executor
    }
    IConnection _connection;
    public IConnection Connection => _connection;
    protected void SetConnection(IConnection connection) => _connection = connection;

    string IStream.Method => Method!.FullName;
    protected RefCountedMemoryPool<byte> Pool => _connection.Pool;

    private ushort _streamId;
    public ILogger? Logger { get; set; }

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

    public virtual void Dispose()
    {
        if (IsActive) ((IStream)this).Cancel();
    }

    [Flags]
    private enum CancellationScenerio : byte
    {
        None = 0,
        OwnerShutdown = 1 << 0,
        StreamSpecific = 1 << 1,
        Deadline = 1 << 2,
    }

    int cancellationRegistered;
    internal virtual CancellationTokenRegistration RegisterForCancellation(CancellationToken streamSpecificCancellation, DateTime? deadline)
    {
        if (Interlocked.CompareExchange(ref cancellationRegistered, 1, 0) != 0)
        {
            throw new InvalidOperationException("Cancellation already registered");
        }
        CancellationScenerio scenario = CancellationScenerio.None;
        var ownerShutdown = _connection?.Shutdown ?? CancellationToken.None;
        if (ownerShutdown.CanBeCanceled)
        {
            ownerShutdown.ThrowIfCancellationRequested();
            scenario |= CancellationScenerio.OwnerShutdown;
        }
        if (streamSpecificCancellation.CanBeCanceled && streamSpecificCancellation != ownerShutdown)
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

        CancellationToken newCancellationToken;
        // 8 possibilities
        switch (scenario)
        {
            case CancellationScenerio.None:
                newCancellationToken = CancellationToken.None;
                break; // nothing to do
            case CancellationScenerio.OwnerShutdown:
                newCancellationToken = ownerShutdown;
                break;
            case CancellationScenerio.StreamSpecific:
                newCancellationToken = streamSpecificCancellation;
                break;
            case CancellationScenerio.Deadline:
                _cancellation = new CancellationTokenSource();
                newCancellationToken = _cancellation.Token;
                break; // deadline dealt with below
            case CancellationScenerio.OwnerShutdown | CancellationScenerio.StreamSpecific:
            case CancellationScenerio.OwnerShutdown | CancellationScenerio.StreamSpecific | CancellationScenerio.Deadline:
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(ownerShutdown, streamSpecificCancellation);
                newCancellationToken = _cancellation.Token;
                break; // deadline dealt with below
            case CancellationScenerio.OwnerShutdown | CancellationScenerio.Deadline:
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(ownerShutdown);
                newCancellationToken = _cancellation.Token;
                break; // deadline dealt with below
            case CancellationScenerio.StreamSpecific | CancellationScenerio.Deadline:
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(streamSpecificCancellation);
                newCancellationToken = _cancellation.Token;
                break; // deadline dealt with below
            default:
                throw new InvalidOperationException($"Unexpected cancellation scenario: {scenario}");
        }
        if (_cancellation is not null)
        {
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
        var reg = this.RegisterCancellation(newCancellationToken);
        CancellationToken = newCancellationToken; // need to do this *afer* RegisterCancellation has checked
        return reg;
    }

    protected virtual void OnCancel() { }

    void IStream.Cancel()
    {
        try
        {
            var wasActive = IsActive;
            OnComplete();
            if (wasActive)
            {
                OnCancel();
                try
                {
                    ThrowCancelled();
                }
                catch (Exception ex)
                {
                    ReportFault(ex);
                }
            }
            
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
    }
    private void ThrowCancelled()
    {
        // try to give the most meaningful cancellation
        var token = _connection?.Shutdown ?? CancellationToken.None;
        if (token.IsCancellationRequested)
        {
            throw new OperationCanceledException($"The {(IsClient ? "client" : "server")} is being shut-down.", token);
        }
        token = _streamSpecificCancellation;
        if (token.IsCancellationRequested)
        {
            throw new OperationCanceledException($"The {(IsClient ? "client" : "server")} stream is being cancelled.", token);
        }
        throw new TimeoutException(); // that leaves a deadline, then
    }

    private object SyncLock => this;

    bool IStream.TryAcceptFrame(in Frame frame)
    {
        var header = frame.GetHeader();
        lock (SyncLock)
        {
            var need = header.Kind switch
            {
                FrameKind.StreamHeader => StreamState.AcceptHeader,
                FrameKind.StreamPayload => StreamState.AcceptPayload,
                FrameKind.StreamTrailer => StreamState.AcceptTrailer,
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
            Logger.Debug((frame, obj: this), static (state, _) => $"added backlog frame: {state.frame}; total frames: {state.obj.CountBacklogFramesLocked()}");
            if (header.IsFinal)
            {
                StartWorkerAsNeeded();
            }
            return true;
        }
    }

    private int CountBacklogFramesLocked()
    {
        if (_backlogFrames is not null) return _backlogFrames.Count;
        return _backlogFrame.HasValue ? 1 : 0;
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
            if (!IsActive) ThrowCancelled(); // nope!
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
                Logger.Debug((nextGroup, kind, streamState), static (state, ex) => $"got {CountBytes(state.nextGroup)} {state.kind} bytes in {state.nextGroup.Length} buffers (stream: {state.streamState})");
                switch (kind)
                {
                    case FrameKind.None:

                        switch (streamState)
                        {
                            case StreamState.ExpectHeaders:
                            case StreamState.ExpectBody:
                                bool wasSuspended = await SuspendWorkerAndAwaitNextGroupAsync();
                                if (wasSuspended) Logger.Debug("worker resumed");
                                continue; // and try again when we resume
                            default:
                                lock (SyncLock)
                                {
                                    _nextItemNeedsSync = Maybe<TReceive>.NoValue;
                                }
                                return; // there will never be anything else
                        }
                    case FrameKind.StreamHeader:
                        _unprocessedHeaders = FrameSequenceSegment.Create(nextGroup.Span);
                        OnHeaders();
                        Release(ref nextGroup, releasePayload: false);
                        continue;
                    case FrameKind.StreamTrailer:
                        _unprocessedTrailers = FrameSequenceSegment.Create(nextGroup.Span);
                        OnTrailers();
                        Release(ref nextGroup, releasePayload: false);
                        lock (SyncLock)
                        {   // there will never be another value, so...
                            _nextItemNeedsSync = Maybe<TReceive>.NoValue;
                        }
                        if (IsClient) OnComplete(); // mark as no longer active
                        return; // not expecting any more
                    case FrameKind.StreamPayload:
                        var value = DeserializePayload(ref nextGroup);
                        lock (SyncLock)
                        {
                            _nextItemNeedsSync = new Maybe<TReceive>(value);
                        }
                        return;
                    default:
                        Logger.Information(kind, static (state, _) => $"unexpected {state} frame-group received");
                        break;
                }
            }
            finally
            {
                Release(ref nextGroup, releasePayload: true);
            }
        }
    }

    protected virtual void OnHeaders() { }

    protected virtual void OnTrailers() { }

    private ReadOnlySequence<byte> _unprocessedHeaders, _unprocessedTrailers;
    protected ReadOnlySequence<byte> RawHeaders => _unprocessedHeaders;
    protected ReadOnlySequence<byte> RawTrailers => _unprocessedTrailers;

    ValueTask<bool> SuspendWorkerAndAwaitNextGroupAsync() // IMPORTANT; this is how the stream goes to sleep and waits to be activated by a new invocation
    {
        lock (SyncLock)
        {
            if (PeekNextFrameGroupSizeLocked(out _) > 0) return default; // there already is another group (racing)

            Logger.Debug("suspending worker...");
            ChangeState(WorkerState.Active, WorkerState.Suspended);
            return new ValueTask<bool>(this, _suspendedContinuationPoint.Version);
        }
    }

    private TReceive DeserializePayload(ref ReadOnlyMemory<Frame> frameGroup)
    {
        Debug.Assert(!frameGroup.IsEmpty, "frame group should not be empty");
        var span = frameGroup.Span;
        var lastHeader = span[span.Length - 1].GetHeader();
        Debug.Assert(lastHeader.Kind == FrameKind.StreamPayload, $"expected payload, got {lastHeader.Kind}");

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
                    _workerStateNeedsSync = WorkerState.Unsuspending;
                    this.StartWorker();
                    break;
            }
        }
    }

    private void ReleaseBacklogLocked()
    {
        _backlogFrame.Release();
        _backlogFrame = default;
        if (_backlogFrames is not null)
        {
#if NET472
            while (_backlogFrames.Count != 0)
            {
                _backlogFrames.Dequeue().Release();
            }
#else
            while (_backlogFrames.TryDequeue(out var frame))
            {
                frame.Release();
            }
#endif
        }
    }

    protected virtual void ReportFault(Exception ex, [CallerMemberName] string caller = "")
    {
        try
        {
            Logger.Critical(caller, static (state, e) => $"{e?.Message} (from: {state})", ex);
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

        // if necessary, reactivate with fault - without risking blocking the listener via callbacks
#if NET472
        ThreadPool.QueueUserWorkItem(static state =>
        {
            var tuple = (Tuple<LiteStream<TSend, TReceive>, Exception>)state;
            try
            {
                tuple.Item1._suspendedContinuationPoint.SetException(tuple.Item2);
            }
            catch (Exception innerEx)
            {
                tuple.Item1.Logger.Critical(innerEx);
            }
        }, Tuple.Create(this, ex));
#else
        ThreadPool.QueueUserWorkItem(static state =>
        {
            try
            {
                state.obj._suspendedContinuationPoint.SetException(state.ex);
            }
            catch (Exception innerEx)
            {
                state.obj.Logger.Critical(innerEx);
            }
        }, (obj: this, ex), false);
#endif
    }
    public void Execute()
    {
        Logger.SetSource(IsClient ? LogKind.Client : LogKind.Server, "executor");
        try
        {
            WorkerState previousState;
            lock (SyncLock)
            {
                previousState = _workerStateNeedsSync;
                switch (previousState)
                {
                    case WorkerState.ActivatingFirstTime:
                    case WorkerState.Unsuspending:
                        _workerStateNeedsSync = WorkerState.Active;
                        break;
                }
            }

            // now do the real code outside of the lock
            switch (previousState)
            {
                case WorkerState.ActivatingFirstTime:
                    _ = ExecuteCoreAsync();
                    break;
                case WorkerState.Unsuspending:
                    _suspendedContinuationPoint.SetResult(true); // the result here is just used to say "yes, you were suspended"
                    break;
                default:
                    throw new InvalidOperationException($"unexpected worker state '{previousState}' when attempting to activate worker");
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
            Logger.Critical((expectedOldState, newState, actualOld), static (state, _) => $"unexpected worker state '{state.actualOld}' when attempting to move from '{state.expectedOldState}' to '{state.newState}'");
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
            _connection?.Remove(Id);
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

    private int PeekNextFrameGroupSizeLocked(out FrameKind kind)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsFinal(in Frame frame, out FrameKind kind)
        {
            var header = frame.GetHeader();
            kind = header.Kind;
            return header.IsFinal;
        }
        static void ThrowWorkserState(WorkerState state) => throw new InvalidOperationException($"Unexpected worker state: {state}");
        static void ThrowMismatchedGroup(FrameKind group, FrameKind item) => throw new InvalidOperationException($"An unexpected {item} frame was encountered in the backlog while reading a {group} group");

        CancellationToken.ThrowIfCancellationRequested();
        if (_workerStateNeedsSync != WorkerState.Active) ThrowWorkserState(_workerStateNeedsSync);
        int count;
        kind = FrameKind.None;

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
        return count;
    }
    private ReadOnlyMemory<Frame> ReadNextFrameGroupLocked(out FrameKind kind)
    {
        int count = PeekNextFrameGroupSizeLocked(out kind);
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
        Logger.Debug((count, kind, obj: this), static (state, _) => $"Dequeued {state.kind} in {state.count} buffers; {state.obj.CountBacklogFramesLocked()} buffers remain");
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

    protected ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> Output
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _connection.Output;
    }
    protected abstract Action<TSend, SerializationContext> Serializer { get; }
    protected abstract Func<DeserializationContext, TReceive> Deserializer { get; }

    protected void TrySendCancellation()
    {
        var frame = Frame.CreateFrame(Pool, new FrameHeader(FrameKind.StreamCancel, 0, Id, NextSequenceId()));
        var val = (frame, FrameWriteFlags.None);
        if (!Output.TryWrite(val))
        {
            _ = Observe(Output.WriteAsync(val, CancellationToken.None));
        }
        static async Task Observe(ValueTask pending)
        {
            try { await pending; } catch { }
        }
    }

    public ValueTask SendHeaderAsync(string? host, in CallOptions options, FrameWriteFlags flags)
    {
        var ctx = PayloadFrameSerializationContext.Get(this, Pool, FrameKind.StreamHeader);
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
        return WriteAndRecycleAsync(ctx, flags);
    }

    protected void OnComplete()
    {
        IsActive = false;
        _connection?.Remove(Id);
    }

    public ValueTask SendTrailerAsync(Metadata? metadata, Status? status, FrameWriteFlags flags)
    {
        var ctx = PayloadFrameSerializationContext.Get(this, Pool, FrameKind.StreamTrailer);
        try
        {
            if (status.HasValue) MetadataEncoder.WriteStatus(ctx, status.GetValueOrDefault());
            if (metadata is not null && metadata.Count != 0) MetadataEncoder.WriteMetadata(ctx, metadata);
            ctx.Complete();
        }
        catch
        {
            ctx.Recycle();
            throw;
        }
        return WriteAndRecycleAsync(ctx, flags);
    }

    private ValueTask WriteAndRecycleAsync(PayloadFrameSerializationContext ctx, FrameWriteFlags flags)
    {
        var pending = ctx.WritePayloadAsync(Output, flags, CancellationToken);
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
            await SendHeaderAsync(host, options, FrameWriteFlags.BufferHint);
            await SendAsync(request, FrameWriteFlags.BufferHint);
            await SendTrailerAsync(null, null, FrameWriteFlags.None);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
    }

    private WriteOptions? _writeOptions;
    public WriteOptions WriteOptions
    {
        get => _writeOptions ??= WriteOptions.Default;
        set => _writeOptions = value;
    }

    protected FrameWriteFlags WriterFlags
    {
        get
        {
            var options = _writeOptions?.Flags ?? 0; // TODO prefer buffer hint if WriteOptions not explicitly specified (need to figure out auto-flush on read-next)
            return (options & WriteFlags.BufferHint) != 0 ? FrameWriteFlags.BufferHint : FrameWriteFlags.None;
        }
    }
    public virtual async ValueTask SendAsync(TSend value, FrameWriteFlags flags)
    {
        //this.Write
        PayloadFrameSerializationContext? serializationContext = null;
        CancellationToken.ThrowIfCancellationRequested();
        try
        {
            Logger.Debug(value, static (state, ex) => $"serializing {state}...");
            serializationContext = PayloadFrameSerializationContext.Get(this, Pool, FrameKind.StreamPayload);
            Serializer(value, serializationContext);
            Logger.Debug(serializationContext, static (state, _) => $"serialized; {state}");
            await serializationContext.WritePayloadAsync(Output, flags, CancellationToken);
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
    bool IValueTaskSource<bool>.GetResult(short token)
    {
        lock (SyncLock)
        {
            try
            {
                return _suspendedContinuationPoint.GetResult(token);
            }
            finally
            {
                if (token == _suspendedContinuationPoint.Version) _suspendedContinuationPoint.Reset();
            }
        }
    }

    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
    {
        lock (SyncLock)
        {
            return _suspendedContinuationPoint.GetStatus(token);
        }
    }

    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _suspendedContinuationPoint.OnCompleted(continuation, state, token, flags);

    MethodType IStream.MethodType => Method!.Type;
}