using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class StreamFrameConnection : IFrameConnection
{
    private readonly ILogger? _logger;
    private readonly Stream _duplex;
    private readonly bool _mergeWrites;

    public StreamFrameConnection(Stream duplex, bool mergeWrites = true, ILogger? logger = null)
    {
        _duplex = duplex.CheckDuplex();
        _logger = logger;
        _mergeWrites = mergeWrites;
    }

    public ValueTask DisposeAsync() => _duplex.SafeDisposeAsync();

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
    private bool AutoFlush()
    {
        if (_nonFlushedBytes != 0) // always flush when we've run out of sync work
        {
            _logger.Debug(_nonFlushedBytes, static (state, _) => $"Flushing {state} bytes (end of sync loop)...");
            _nonFlushedBytes = 0;
            return true;
        }
        return false;
    }

    public Task WriteAsync(ChannelReader<Frame> source, CancellationToken cancellationToken = default)
        => _mergeWrites ? WriteWithMergeAsync(source, cancellationToken) : WriteSimpleAsync(source, cancellationToken);

    private async Task WriteSimpleAsync(ChannelReader<Frame> source, CancellationToken cancellationToken)
    {
        do
        {
            while (source.TryRead(out var frame))
            {
                var memory = frame.Memory;
                _logger.Debug(memory, static (state, _) => $"Writing {state.Length} bytes...");
                await _duplex.WriteAsync(memory, cancellationToken);
                frame.Release();
                if (AutoFlush(memory.Length))
                    await _duplex.FlushAsync(cancellationToken);
            }

            if (AutoFlush())
                await _duplex.FlushAsync(cancellationToken);
            _logger.Debug($"Awaiting more work...");
        }
        while (await source.WaitToReadAsync(cancellationToken));
    }

    private async Task WriteWithMergeAsync(ChannelReader<Frame> source, CancellationToken cancellationToken)
    {
        [DoesNotReturn]
        static void ThrowMemoryManager() => throw new InvalidOperationException("Unable to get ref-counted memory manager");
        do
        {
            while (source.TryRead(out var frame))
            {
                var current = frame.Memory;
                if (!MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(current, out var currentManager, out var currentStart, out var currentLength))
                    ThrowMemoryManager();

                while (source.TryRead(out frame))
                {
                    var next = frame.Memory;
                    if (!MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(next, out var nextManager, out var nextStart, out var nextLength))
                        ThrowMemoryManager();
                    
                    if (ReferenceEquals(currentManager, nextManager) && nextStart == currentStart + currentLength)
                    {
                        // the data is contiguous; we can merge it to reduce writes
                        _logger.Debug((currentStart, currentLength, nextStart, nextLength), static (state, _) => $"Merging [{state.currentStart},{state.currentStart + state.currentLength}) and [{state.nextStart},{state.nextStart + state.nextLength}) into [{state.currentStart},{state.nextStart + state.nextLength})...");
                        current = currentManager.Memory.Slice(currentStart, currentLength + nextLength);
                        currentLength += nextLength;
                        currentManager.Dispose(); // account for the write that we've avoided
                        continue; // keep trying to merge (note that manager and start don't change)
                    }
                    else
                    {
                        // not contiguous; write the old block
                        _logger.Debug((currentStart, currentLength), static (state, _) => $"Writing [{state.currentStart},{state.currentStart + state.currentLength})...");
                        await _duplex.WriteAsync(current, cancellationToken);
                        currentManager!.Dispose();
                        if (AutoFlush(current.Length))
                            await _duplex.FlushAsync(cancellationToken);

                        // start trying to merge data from the new block
                        current = next;
                        currentManager = nextManager;
                        currentStart = nextStart;
                        currentLength = nextLength;
                    }
                }

                // write whatever we have left
                _logger.Debug((currentStart, currentLength), static (state, _) => $"Writing [{state.currentStart},{state.currentStart + state.currentLength})...");
                await _duplex.WriteAsync(current, cancellationToken);
                currentManager.Dispose();
                if (AutoFlush(current.Length))
                        await _duplex.FlushAsync(cancellationToken);
            }

            if (AutoFlush())
                await _duplex.FlushAsync(cancellationToken);
            _logger.Debug($"Awaiting more work...");
        }
        while (await source.WaitToReadAsync(cancellationToken));
    }
}
