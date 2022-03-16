using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Lite.Internal;

internal interface IListener
{
    bool IsClient { get; }
    IFrameConnection Connection { get; }
    
    bool TryCreateStream(in Frame initialize, [MaybeNullWhen(false)] out IStream handler);

    ConcurrentDictionary<ushort, IStream> Streams { get; }
}
internal static class ListenerEngine
{

    public async static Task RunAsync(this IListener listener, ILogger? logger, CancellationToken cancellationToken)
    {
        try
        {
            logger.Debug(listener, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) processing streams...");
            await using var iter = listener.Connection.GetAsyncEnumerator(cancellationToken);
            while (!cancellationToken.IsCancellationRequested && await iter.MoveNextAsync())
            {
                var frame = iter.Current;
                var header = frame.GetHeader();
                logger.Debug(frame, static (state, _) => $"received frame {state}");
                bool release = true;
                switch (header.Kind)
                {
                    case FrameKind.CloseConnection:
                    case FrameKind.Ping:
                        var generalFlags = (GeneralFlags)header.KindFlags;
                        if ((generalFlags & GeneralFlags.IsResponse) == 0)
                        {
                            // if this was a request, we reply in kind, but noting that it is a response
                            await listener.Connection.WriteAsync(new FrameHeader(header.Kind, (byte)GeneralFlags.IsResponse, header.StreamId, header.SequenceId), cancellationToken);
                        }
                        // shutdown if requested
                        if (header.Kind == FrameKind.CloseConnection)
                        {
                            listener.Connection.Close();
                        }
                        break;
                    case FrameKind.NewStream:
                        if (listener.Streams.ContainsKey(header.StreamId))
                        {
                            logger.Error(header.StreamId, static (state, _) => $"duplicate id! {state}");
                            await listener.Connection.WriteAsync(new FrameHeader(FrameKind.Cancel, 0, header.StreamId, 0), cancellationToken);
                        }
                        else if (listener.TryCreateStream(in frame, out var newStream) && newStream is not null)
                        {
                            //var method = ;
                            //var handler = _server.TryGetHandler(method, out var handlerFactory) ? handlerFactory() : null;
                            // handler.Initialize(header.StreamId, _connection, logger);
                            if (listener.Streams.TryAdd(header.StreamId, newStream))
                            {
                                logger.Debug(frame, static (state, _) => $"method accepted: {state.GetPayloadString()}");
                            }
                            else
                            {
                                logger.Error(header.StreamId, static (state, _) => $"duplicate id! {state}");
                                await listener.Connection.WriteAsync(new FrameHeader(FrameKind.Cancel, 0, header.StreamId, 0), cancellationToken);
                            }
                        }
                        else
                        {
                            logger.Debug(frame, static (state, _) => $"method not found: {state.GetPayloadString()}");
                            await listener.Connection.WriteAsync(new FrameHeader(FrameKind.MethodNotFound, 0, header.StreamId, 0), cancellationToken);
                        }
                        break;
                    default:
                        if (listener.Streams.TryGetValue(header.StreamId, out var existingStream) && existingStream is not null)
                        {
                            logger.Debug((handler: existingStream, frame: frame), static (state, _) => $"pushing {state.frame} to {state.handler.Method} ({state.handler.MethodType})");
                            if (existingStream.TryAcceptFrame(in frame))
                            {
                                release = false;
                            }
                            else
                            {
                                logger.Information(frame, static (state, _) => $"frame {state} rejected by handler");
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
                    logger.Information(frame.TotalLength, static (state, _) => $"releasing {state} bytes");
                    frame.Release();
                }
            }

            logger.Information(listener, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) exiting cleanly");
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        { } // alt-success
        catch (Exception ex)
        {
            logger.Error(ex);
            throw;
        }
        finally
        {
            logger.Information(listener, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) all done");
            if (listener is not null)
            {
                await listener.Connection.SafeDisposeAsync();
            }
        }
    }

}