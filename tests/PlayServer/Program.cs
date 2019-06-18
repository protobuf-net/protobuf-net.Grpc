using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using Shared_CS;
using System;
using System.Threading.Tasks;

namespace PlayServer
{
    class Program
    {
        static async Task Main()
        {
            const int port = 10042;
            Server server = new Server
            {
                Services = { MyServerBinder.BindService(new MyServer()) },
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("server listening on port " + port);
            Console.ReadKey();

            await server.ShutdownAsync();
        }
    }
}

static class MyServerBinder
{
    /// <summary>Creates service definition that can be registered with a server</summary>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    public static ServerServiceDefinition BindService(MyServer serviceImpl)
    {
        return ServerServiceDefinition.CreateBuilder()
            .AddMethod(s_multiply, serviceImpl.Multiply)
            .Build();
    }

    /// <summary>Register service method with a service binder with or without implementation. Useful when customizing the  service binding logic.
    /// Note: this method is part of an experimental API that can change or be removed without any prior notice.</summary>
    /// <param name="serviceBinder">Service methods will be bound by calling <c>AddMethod</c> on this object.</param>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    public static void BindService(ServiceBinderBase serviceBinder, MyServer serviceImpl)
    {
        serviceBinder.AddMethod(s_multiply, serviceImpl == null ? null : new UnaryServerMethod<MultiplyRequest, MultiplyResult>(serviceImpl.Multiply));
    }


    static readonly Method<MultiplyRequest, MultiplyResult> s_multiply = new Method<MultiplyRequest, MultiplyResult>(MethodType.Unary,
        "Hyper.Calculator", "Multiply",
#pragma warning disable CS0618
        MarshallerCache<MultiplyRequest>.Instance,
        MarshallerCache<MultiplyResult>.Instance);
#pragma warning restore CS0618
}

[BindServiceMethod(typeof(MyServerBinder), nameof(MyServerBinder.BindService))]
public class MyServer
{
    
    public Task<MultiplyResult> Multiply(MultiplyRequest request, ServerCallContext context)
    {
        return Task.FromResult(new MultiplyResult(request.X * request.Y));
    }
}