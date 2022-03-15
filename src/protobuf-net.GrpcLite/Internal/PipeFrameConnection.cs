using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.IO.Pipelines;
using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class PipeFrameConnection : IFrameConnection
{
    private readonly IDuplexPipe _pipe;
    private readonly ILogger? _logger;

    public PipeFrameConnection(IDuplexPipe pipe, ILogger? logger = null)
    {
        _pipe = pipe;
        _logger = logger;
    }
    bool IFrameConnection.ThreadSafeWrite => false;
    object? _completion;

    Task IFrameConnection.Complete => Utilities.GetLazyCompletion(ref _completion, false);

    public void Close(Exception? exception = null)
    {
        _ = Utilities.GetLazyCompletion(ref _completion, true);
        try { _pipe.Input.Complete(exception); } catch { }
        try { _pipe.Output.Complete(exception); } catch { }
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Close();
        return default;
    }

    async IAsyncEnumerator<Frame> IAsyncEnumerable<Frame>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(this, static (state, _) => $"pipe reader starting");
            int requestNextTime = FrameHeader.Size;
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(requestNextTime, static (state, _) => $"pipe reader requesting {state} bytes...");
                var result = await _pipe.Input.ReadAtLeastAsync(requestNextTime, cancellationToken);

                var buffer = result.Buffer;
                if (result.IsCanceled) ThrowCancelled(cancellationToken);
                if (buffer.Length < FrameHeader.Size && result.IsCompleted)
                {
                    if (buffer.IsEmpty) yield break; // natural EOF
                    ThrowEOF();
                }
                _logger.LogDebug(buffer, static (state, _) => $"pipe reader provided {state.Length} bytes; parsing...");
                bool readFrame = true;
                do
                {
                    Frame frame;
                    try
                    {   // some code gynmastics here so we can get exception logging 
                        readFrame = TryReadFrame(ref buffer, out frame, out requestNextTime);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex);
                        Close(ex);
                        throw;
                    }
                    if (readFrame)
                    {
                        _logger.LogDebug(frame, (state, _) => $"yielding {state}...");
                        yield return frame;
                    }
                } while (readFrame);

                _pipe.Input.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        finally
        {
            _logger.LogDebug(this, static (state, _) => $"pipe reader exiting");
            Close();
        }

        static void ThrowEOF() => throw new EndOfStreamException();
    }

    private bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out Frame frame, out int requestNextTime)
    {
        FrameBufferManager.Slab? slab = null;
        try
        {
            FrameHeader header;
            if (!(TryReadHeader(buffer.First.Span, out header) || TryReadHeader(in buffer, out header)))
            {
                frame = default;
                requestNextTime = FrameHeader.Size;
                return false;
            }
            Frame.AssertValidLength(header.PayloadLength);
            var totalLength = FrameHeader.Size + header.PayloadLength;
            if (buffer.Length < totalLength)
            {
                frame = default;
                requestNextTime = totalLength;
                return false;
            }
            // rent a buffer and get the right-sized chunk
            slab = FrameBufferManager.Shared.Rent(header.PayloadLength);
            var payload = slab.ActiveBuffer.Slice(0, header.PayloadLength);

            if (header.PayloadLength != 0)
            {
                // copy the payload portion of the data
                buffer.Slice(start: FrameHeader.Size, length: header.PayloadLength).CopyTo(payload.Span);
                slab.Advance(header.PayloadLength);
            }
            frame = slab.CreateFrameAndInvalidate(header, updateHeaderLength: false);
            buffer = buffer.Slice(start: totalLength);
            requestNextTime = FrameHeader.Size;
            return true;
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

    }

    static bool TryReadHeader(ReadOnlySpan<byte> span, out FrameHeader header)
    {
        if (span.Length >= FrameHeader.Size)
        {
            header = FrameHeader.ReadUnsafe(in span[0]);
            return true;
        }
        header = default;
        return false;
    }
    static bool TryReadHeader(in ReadOnlySequence<byte> buffer, out FrameHeader header)
    {
        if (buffer.Length >= FrameHeader.Size)
        {
            Span<byte> span = stackalloc byte[FrameHeader.Size];
            buffer.Slice(0, 8).CopyTo(span);
            header = FrameHeader.ReadUnsafe(in span[0]);
            return true;
        }
        header = default;
        return false;
    }

    public ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
    {
        var pending = _pipe.Output.WriteAsync(frame.RawBuffer, cancellationToken);
        if (pending.IsCompletedSuccessfully)
        {
            _logger.LogDebug(frame, static (state, _) => $"pipe writer wrote {state} ({state.TotalLength} bytes) synchronously");
            CheckFlush(pending.Result, cancellationToken);
            return default;
        }
        _logger.LogDebug(frame, static (state, _) => $"pipe writer writing {state} ({state.TotalLength} bytes) asynchronously...");
        return Awaited(pending, cancellationToken);

        async static ValueTask Awaited(ValueTask<FlushResult> pending, CancellationToken cancellationToken)
            => CheckFlush(await pending, cancellationToken);
    }

    static void ThrowCancelled(CancellationToken cancellationToken)
        => throw new OperationCanceledException("Pipe: flush was cancelled", cancellationToken);
    static void CheckFlush(FlushResult result, CancellationToken cancellationToken)
    {
        if (result.IsCanceled) ThrowCancelled(cancellationToken);
        if (result.IsCompleted) throw new InvalidOperationException("Pipe: the consumer is completed");
    }

    ValueTask IFrameConnection.WriteAsync(ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken)
    {
        return frames.Length switch
        {
            0 => default,
            1 => WriteAsync(frames.Span[0], cancellationToken),
            _ => SlowAsync(_pipe.Output, frames, cancellationToken, _logger),
        };
        async static ValueTask SlowAsync(PipeWriter writer, ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken, ILogger? logger)
        {
            var length = frames.Length;
            const int FLUSH_EVERY = 8 * 1024;
            int nonFlushed = 0;
            for (int i = 0; i < length; i++)
            {
                var buffer = frames.Span[i].RawBuffer;
                logger.LogDebug(frames.Span[i], static (state, _) => $"pipe writer writing {state} ({state.TotalLength} bytes)...");
                writer.Write(buffer.Span);
                nonFlushed += buffer.Length;
                if (nonFlushed >= FLUSH_EVERY)
                {
                    logger.LogDebug(nonFlushed, static (state, _) => $"pipe writer flushing {state} bytes...");
                    CheckFlush(await writer.FlushAsync(cancellationToken), cancellationToken);
                    nonFlushed = 0;
                }
            }
            if (nonFlushed != 0)
            {
                logger.LogDebug(nonFlushed, static (state, _) => $"pipe writer flushing {state} bytes...");
                CheckFlush(await writer.FlushAsync(cancellationToken), cancellationToken);
            }
        }
    }
}
