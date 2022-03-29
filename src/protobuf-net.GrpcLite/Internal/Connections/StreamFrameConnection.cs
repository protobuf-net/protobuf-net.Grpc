using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class StreamFrameConnection : IFrameConnection
{
    private readonly ILogger? _logger;
    private readonly Stream _duplex;
    private readonly bool _mergeWrites;
    private readonly int _outputBufferSize;
    internal const int DefaultOutputBuffer = 1024;
    public StreamFrameConnection(Stream duplex, bool mergeWrites = false, int outputBufferSize = -1, ILogger? logger = null)
    {
        if (mergeWrites & outputBufferSize > 0) throw new ArgumentException($"When using {nameof(mergeWrites)}, {outputBufferSize} must not be specified.", nameof(outputBufferSize));
        _duplex = duplex.CheckDuplex();
        _logger = logger;
        _mergeWrites = mergeWrites;

        
        _outputBufferSize = outputBufferSize < 0 ? DefaultOutputBuffer : outputBufferSize; //-ve means "let chef decide"
    }

    public ValueTask DisposeAsync() => _duplex.SafeDisposeAsync();

    public async IAsyncEnumerator<Frame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var builder = Frame.CreateBuilder(logger: _logger);
        try
        {
            int bytesRead;
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.Debug(builder.GetBuffer(), static (state, _) => $"reading up-to {state.Length} bytes from stream...");
                try
                {
                    bytesRead = await _duplex.ReadAsync(builder.GetBuffer(), cancellationToken);
                    if (bytesRead <= 0) break; // natural EOF
                }
                catch (IOException ex)
                {   // treat as EOF (we'll check for incomplete reads below)
                    _logger.Debug(ex.Message);
                    break;
                }
                _logger.Debug((builder, bytesRead), static (state, _) => $"read {state.bytesRead} bytes from stream; parsing {state.builder.GetBuffer().Slice(start: 0, length: state.bytesRead).ToHex()}");
                while (builder.TryRead(ref bytesRead, out var frame))
                {
                    _logger.Debug((frame, builder, bytesRead), static (state, _) => $"parsed {state.frame}; {state.bytesRead} remaining");
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

    Task IFrameConnection.WriteAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken)
        => _mergeWrites ? WriteWithMergeAsync(source, cancellationToken) :
           // WriteDirectAsync(source, cancellationToken);
    _outputBufferSize > 0 ? WriteWithOutputBufferAsync(source, cancellationToken) :
           WriteDirectAsync(source, cancellationToken);

    private async Task WriteDirectAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken)
    {
        try
        {
            do
            {
                bool needsFlush = false;
                while (true)
                {
                    if (!source.TryRead(out var pair))
                    {
                        break;
                        //await Task.Yield(); // blink; see if things improved
                        //if (!source.TryRead(out frame)) break; // nope, definitely nothing there
                    }

                    _logger.Debug(pair.Frame, static (state, _) => $"Dequeued {state} for writing");
                    var memory = pair.Frame.Memory;
                    if (memory.IsEmpty)
                    {
                        Debug.Assert(false, "empty frame!");
                    }
                    else
                    {
                        await WriteAsync(memory, "frame", cancellationToken);
                        if ((pair.Flags & FrameWriteFlags.BufferHint) == 0) needsFlush = true;
                        pair.Frame.Release();
                    }
                }

                if (needsFlush)
                {
                    _logger.Debug("Flushing...");
                    await _duplex.FlushAsync(cancellationToken);
                }
                _logger.Debug("Awaiting more work...");
            }
            while (await source.WaitToReadAsync(cancellationToken));
            _logger.Debug("Exiting write-loop due to end of data");
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        {
            _logger.Debug("Exiting write-loop due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
    }

    private async Task WriteWithOutputBufferAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken)
    {
        Memory<byte> bufferMem = default;
        try
        {
            int buffered = 0, capacity = _outputBufferSize;
            do
            {
                bool needFlush = false;
                while (true) // try to read synchronously
                {
                    if (!source.TryRead(out var pair))
                    {
                        break;
                        //await Task.Yield(); // blink; see if things improved
                        //if (!source.TryRead(out frame)) break; // nope, definitely nothing there
                    }

                    _logger.Debug(pair.Frame, static (state, _) => $"Dequeued {state} for writing");
                    var inbound = pair.Frame.Memory;
                    int inboundLength = inbound.Length;
                    
                    // scenarios:
                    // A: nothing buffered, inbound doesn't fit: just write inbound directly
                    // B: (something or nothing buffered); inbound fits into buffer: buffer it (write if no remaining capacity after buffering) 
                    // C: something buffered, inbound doesn't fit:
                    //    fill the existing buffer and write
                    //    D: remainder fits into next buffer: buffer the remainder
                    //    E: otherwise, send the remainder
                    _logger.Debug((inbound, capacity), static (state, _) => $"Considering {state.inbound.Length} bytes (buffer capacity: {state.capacity}): {state.inbound.ToHex()}");
                    if (inboundLength == 0)
                    {
                        Debug.Assert(false, "empty frame!");
                        continue;
                    }

                    if ((pair.Flags & FrameWriteFlags.BufferHint) == 0) needFlush = true;

                    if (buffered == 0 && inboundLength >= capacity)
                    {
                        // scenario A
                        await WriteAsync(inbound, "frame (A)", cancellationToken);
                    }
                    else
                    {
                        if (bufferMem.IsEmpty) // all of B/C/D require a buffer
                        {
                            bufferMem = ArrayPool<byte>.Shared.Rent(_outputBufferSize);
                            capacity = bufferMem.Length;
                        }
                        if (inboundLength <= capacity)
                        {
                            // scenario B
                            Debug.Assert(buffered + capacity == bufferMem.Length, "tracking mismatch!");
                            inbound.CopyTo(bufferMem.Slice(start: buffered));
                            capacity -= inboundLength;
                            buffered += inboundLength;
                            _logger.Debug((capacity, buffered), static (state, _) => $"scenario B; buffered: {state.buffered}, capacity: {state.capacity}");
                            if (capacity == 0) // all full up
                            {
                                _logger.Debug(bufferMem, static (state, _) => $"(B2) Writing {state.Length} bytes from buffer: {state.ToHex()}");
                                await WriteAsync(bufferMem, "buffer", cancellationToken);
                                capacity = buffered;
                                buffered = 0;
                            }
                        }
                        else
                        {
                            // scenario C
                            Debug.Assert(buffered > 0, "we expect a partial buffer");
                            _logger.Debug("scenario C");
                            inbound.Slice(start: 0, length: capacity).CopyTo(bufferMem.Slice(start: buffered));
                            var remaining = inbound.Slice(start: capacity);
                            buffered += capacity;
                            Debug.Assert(buffered == bufferMem.Length, "we expect to have filled the buffer");
                            await WriteAsync(bufferMem, "buffer (C)", cancellationToken);
                            capacity = buffered;
                            buffered = 0;

                            if (remaining.Length < capacity)
                            {
                                // scenario D
                                _logger.Debug("scenario D");
                                remaining.CopyTo(bufferMem);
                                buffered = remaining.Length;
                                capacity -= buffered;
                            }
                            else
                            {
                                // scenario E
                                await WriteAsync(remaining, "remaining inbound (E)", cancellationToken);
                            }
                        }
                    }
                    pair.Frame.Release();
                }

                // no remaining synchronous work

                // write any remaining buffered data
                if (needFlush) // note that bufferMem is preserved between writes if not flushing
                {
                    if (buffered != 0)
                    {
                        await WriteAsync(bufferMem.Slice(start: 0, length: buffered), "buffer (F)", cancellationToken);
                        capacity += buffered;
                        buffered = 0;
                    }
                    if (!bufferMem.IsEmpty) // note that we need to do this even if buffered == 0, if last op was scenario E
                    {
                        Return(ref bufferMem, _logger);
                        capacity = _outputBufferSize;
                    }

                    _logger.Debug("Flushing...");
                    await _duplex.FlushAsync(cancellationToken);
                }
                _logger.Debug("Awaiting more work...");
            }
            while (await source.WaitToReadAsync(cancellationToken));
            _logger.Debug("Exiting write-loop due to end of data");
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        {
            _logger.Debug("Exiting write-loop due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
        finally
        {
            Return(ref bufferMem, _logger);
        }

        static void Return(ref Memory<byte> buffer, ILogger? logger, [CallerLineNumber] int lineNumber = 0)
        {
            if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment) && segment.Array is not null)
            {
                logger.Debug((segment, lineNumber), static (state, _) => $"Returning {state.segment.Count} bytes to array-pool (from L{state.lineNumber})");
                ArrayPool<byte>.Shared.Return(segment.Array);
            }
            buffer = default;
        }
    }
    ValueTask WriteAsync(ReadOnlyMemory<byte> value, string source, CancellationToken cancellationToken)
    {
        Debug.Assert(!value.IsEmpty, "empty write");
        _logger.Debug((value, source), static (state, _) => $"Writing {state.value.Length} bytes from {state.source}: {state.value.ToHex()}");
        return _duplex.WriteAsync(value, cancellationToken);
    }

    private async Task WriteWithMergeAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken)
    {
        [DoesNotReturn]
        static void ThrowMemoryManager() => throw new InvalidOperationException("Unable to get ref-counted memory manager");

        try
        {
            do
            {
                bool needFlush = false;
                while (true)
                {
                    if (!source.TryRead(out var pair))
                    {
                        break;
                        //await Task.Yield(); // blink; see if things improved
                        //if (!source.TryRead(out frame)) break; // nope, definitely nothing there
                    }
                    if ((pair.Flags & FrameWriteFlags.BufferHint) == 0) needFlush = true;

                    _logger.Debug(pair.Frame, static (state, _) => $"Dequeued {state} for writing");
                    var current = pair.Frame.Memory;
                    if (current.IsEmpty)
                    {
                        Debug.Assert(false, "empty frame!");
                        continue;
                    }
                    if (!MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(current, out var currentManager, out var currentStart, out var currentLength))
                        ThrowMemoryManager();

                    while (source.TryRead(out pair))
                    {
                        if ((pair.Flags & FrameWriteFlags.BufferHint) == 0) needFlush = true;
                        var next = pair.Frame.Memory;
                        
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
                }

                if (needFlush)
                {
                    _logger.Debug("Flushing...");
                    await _duplex.FlushAsync(cancellationToken);
                }
                _logger.Debug("Awaiting more work...");
            }
            while (await source.WaitToReadAsync(cancellationToken));
            _logger.Debug("Exiting write-loop due to end of data");
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        {
            _logger.Debug("Exiting write-loop due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
    }
}
