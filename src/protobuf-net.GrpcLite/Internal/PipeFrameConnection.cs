using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.IO.Pipelines;
using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class PipeFrameConnection : IRawFrameConnection
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
            _logger.Debug(this, static (state, _) => $"pipe reader starting");
            int requestNextTime = FrameHeader.Size;
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.Debug(requestNextTime, static (state, _) => $"pipe reader requesting {state} bytes...");
                var result = await _pipe.Input.ReadAtLeastAsync(requestNextTime, cancellationToken);

                var buffer = result.Buffer;
                if (result.IsCanceled) ThrowCancelled(cancellationToken);
                if (buffer.Length < FrameHeader.Size && result.IsCompleted)
                {
                    if (buffer.IsEmpty) yield break; // natural EOF
                    ThrowEOF();
                }
                _logger.Debug(buffer, static (state, _) => $"pipe reader provided {state.Length} bytes; parsing...");
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
                        _logger.Error(ex);
                        Close(ex);
                        throw;
                    }
                    if (readFrame)
                    {
                        _logger.Debug(frame, (state, _) => $"yielding {state}...");
                        yield return frame;
                    }
                } while (readFrame);

                _pipe.Input.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        finally
        {
            _logger.Debug(this, static (state, _) => $"pipe reader exiting");
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
            _logger.Error(ex);
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

    private const int AUTO_FLUSH_EVERY_BYTES = 8 * 1024;
    private ValueTask WriteCoreAsync(Frame frame, CancellationToken cancellationToken)
    {
        var length = frame.TotalLength;
        _logger.Debug(length, static (state, _) => $"pipe writer writing {state} ({state} bytes, releasing)...");
        _pipe.Output.Write(frame.RawBuffer.Span);
        frame.Release();

        _outstandingBytes += length;
        
        return _outstandingBytes < AUTO_FLUSH_EVERY_BYTES ? default : FlushAsync(cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> frames, CancellationToken cancellationToken)
    {
        var length = frames.Length;
        if (length == 0) return default;
        
        _logger.Debug(length, static (state, _) => $"pipe writer writing {state} ({state} bytes)...");
        _pipe.Output.Write(frames.Span);

        _outstandingBytes += length;
        return _outstandingBytes < AUTO_FLUSH_EVERY_BYTES ? default : FlushAsync(cancellationToken);
    }

    public ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
        => WriteCoreAsync(frame, cancellationToken);

    private int _outstandingBytes;

    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        _logger.Debug(_outstandingBytes, static (state, _) => $"pipe writer flushing {state} bytes...");
        _outstandingBytes = 0;
        var pending = _pipe.Output.FlushAsync(cancellationToken);
        if (pending.IsCompletedSuccessfully)
        {
            CheckFlush(pending.Result, cancellationToken);
            return default;
        }

        return Awaited(pending, _logger, cancellationToken);

        async static ValueTask Awaited(ValueTask<FlushResult> pending, ILogger? logger, CancellationToken cancellationToken)
            => CheckFlush(await pending, cancellationToken);
        static void CheckFlush(FlushResult result, CancellationToken cancellationToken)
        {
            if (result.IsCanceled) ThrowCancelled(cancellationToken);
            if (result.IsCompleted) throw new InvalidOperationException("Pipe: the consumer is completed");
        }
    }

    static void ThrowCancelled(CancellationToken cancellationToken)
        => throw new OperationCanceledException("Pipe: flush was cancelled", cancellationToken);

    ValueTask IFrameConnection.WriteAsync(ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken)
    {
        return frames.Length switch
        {
            0 => default,
            1 => WriteAsync(frames.Span[0], cancellationToken),
            _ => SlowAsync(this, frames, cancellationToken),
        };
        async static ValueTask SlowAsync(PipeFrameConnection obj, ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken)
        {
            var length = frames.Length;
            for (int i = 0; i < length; i++)
            {
                await obj.WriteCoreAsync(frames.Span[i], cancellationToken);
            }
        }
    }
}
