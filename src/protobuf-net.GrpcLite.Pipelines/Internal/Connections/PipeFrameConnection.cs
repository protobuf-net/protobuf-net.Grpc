using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.IO.Pipelines;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class PipeFrameConnection : IFrameConnection
{
    private readonly IDuplexPipe _pipe;
    private readonly ILogger? _logger;

    public PipeFrameConnection(IDuplexPipe pipe, ILogger? logger = null)
    {
        _pipe = pipe;
        _logger = logger;
    }

    private void Close(Exception? exception)
    {
        try { _pipe.Input.Complete(exception); } catch { }
        try { _pipe.Output.Complete(exception); } catch { }
    }
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Close(null);
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

                    if (result.IsCanceled) ThrowCancelled(nameof(PipeReader.ReadAtLeastAsync), cancellationToken);
                }
                catch (Exception ex)
                {
                    Close(ex);
                    _logger.Error(ex);
                    throw;
                }
                var buffer = result.Buffer;
                _logger.Debug(buffer, static (state, _) => $"pipe reader provided {state.Length} bytes; parsing {state.ToHex()}");
                while (builder.TryRead(ref buffer, out var frame))
                {
                    yield return frame;
                }
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
            _logger.Debug("pipe reader exiting");
            Close(null);
        }

        static void ThrowEOF() => throw new EndOfStreamException();
    }

    private ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        var pending = _pipe.Output.FlushAsync(cancellationToken);
        if (pending.IsCompletedSuccessfully)
        {
            CheckFlush(pending.Result, cancellationToken);
            return default;
        }
        return Awaited(pending, cancellationToken);

        async static ValueTask Awaited(ValueTask<FlushResult> pending, CancellationToken cancellationToken)
            => CheckFlush(await pending, cancellationToken);

        static void CheckFlush(FlushResult result, CancellationToken cancellationToken)
        {
            if (result.IsCanceled) ThrowCancelled(nameof(PipeWriter.FlushAsync), cancellationToken);
            if (result.IsCompleted) ThrowCompleted();
        }
        static void ThrowCompleted() => throw new InvalidOperationException("Pipe: the consumer is completed");
    }
    static void ThrowCancelled(string name, CancellationToken cancellationToken) => throw new OperationCanceledException($"Pipe: '{name}' operation was cancelled", cancellationToken);


    public async Task WriteAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken = default)
    {
        try
        {
            do
            {
                bool needsFlush = false;
                while (source.TryRead(out var pair))
                {
                    if ((pair.Flags & FrameWriteFlags.BufferHint) == 0) needsFlush = true;
                    var memory = pair.Frame.Memory;
                    _logger.Debug(memory, static (state, _) => $"Writing {state.Length} bytes: {state.ToHex()}");
                    _pipe.Output.Write(memory.Span);
                    pair.Frame.Release();
                    if (AutoFlush(memory.Length))
                    {
                        await FlushAsync(cancellationToken);
                        needsFlush = false;
                    }
                }

                if (needsFlush)
                {
                    await FlushAsync(cancellationToken);
                }
                _logger.Debug($"Awaiting more work...");
            }
            while (await source.WaitToReadAsync(cancellationToken));
            Close(null);
        }
        catch (Exception ex)
        {
            Close(ex);
            _logger.Error(ex);
            throw;
        }
    }

    private int _nonFlushedBytes;
    const int FLUSH_EVERY_BYTES = 8 * 1024;
    private bool AutoFlush(int bytes)
    {
        _nonFlushedBytes += bytes;
        if (_nonFlushedBytes >= FLUSH_EVERY_BYTES)
        {
            _logger.Debug(_nonFlushedBytes, static (state, _) => $"Auto-flushing {state} bytes");
            _nonFlushedBytes = 0;
            return true;
        }
        return false;
    }
}
