using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal sealed class ServerUnaryHandler<TRequest, TResponse> : ServerHandler<TRequest, TResponse> where TResponse : class where TRequest : class
{
    private UnaryServerMethod<TRequest, TResponse>? _handler;

    public static ServerUnaryHandler<TRequest, TResponse> Get(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
    {
        var obj = Pool<ServerUnaryHandler<TRequest, TResponse>>.Get();
        obj.Method = method;
        obj._handler = handler;
        return obj;
    }

    public override void Recycle()
    {
        // not really necessary to reset marshaller/method/handler; they'll be alive globally
        Pool<ServerUnaryHandler<TRequest, TResponse>>.Put(this);
    }

    public override FrameKind Kind => FrameKind.NewUnary;
    public override ValueTask CompleteAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    protected override async ValueTask PushCompletePayloadAsync(ushort id, ChannelWriter<StreamFrame> output, TRequest value, ILogger? logger, CancellationToken cancellationToken)
    {
        var method = Method;
        logger.LogDebug(method, static (state, _) => $"invoking {state.FullName}...");
        try
        {
            var ctx = CreateServerCallContext();
            var response = await _handler!(value, null!);
            ctx.Recycle();
            logger.LogDebug(method, static (state, _) => $"completed {state.FullName}...");

            if (Status.StatusCode == StatusCode.OK)
            {
                await WritePayloadAsync(response, true);
            }
        }
        catch (RpcException rpc)
        {
            logger.LogInformation(method!, static (state, ex) => $"rpc exception {state.FullName}: {ex!.Message}", rpc);
            var status = rpc.Status;
            if (status.StatusCode == StatusCode.OK)
            {
                // one does not simply fail with success!
                status = new Status(StatusCode.Unknown, status.Detail, status.DebugException);
            }
            Status = status;
        }
        catch (Exception ex)
        {
            logger.LogError(method!, static (state, ex) => $"faulted {state.FullName}: {ex!.Message}", ex);
            Status = new Status(StatusCode.Unknown, "The server encountered an error while performing the operation", ex);
        }

        await WriteStatusAndTrailers();
    }
}
