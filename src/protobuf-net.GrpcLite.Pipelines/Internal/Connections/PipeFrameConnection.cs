using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.IO.Pipelines;
using System.Buffers;
using System.Diagnostics;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

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
        _logger.Debug(this, static (state, _) => $"pipe reader starting");
        var builder = Frame.CreateBuilder();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result;
                try
                {
                    _logger.Debug(builder.RequestBytes, static (state, _) => $"pipe reader requesting {state} bytes...");
                    result = await _pipe.Input.ReadAtLeastAsync(builder.RequestBytes, cancellationToken);

                    if (result.IsCanceled) ThrowCancelled(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    throw;
                }
                var buffer = result.Buffer;
                _logger.Debug(buffer, static (state, _) => $"pipe reader provided {state.Length} bytes; parsing...");
                bool readFrame;
                do
                {
                    Frame frame;
                    try
                    {
                        readFrame = builder.TryRead(ref buffer, out frame);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                        throw;
                    }
                    yield return frame; // a lot of mess above simply because we can't 'yield' inside a 'try' with a 'catch'
                }
                while (readFrame);

                _pipe.Input.AdvanceTo(buffer.Start, buffer.End);
                Debug.Assert(buffer.IsEmpty, "we expect to consume the entire buffer"); // because we can't trust the pipe's allocator :(
                if (result.IsCompleted)
                {
                    if (builder.InProgress) ThrowEOF();
                    break; // exit main while
                }
            }
        }
        finally
        {
            builder.Release();
            _logger.Debug(this, static (state, _) => $"pipe reader exiting");
            Close();
        }

        static void ThrowEOF() => throw new EndOfStreamException();
    }

    private const int AUTO_FLUSH_EVERY_BYTES = 8 * 1024;
    private ValueTask WriteCoreAsync(Frame frame, CancellationToken cancellationToken)
    {
        var length = frame.TotalLength;
        _logger.Debug(length, static (state, _) => $"pipe writer writing {state} ({state} bytes, releasing)...");
        _pipe.Output.Write(frame.Memory.Span);
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
