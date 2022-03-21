using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class StreamFrameConnection : IRawFrameConnection, IValueTaskSource
{
    bool IFrameConnection.ThreadSafeWrite => false;

    private readonly ILogger? _logger;
    private readonly Stream _duplex;

    private readonly Action _writeComplete;
    private ValueTaskAwaiter _pendingWrite;
    private ManualResetValueTaskSourceCore<bool> _vts;
    private object? _completion;

    Frame _writingFrame;

    public StreamFrameConnection(Stream duplex, ILogger? logger = null)
    {
        _duplex = duplex.CheckDuplex();
        _logger = logger;
        _writeComplete = () =>
        {
            var frame = _writingFrame;
            var pending = _pendingWrite;
            _writingFrame = default;
            _pendingWrite = default;
            try
            {
                _logger.Debug(frame, static (state, _) => $"write complete (async, releasing {state.TotalLength} bytes)");
                frame.Release();
                pending.GetResult();
                _vts.SetResult(true);
            }
            catch (Exception ex)
            {
                _vts.SetException(ex);
            }
        };
    }

    public ValueTask DisposeAsync()
    {
        _ = Utilities.GetLazyCompletion(ref _completion, true);
        return Utilities.SafeDisposeAsync(_duplex);
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken)
        => new ValueTask(_duplex.FlushAsync(cancellationToken));

    Task IFrameConnection.Complete => Utilities.GetLazyCompletion(ref _completion, false);

    public async IAsyncEnumerator<Frame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var builder = Frame.CreateBuilder();
        try
        {
            int bytesRead;
            _logger.Debug(builder.GetBuffer(), static (state, _) => $"reading up-to {state.Length} bytes from stream...");
            while (!cancellationToken.IsCancellationRequested & (bytesRead = await _duplex.ReadAsync(builder.GetBuffer(), cancellationToken)) > 0)
            {
                _logger.Debug(bytesRead, static (state, _) => $"read {state} bytes from stream; parsing...");
                while (builder.TryRead(ref bytesRead, out var frame))
                {
                    _logger.Debug((frame, bytesRead), static (state, _) => $"parsed {state.frame}; {state.bytesRead} remaining");
                    yield return frame;
                }
            }
            if (!cancellationToken.IsCancellationRequested)
            {
                if (builder.InProgress) ThrowEOF(); // incomplete frame detected
                static void ThrowEOF() => throw new EndOfStreamException();
            }
        }
        finally
        {
            builder.Release();
        }
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> frames, CancellationToken cancellationToken)
    {
        _logger.Debug(frames, static (state, _) => $"writing {state}, {state.Length} bytes...");
        return _duplex.WriteAsync(frames, cancellationToken);
    }

    public ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
    {
        _logger.Debug(frame, static (state, _) => $"writing {state}, {state.TotalLength} bytes...");
        var pending = _duplex.WriteAsync(frame.Memory, cancellationToken);
        if (pending.IsCompleted)
        {
            _logger.Debug(frame, static (state, _) => $"write complete (sync, releasing {state.TotalLength} bytes)");
            frame.Release();
            return pending;
        }
        return ScheduleRelease(frame, pending);
    }
    private ValueTask ScheduleRelease(in Frame frame, in ValueTask pending)
    {
        _pendingWrite = pending.GetAwaiter();
        _writingFrame = frame;
        var result = new ValueTask(this, _vts.Version);
        _pendingWrite.UnsafeOnCompleted(_writeComplete);
        return result;
    }

    void IValueTaskSource.GetResult(short token)
    {
        try
        {
            _vts.GetResult(token);
        }
        finally
        {
            if (token == _vts.Version) _vts.Reset();
        }
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => _vts.GetStatus(token);

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _vts.OnCompleted(continuation, state, token, flags);

    public ValueTask WriteAsync(ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken = default)
        => this.WriteAllAsync(frames, cancellationToken);

    void IFrameConnection.Close(Exception? exception)
    {
        _ = DisposeAsync().AsTask();
    }
}
