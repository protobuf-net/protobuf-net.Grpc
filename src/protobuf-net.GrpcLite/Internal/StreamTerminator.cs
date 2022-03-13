using ProtoBuf.Grpc.Lite.Connections;
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
        => new ValueTask(_output.WriteAsync(frames.Array, frames.Offset, frames.Length, cancellationToken));
}
