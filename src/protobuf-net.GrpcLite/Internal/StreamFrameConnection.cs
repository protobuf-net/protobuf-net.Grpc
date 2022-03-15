﻿using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class StreamFrameConnection : IFrameConnection, IValueTaskSource
{
    bool IFrameConnection.ThreadSafeWrite => false;

    private readonly ILogger? _logger;
    private readonly Stream _input, _output;

    private readonly Action _writeComplete;
    private ValueTaskAwaiter _pendingWrite;
    private ManualResetValueTaskSourceCore<bool> _vts;
    private object? _completion;

    Frame _writingFrame;

    public StreamFrameConnection(Stream input, Stream output, ILogger? logger = null)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!input.CanRead) throw new ArgumentException("Cannot read from input stream", nameof(input));
        if (!output.CanWrite) throw new ArgumentException("Cannot write to output stream", nameof(output));
        _logger = logger;
        _input = input;
        _output = output;
        _writeComplete = () =>
        {
            var frame = _writingFrame;
            var pending = _pendingWrite;
            _writingFrame = default;
            _pendingWrite = default;
            try
            {
                _logger.LogDebug(frame, (state, _) => $"write complete (async, releasing {state.TotalLength} bytes)");
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
        return Utilities.SafeDisposeAsync(_input, _output);
        
    }

    Task IFrameConnection.Complete => Utilities.GetLazyCompletion(ref _completion, false);

    public async IAsyncEnumerator<Frame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        byte[] headerBuffer = new byte[FrameHeader.Size];
        while (!cancellationToken.IsCancellationRequested)
        {
            int remaining = FrameHeader.Size, offset = 0, bytesRead;
            _logger.LogDebug(true, (state, _) => $"reading next header...");
            while (remaining > 0 && (bytesRead = await _input.ReadAsync(headerBuffer, offset, remaining, cancellationToken)) > 0)
            {
                remaining -= bytesRead;
                offset += bytesRead;
            }
            if (remaining == FrameHeader.Size) yield break; // clean EOF
            if (remaining != 0) ThrowEOF();

            var header = FrameHeader.ReadUnsafe(in headerBuffer[0]);
            _logger.LogDebug(header, (state, _) => $"reading {state}...");

            // note we rent a new buffer even for zero-length payloads, so we can return a frame based on that segment
            Frame.AssertValidLength(header.PayloadLength);
            var slab = FrameBufferManager.Shared.Rent(header.PayloadLength);
            Frame frame;
            try
            {
                if (header.PayloadLength != 0)
                {
                    var payload = slab.ActiveBuffer.Slice(0, header.PayloadLength);
                    while (!payload.IsEmpty && (bytesRead = await _input.ReadAsync(payload, cancellationToken)) > 0)
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
                _logger.LogError(ex);
                throw;
            }
            finally
            {
                slab?.Return();
            }
            _logger.LogDebug(frame, (state, _) => $"yielding {state}...");
            yield return frame;
        }
        _logger.LogDebug(cancellationToken.IsCancellationRequested, static (state, _) => $"stream-frame connection exiting cleanly; cancelled: {state}");
        static void ThrowEOF() => throw new EndOfStreamException();
    }

    public ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
    {
        _logger.LogDebug(frame, (state, _) => $"writing {state}, {state.TotalLength} bytes...");
        var pending = _output.WriteAsync(frame.RawBuffer, cancellationToken);
        if (pending.IsCompleted)
        {
            _logger.LogDebug(frame, (state, _) => $"write complete (sync, releasing {state.TotalLength} bytes)");
            frame.Release();
            return pending;
        }
        return ScheduleRelease(frame, pending);
    }
    private ValueTask ScheduleRelease(in Frame frame, in ValueTask pending)
    {
        _pendingWrite = pending.GetAwaiter();
        _writingFrame = frame;
        _vts.Reset();
        var result = new ValueTask(this, _vts.Version);
        _pendingWrite.UnsafeOnCompleted(_writeComplete);
        return result;
    }

    void IValueTaskSource.GetResult(short token)
        => _vts.GetResult(token);

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
