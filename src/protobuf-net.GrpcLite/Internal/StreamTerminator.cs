using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class StreamTerminator : ITerminator
{
    private readonly Stream _input, _output;

    public StreamTerminator(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public ValueTask DisposeAsync() => Utilities.SafeDisposeAsync(_input, _output);

    public async IAsyncEnumerator<Frame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        byte[] headerBuffer = new byte[Frame.HeaderBytes];
        while (!cancellationToken.IsCancellationRequested)
        {
            int remaining = Frame.HeaderBytes, offset = 0, bytesRead;
            while (remaining > 0 && (bytesRead = await _input.ReadAsync(headerBuffer, offset, remaining, cancellationToken)) > 0)
            {
                remaining -= bytesRead;
                offset += bytesRead;
            }
            if (remaining == Frame.HeaderBytes) yield break; // clean EOF
            if (remaining != 0) ThrowEOF();

            var frame = Frame.UnsafeRead(ref headerBuffer[0]);

            if (frame.Length == 0)
            {
                yield return frame; // we're done
            }
            else
            {
                var dataBuffer = ArrayPool<byte>.Shared.Rent(frame.Length);
                remaining = frame.Length;
                offset = 0;
                while (remaining > 0 && (bytesRead = await _input.ReadAsync(dataBuffer, offset, remaining, cancellationToken)) > 0)
                {
                    remaining -= bytesRead;
                    offset += bytesRead;
                }
                if (remaining != 0) ThrowEOF();
                yield return new Frame(frame, dataBuffer, 0, FrameFlags.RecycleBuffer);
            }

        }
        static void ThrowEOF() => throw new EndOfStreamException();
    }

    public ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
