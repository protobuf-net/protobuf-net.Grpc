using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Lite.Internal;

internal interface IListener
{
    bool IsClient { get; }
    IFrameConnection Connection { get; }
    
    bool TryCreateStream(in Frame initialize, [MaybeNullWhen(false)] out IHandler handler);

    ConcurrentDictionary<ushort, IHandler> Streams { get; }
}
internal static class ListenerEngine
{

    public async static Task RunAsync(this IListener listener, ILogger? logger, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            logger.LogDebug(listener, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) processing streams...");
            await using var iter = listener.Connection.GetAsyncEnumerator(cancellationToken);
            while (!cancellationToken.IsCancellationRequested && await iter.MoveNextAsync())
            {
                var frame = iter.Current;
                var header = frame.GetHeader();
                logger.LogDebug(frame, static (state, _) => $"received frame {state}");
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
                            logger.LogError(header.StreamId, static (state, _) => $"duplicate id! {state}");
                            await listener.Connection.WriteAsync(new FrameHeader(FrameKind.Cancel, 0, header.StreamId, 0), cancellationToken);
                        }
                        else if (listener.TryCreateStream(in frame, out var newStream) && newStream is not null)
                        {
                            //var method = ;
                            //var handler = _server.TryGetHandler(method, out var handlerFactory) ? handlerFactory() : null;
                            // handler.Initialize(header.StreamId, _connection, logger);
                            if (listener.Streams.TryAdd(header.StreamId, newStream))
                            {
                                logger.LogDebug(frame, static (state, _) => $"method accepted: {state.GetPayloadString()}");
                            }
                            else
                            {
                                logger.LogError(header.StreamId, static (state, _) => $"duplicate id! {state}");
                                await listener.Connection.WriteAsync(new FrameHeader(FrameKind.Cancel, 0, header.StreamId, 0), cancellationToken);
                                newStream.Recycle();
                            }
                        }
                        else
                        {
                            logger.LogDebug(frame, static (state, _) => $"method not found: {state.GetPayloadString()}");
                            await listener.Connection.WriteAsync(new FrameHeader(FrameKind.MethodNotFound, 0, header.StreamId, 0), cancellationToken);
                        }
                        break;
                    default:
                        if (listener.Streams.TryGetValue(header.StreamId, out var existingStream) && existingStream is not null)
                        {
                            logger.LogDebug((handler: existingStream, frame: frame), static (state, _) => $"pushing {state.frame} to {state.handler.Method} ({state.handler.MethodType})");
                            if (existingStream.TryAcceptFrame(in frame))
                            {
                                release = false;
                            }
                            else
                            {
                                logger.LogInformation(frame, static (state, _) => $"frame {state} rejected by handler");
                            }
                        }
                        else
                        {
                            logger.LogInformation(frame, static (state, _) => $"received frame for unknown stream {state}");
                        }
                        break;
                }
                if (release)
                {
                    logger.LogInformation(frame.TotalLength, static (state, _) => $"releasing {state} bytes");
                    frame.Release();
                }
            }

            logger.LogInformation(listener, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) exiting cleanly");
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        { } // alt-success
        catch (Exception ex)
        {
            logger.LogError(ex);
            throw;
        }
        finally
        {
            logger.LogInformation(listener, static (state, _) => $"connection {state} ({(state.IsClient ? "client" : "server")}) all done");
            if (listener is not null)
            {
                await listener.Connection.SafeDisposeAsync();
            }
        }
    }

}