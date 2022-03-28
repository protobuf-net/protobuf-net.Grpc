using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal;

internal interface IConnection
{
    bool IsClient { get; }
    ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> Output { get; }
    IAsyncEnumerable<Frame> Input { get; }
    bool TryCreateStream(in Frame initialize, ReadOnlyMemory<char> route, [MaybeNullWhen(false)] out IStream stream);

    ConcurrentDictionary<ushort, IStream> Streams { get; }
    void Remove(ushort streamId);
    CancellationToken Shutdown { get; }

    void Close(Exception? fault);
    RefCountedMemoryPool<byte> Pool { get; }

    string LastKnownUserAgent { get; set; }
}
internal static class ListenerEngine
{
    public async static Task RunAsync(this IConnection connection, ILogger? logger, CancellationToken cancellationToken)
    {
        static ValueTask WriteAsync(IConnection connection, FrameKind kind, ushort streamId, ushort sequenceId, CancellationToken cancellationToken)
            => connection.Output.WriteAsync((Frame.CreateFrame(connection.Pool, new FrameHeader(kind, 0, streamId, sequenceId)), FrameWriteFlags.None), cancellationToken);

        Frame frame = default;
        try
        {
            logger.Debug(connection, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) processing streams...");
            await using var iter = connection.Input.GetAsyncEnumerator(cancellationToken);
            while (!cancellationToken.IsCancellationRequested && await iter.MoveNextAsync())
            {
                frame = iter.Current;
                var header = frame.GetHeader();
                logger.Debug(frame, static (state, _) => $"received frame {state}");
                bool release = true;
                switch (header.Kind)
                {
                    case FrameKind.None:
                        logger.Debug(frame, static (state, _) => $"invalid frame {state} received");
                        break;
                    case FrameKind.ConnectionClose:
                    case FrameKind.ConnectionPing:
                        if (header.IsClientStream != connection.IsClient)
                        {
                            // the other end is initiating; acknowledge with an empty but similar frame
                            await WriteAsync(connection, header.Kind, header.StreamId, header.SequenceId, cancellationToken);
                        }
                        // shutdown if requested
                        if (header.Kind == FrameKind.ConnectionClose)
                        {
                            connection.Output.Complete();
                        }
                        break;
                    case FrameKind.StreamHeader when header.IsClientStream != connection.IsClient: // a header with the "other" stream marker means
                        if (connection.Streams.ContainsKey(header.StreamId))
                        {
                            logger.Error(header.StreamId, static (state, _) => $"duplicate id! {state}");
                            await WriteAsync(connection, FrameKind.StreamCancel, header.StreamId, 0, cancellationToken);
                        }
                        else
                        {
                            ArraySegment<char> route = MetadataEncoder.GetRouteBuffer(frame.GetPayload());
                            PayloadFrameSerializationContext? ctx = null;
                            try
                            {
                                if (connection.TryCreateStream(in frame, new ReadOnlyMemory<char>(route.Array, route.Offset, route.Count), out var newStream) && newStream is not null)
                                {
                                    if (connection.Streams.TryAdd(header.StreamId, newStream))
                                    {
                                        if (newStream.TryAcceptFrame(frame))
                                        {
                                            logger.Debug(route, static (state, _) => $"method accepted: {state.CreateString()}");
                                            release = false;
                                        }
                                        else
                                        {
                                            logger.Debug(route, static (state, _) => $"method resolved, but initial frame rejected: {state.CreateString()}");
                                            connection.Remove(header.StreamId);
                                            ctx = PayloadFrameSerializationContext.Get(header.StreamId, connection.Pool, FrameKind.StreamTrailer);
                                            MetadataEncoder.WriteStatus(ctx, StatusCode.Internal, "Initial frame rejected".AsSpan());
                                            ctx.Complete();
                                            await ctx.WritePayloadAsync(connection.Output, FrameWriteFlags.None, cancellationToken);
                                        }
                                    }
                                    else
                                    {
                                        logger.Error(header.StreamId, static (state, _) => $"duplicate id! {state}");
                                        ctx = PayloadFrameSerializationContext.Get(header.StreamId, connection.Pool, FrameKind.StreamTrailer);
                                        MetadataEncoder.WriteStatus(ctx, StatusCode.AlreadyExists, "Specified stream already exists".AsSpan());
                                        ctx.Complete();
                                        await ctx.WritePayloadAsync(connection.Output, FrameWriteFlags.None, cancellationToken);
                                    }
                                }
                                else
                                {
                                    logger.Debug(route, static (state, _) => $"method not found: {state.CreateString()}");
                                    ctx = PayloadFrameSerializationContext.Get(header.StreamId, connection.Pool, FrameKind.StreamTrailer);
                                    MetadataEncoder.WriteStatus(ctx, StatusCode.NotFound, route.AsSpan());
                                    ctx.Complete();
                                    await ctx.WritePayloadAsync(connection.Output, FrameWriteFlags.None, cancellationToken);
                                }
                            }
                            finally
                            {
                                if (route.Array is not null)
                                    ArrayPool<char>.Shared.Return(route.Array);
                                ctx?.Recycle();
                            }
                        }
                        break;
                    default:
                        if (connection.Streams.TryGetValue(header.StreamId, out var existingStream) && existingStream is not null)
                        {
                            if (!existingStream.IsActive)
                            {
                                // shouldn't still be here, but; fix that
                                connection.Streams.TryRemove(header.StreamId, out _);
                            }
                            if (header.Kind == FrameKind.StreamCancel)
                            {
                                // kill it
                                connection.Streams.TryRemove(header.StreamId, out _);
                                existingStream.Cancel();
                            }
                            else
                            {
                                logger.Debug((stream: existingStream, frame: frame), static (state, _) => $"pushing {state.frame} to {state.stream.Method} ({state.stream.MethodType})");
                                if (existingStream.TryAcceptFrame(in frame))
                                {
                                    release = false;
                                }
                                else
                                {
                                    logger.Information(frame, static (state, _) => $"frame {state} rejected by stream");
                                }

                                if ((header.Kind == FrameKind.StreamTrailer && header.IsFinal))
                                {
                                    logger.Debug(header, static (state, _) => $"removing stream {state}");
                                    connection.Streams.TryRemove(header.StreamId, out _);
                                }
                            }
                        }
                        else
                        {
                            logger.Information(frame, static (state, _) => $"received frame for unknown stream {state}");
                        }
                        break;
                }
                if (release)
                {
                    logger.Debug(frame.TotalLength, static (state, _) => $"releasing {state} bytes");
                    frame.Release();
                    frame = default;
                }
            }

            logger.Debug(connection, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) exiting cleanly");
            connection.Output.Complete(null);
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        { } // alt-success
        catch (Exception ex)
        {
            logger.Error(frame, static (state, ex) => $"Error processing {state}: {ex?.Message}");
            connection?.Output.Complete(ex);
            throw;
        }
        finally
        {
            logger.Information(connection, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) all done");
        }
    }

}