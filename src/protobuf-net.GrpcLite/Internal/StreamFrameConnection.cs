using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal;

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
                _logger.Debug(frame, (state, _) => $"write complete (async, releasing {state.TotalLength} bytes)");
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
        byte[] headerBuffer = new byte[FrameHeader.Size];
        while (!cancellationToken.IsCancellationRequested)
        {
            int remaining = FrameHeader.Size, offset = 0, bytesRead;
            _logger.Debug(true, (state, _) => $"reading next header...");
            try
            {
                while (remaining > 0 && (bytesRead = await _duplex.ReadAsync(headerBuffer, offset, remaining, cancellationToken)) > 0)
                {
                    remaining -= bytesRead;
                    offset += bytesRead;
                }

                if (remaining == FrameHeader.Size) yield break; // clean EOF
                if (remaining != 0) ThrowEOF();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }

            var header = FrameHeader.ReadUnsafe(in headerBuffer[0]);
            _logger.Debug(header, (state, _) => $"reading {state}...");

            // note we rent a new buffer even for zero-length payloads, so we can return a frame based on that segment
            Frame.AssertValidLength(header.PayloadLength);
            var slab = FrameBufferManager.Shared.Rent(header.PayloadLength);
            Frame frame;
            try
            {
                if (header.PayloadLength != 0)
                {
                    var payload = slab.ActiveBuffer.Slice(0, header.PayloadLength);
                    while (!payload.IsEmpty && (bytesRead = await _duplex.ReadAsync(payload, cancellationToken)) > 0)
                    {
                        payload = payload.Slice(bytesRead);
                    }
                    if (!payload.IsEmpty) ThrowEOF();
                    slab.Advance(header.PayloadLength);
                }
                frame = slab.CreateFrameAndInvalidate(header, updateHeaderLength: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
            finally
            {
                slab?.Return();
            }
            _logger.Debug(frame, (state, _) => $"yielding {state}...");
            yield return frame;
        }
        _logger.Debug(cancellationToken.IsCancellationRequested, static (state, _) => $"stream-frame connection exiting cleanly; cancelled: {state}");
        static void ThrowEOF() => throw new EndOfStreamException();
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> frames, CancellationToken cancellationToken)
    {
        _logger.Debug(frames, (state, _) => $"writing {state}, {state.Length} bytes...");
        return _duplex.WriteAsync(frames, cancellationToken);
    }

    public ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
    {
        _logger.Debug(frame, (state, _) => $"writing {state}, {state.TotalLength} bytes...");
        var pending = _duplex.WriteAsync(frame.RawBuffer, cancellationToken);
        if (pending.IsCompleted)
        {
            _logger.Debug(frame, (state, _) => $"write complete (sync, releasing {state.TotalLength} bytes)");
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
