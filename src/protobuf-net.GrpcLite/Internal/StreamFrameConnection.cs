using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class StreamFrameConnection : IFrameConnection, IValueTaskSource
{
    bool IFrameConnection.ThreadSafeWrite => false;
    private readonly Stream _input, _output;

    private ValueTaskAwaiter _pendingWrite;
    private readonly Action _writeComplete;
    private BufferSegment _writingSegment;
    private ManualResetValueTaskSourceCore<bool> _vts;

    public StreamFrameConnection(Stream input, Stream output)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!input.CanRead) throw new ArgumentException("Cannot read from input stream", nameof(input));
        if (!output.CanWrite) throw new ArgumentException("Cannot write to output stream", nameof(output));
        _input = input;
        _output = output;
        _writeComplete = () =>
        {
            var segment = _writingSegment;
            _writingSegment = default;
            var pending = _pendingWrite;
            _pendingWrite = default;
            try
            {
                segment.Release();
                pending.GetResult();
                _vts.SetResult(true);
            }
            catch (Exception ex)
            {
                _vts.SetException(ex);
            }
        };
    }

    public ValueTask DisposeAsync() => Utilities.SafeDisposeAsync(_input, _output);

    public async IAsyncEnumerator<NewFrame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        byte[] headerBuffer = new byte[FrameHeader.Size];
        while (!cancellationToken.IsCancellationRequested)
        {
            int remaining = FrameHeader.Size, offset = 0, bytesRead;
            while (remaining > 0 && (bytesRead = await _input.ReadAsync(headerBuffer, offset, remaining, cancellationToken)) > 0)
            {
                remaining -= bytesRead;
                offset += bytesRead;
            }
            if (remaining == FrameHeader.Size) yield break; // clean EOF
            if (remaining != 0) ThrowEOF();

            var header = FrameHeader.ReadUnsafe(ref headerBuffer[0]);

            if (header.Length == 0)
            {
                yield return new NewFrame(header); // we're done
            }
            else
            {
                var dataBuffer = ArrayPool<byte>.Shared.Rent(header.Length);
                remaining = header.Length;
                offset = 0;
                while (remaining > 0 && (bytesRead = await _input.ReadAsync(dataBuffer, offset, remaining, cancellationToken)) > 0)
                {
                    remaining -= bytesRead;
                    offset += bytesRead;
                }
                if (remaining != 0) ThrowEOF();
                yield return new NewFrame(header, new BufferSegment(dataBuffer, 0, header.Length));
            }

        }
        static void ThrowEOF() => throw new EndOfStreamException();
    }

    public ValueTask WriteAsync(BufferSegment frames, CancellationToken cancellationToken)
    {
        var pending = new ValueTask(_output.WriteAsync(frames.Array, frames.Offset, frames.Length, cancellationToken));
        if (pending.IsCompleted)
        {
            frames.Release();
            return pending;
        }
        return ScheduleRelease(frames, pending);
    }
    private ValueTask ScheduleRelease(in BufferSegment frames, in ValueTask pending)
    {
        _pendingWrite = pending.GetAwaiter();
        _writingSegment = frames;
        _pendingWrite.UnsafeOnCompleted(_writeComplete);
        return new ValueTask(this, _vts.Version);
    }

    void IValueTaskSource.GetResult(short token)
    {
        try
        {
            _vts.GetResult(token);
        }
        finally
        {
            _vts.Reset();
        }
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => _vts.GetStatus(token);

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _vts.OnCompleted(continuation, state, token, flags);
}
