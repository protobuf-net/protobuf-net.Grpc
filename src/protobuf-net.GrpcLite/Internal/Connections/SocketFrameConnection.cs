using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class SocketFrameConnection : IFrameConnection
{
    private readonly Socket _socket;
    private readonly ILogger? _logger;
    private readonly int _outputBufferSize;

    public SocketFrameConnection(Socket socket, int outputBufferSize, ILogger? logger)
    {
        _socket = socket;
        _logger = logger;
        if (outputBufferSize <= 0) outputBufferSize = StreamFrameConnection.DefaultOutputBuffer;
        _outputBufferSize = outputBufferSize;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _socket.SafeDispose();
        return default;
    }


    static CancellationTokenRegistration RegisterForCancellation(SocketAwaitableEventArgs args, CancellationToken cancellationToken)
        => cancellationToken.Register(static state => ((SocketAwaitableEventArgs) state!).Abort(), args);

    static void SetBuffer(SocketAwaitableEventArgs args, Memory<byte> value)
    {
#if NET472
        if (!MemoryMarshal.TryGetArray<byte>(value, out var segment)) ThrowNeedArray();
        args.SetBuffer(segment.Array, segment.Offset, segment.Count);
        static void ThrowNeedArray() => throw new NotSupportedException("The underlying buffer array could not be retrieved");
#else
        args.SetBuffer(value);
#endif
    }
    async IAsyncEnumerator<Frame> IAsyncEnumerable<Frame>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        var builder = Frame.CreateBuilder(logger: _logger);
        var readArgs = new SocketAwaitableEventArgs();
        CancellationTokenRegistration ctr = RegisterForCancellation(readArgs, cancellationToken);
        try
        {
            int bytesRead;
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.Debug(builder.GetBuffer(), static (state, _) => $"reading up-to {state.Length} bytes from stream...");
                try
                {
                    SetBuffer(readArgs, builder.GetBuffer());
                    bool isAsync = _socket.ReceiveAsync(readArgs);
                    await readArgs;
                    bytesRead = readArgs.BytesTransferred;
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
            ctr.SafeDispose();
        }
    }

    async Task IFrameConnection.WriteAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken)
    {
        Memory<byte> bufferMem = default;

        var writeArgs = new SocketAwaitableEventArgs();
        CancellationTokenRegistration ctr = RegisterForCancellation(writeArgs, cancellationToken);
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
                        await WriteAsync(writeArgs, MemoryMarshal.AsMemory(inbound), "frame (A)");
                        Debug.Assert(writeArgs.BytesTransferred == inbound.Length, "incomplete write (A)");
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
                                await WriteAsync(writeArgs, bufferMem, "buffer (B2)");
                                Debug.Assert(writeArgs.BytesTransferred == bufferMem.Length, "incomplete write (B2)");
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
                            await WriteAsync(writeArgs, bufferMem, "buffer (C)");
                            Debug.Assert(writeArgs.BytesTransferred == bufferMem.Length, "incomplete write (C)");
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
                                await WriteAsync(writeArgs, MemoryMarshal.AsMemory(remaining), "remaining inbound (E)");
                                Debug.Assert(writeArgs.BytesTransferred == remaining.Length, "incomplete write (E)");
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
                        await WriteAsync(writeArgs, bufferMem.Slice(start: 0, length: buffered), "buffer (F)");
                        Debug.Assert(writeArgs.BytesTransferred == buffered, "incomplete write (F)");
                        capacity += buffered;
                        buffered = 0;
                    }
                    if (!bufferMem.IsEmpty) // note that we need to do this even if buffered == 0, if last op was scenario E
                    {
                        Return(ref bufferMem, _logger);
                        capacity = _outputBufferSize;
                    }
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
            ctr.SafeDispose();
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

        SocketAwaitableEventArgs WriteAsync(SocketAwaitableEventArgs writeArgs, Memory<byte> value, string source)
        {
            Debug.Assert(!value.IsEmpty, "empty write");
            _logger.Debug((value, source), static (state, _) => $"Writing {state.value.Length} bytes from {state.source}: {state.value.ToHex()}");
            SetBuffer(writeArgs, value);
            bool isAsync = _socket.SendAsync(writeArgs);
            _logger.Debug(isAsync, static (state, _) => $"Wrote async: {state}");
            return writeArgs;
        }
    }
}
