using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IStream : IPooled
{
    ushort Id { get; }
    ushort NextSequenceId();
    MethodType MethodType { get; }
    string Method { get; }
    bool TryAcceptFrame(in Frame frame);
}

internal enum HandlerState
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
    IsActive = 1 << 31,

    // for the step, we don't need to remove *all* the flags, because most of the flags are built into the
    // values directly; we only need to remove the flags that *aren't* handled that way, for example
    // the "is there an active worker" flag
    StepMask = ~IsActive,
}
internal abstract class HandlerBase<TSend, TReceive> : IStream, IWorker where TSend : class where TReceive : class
{
    string IStream.Method => Method!.FullName;
    protected FrameBufferManager BufferManager => FrameBufferManager.Shared;

    private ushort _streamId;
    private ILogger? _logger;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // these are all set via initialize
    private IMethod _method;
    IFrameConnection _output;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    // IMPORTANT: state and backlog changes should be synchronized
    private HandlerState _state = HandlerState.Uninitialized;
    private Queue<Frame>? _backlogFrames;
    Frame _backlogFrame;

    private Task _currentWorker = Task.CompletedTask;

    protected CancellationToken StreamCancellation => default;



    // don't need volatile read; if we care about correctness, we'll be reading inside a lock; if
    // we don't care about correctness, then *we don't care about correctness*
    public bool IsActive => (_state & HandlerState.IsActive) != 0;
    public HandlerState Step => _state & HandlerState.StepMask;

    private object SyncLock => this;

    bool IStream.TryAcceptFrame(in Frame frame)
    {
        var header = frame.GetHeader();
        lock (SyncLock)
        {
            var need = header.Kind switch
            {
                FrameKind.Header => HandlerState.AcceptHeader,
                FrameKind.Payload => HandlerState.AcceptPayload,
                FrameKind.Trailer => HandlerState.AcceptTrailer,
                FrameKind.Status => HandlerState.AcceptStatus,
                _ => default,
            };
            if ((_state & need) == 0) return false; // not expecting that

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
            if ((header.KindFlags & (byte)PayloadFlags.EndItem) != 0 && !IsActive)
            {
                // we have something useful, and there's no current worker: start a worker!
                Activate();
            }
            return true;
        }
    }

    public void Activate()
    {
        lock (SyncLock)
        {
            // only if not already active
            if ((_state & HandlerState.IsActive) == 0)
            {
                _state |= HandlerState.IsActive;
                this.StartWorker();
            }
        }
    }

    // this is the main pump; when useful data has become available, the framework activates
    // this method as a worker (as needed), which isolates each stream from the main IO loop
    public void Execute()
    {
        Logging.SetSource((IsClient ? Logging.ClientPrefix : Logging.ServerPrefix) + "stream " + Method.Name);
        bool start;
        lock (SyncLock)
        {
            start = !_currentWorker.IsCompleted;
        }

        try
        {
            if (start)
            {
                _currentWorker = ExecuteCoreAsync();
            }
            else
            {
                _logger.Information("not starting worker; existing worker is still active");
            }
        }
        catch (Exception ex)
        {   // unable to start synchronously?
            ReportFault(ex);
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
            _logger.Critical(ex);
            lock (SyncLock)
            {
                // normally when setting state we'd need to preserve the active flag,
                // but we're exiting, so...
                _state = HandlerState.Faulted;
                ReleaseBacklogLocked();
            }
        }
        catch (Exception innerEx)
        {   // ok, even our fault handler is faulting; try one last log
            _logger.Critical(innerEx);
        }
    }
    private async Task ExecuteCoreAsync()
    {
        ReadOnlyMemory<Frame> frameGroup = default;
        FrameKind kind = FrameKind.None;
        try
        {
            // if we're here, we're on a worker thread that represents this stream, and we expect there to be something useful to do
            while (true)
            {
                lock (SyncLock)
                {
                    switch (Step)
                    {
                        case HandlerState.ExpectHeaders:
                        case HandlerState.ExpectBody:
                        case HandlerState.ExpectTrailers:
                        case HandlerState.ExpectStatus:
                            frameGroup = ReadNextFrameGroupLocked(out kind); // but we'll process it *outside* of the lock
                            break; // these are the things we *expect*, i.e. normal
                        default:
                            // something unexpected
                            ReleaseBacklogLocked();
                            return;

                    }
                }

                if (frameGroup.IsEmpty) return; // no frames to process

                switch (kind)
                {
                    case FrameKind.Header:
                        await OnTrailerAsync(frameGroup);
                        break;
                    case FrameKind.Payload:
                        await OnPayloadAsync(ref frameGroup, out var isFinal);
                        if (isFinal) await OnPayloadEnd();
                        break;
                    case FrameKind.Trailer:
                        await OnTrailerAsync(frameGroup);
                        break;
                    case FrameKind.Status:
                        await OnStatusAsync(frameGroup);
                        break;
                }
                Release(ref frameGroup);
            }
        }
        catch (Exception ex)
        {
            ReportFault(ex);
        }
        finally
        {
            lock (SyncLock)
            {
                _state &= HandlerState.IsActive;
            }
            Release(ref frameGroup);
        }
    }

    protected virtual ValueTask OnHeaderAsync(ReadOnlyMemory<Frame> frameGroup) => default;
    private ValueTask OnPayloadAsync(ref ReadOnlyMemory<Frame> frameGroup, out bool isFinal)
    {
        Debug.Assert(!frameGroup.IsEmpty, "frame group should not be empty");
        var span = frameGroup.Span;
        var lastHeader = span[span.Length - 1].GetHeader();
        Debug.Assert(lastHeader.Kind == FrameKind.Payload, $"expected payload, got {lastHeader.Kind}");

        // check the state flags
        var flags = (PayloadFlags)lastHeader.KindFlags;
        isFinal = (flags & PayloadFlags.FinalItem) != 0;
        if ((flags & PayloadFlags.NoItem) != 0) return default; // don't run the deserializer if they're telling is "no item" (caller will release the buffers)

        // run the deserializer
        var payload = FrameSequenceSegment.Create(span);
        _logger.Debug(payload, static (state, _) => $"deserializing payload, {state.Length} bytes, {(state.IsSingleSegment ? "single segment" : "multiple segments")}...");
        var ctx = Pool<SingleBufferDeserializationContext>.Get();
        ctx.Initialize(payload);
        var value = Deserializer(ctx);
        _logger.Debug(value, static (state, _) => $"deserialized: {state}");
        ctx.Recycle();

        // we can release the original buffers now, before we push the value onwards
        Release(ref frameGroup); 
        return OnPayloadAsync(value);
    }
    protected virtual ValueTask OnPayloadEnd() => default;
    protected abstract ValueTask OnPayloadAsync(TReceive value);
    protected virtual ValueTask OnTrailerAsync(ReadOnlyMemory<Frame> frameGroup) => default;
    protected virtual ValueTask OnStatusAsync(ReadOnlyMemory<Frame> frameGroup) => default;

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
            _logger.Critical(ex);
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
    protected ILogger? Logger => _logger;
    protected IMethod Method
    {
        get => _method;
        set => _method = value;
    }
    protected IFrameConnection Output => _output;

    public virtual void Initialize(ushort streamId, IFrameConnection output, ILogger? logger)
    {
        if (_state != HandlerState.Uninitialized) Throw(_state);
        _output = output;
        _logger = logger;
        _streamId = streamId;
        _sequenceId = ushort.MaxValue; // so first is zero
        _state = HandlerState.ExpectHeaders;
        _currentWorker = Task.CompletedTask;

        static void Throw(HandlerState state) => throw new InvalidOperationException($"Attempted to initialize a handler that was: {state}");
    }

    protected abstract Action<TSend, SerializationContext> Serializer { get; }
    protected abstract Func<DeserializationContext, TReceive> Deserializer { get; }

    public virtual void Recycle()
    {
        _state = HandlerState.Uninitialized;
        _currentWorker = Task.CompletedTask;
        _output = null!;
        _logger = null;
    }

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
            _logger.Error(ex);
        }
    }

    public async ValueTask SendAsync(TSend value, PayloadFlags flags, CancellationToken cancellationToken)
    {
        PayloadFrameSerializationContext? serializationContext = null;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _logger.Debug(value, (state, ex) => $"serializing {state}...");
            serializationContext = PayloadFrameSerializationContext.Get(this, BufferManager, Id, flags);
            Serializer(value, serializationContext);
            _logger.Debug(serializationContext, (state, ex) => $"serialized; {serializationContext}");
            await serializationContext.WritePayloadAsync(Output, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
        finally
        {
            serializationContext?.Recycle();
        }
    }

    MethodType IStream.MethodType => _method!.Type;
}