using Grpc.Core;
using ProtoBuf.Grpc.Server;
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
                Services = { new MyServer() },
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("server listening on port " + port);
            Console.ReadKey();

            await server.ShutdownAsync();
        }
    }
}

public class MyServer : ICalculator
{
    public Task<MultiplyResult> Multiply(MultiplyRequest request, ServerCallContext context)
        => ((ICalculator)this).MultiplyAsync(request).AsTask();

    ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request)
    {
        Console.WriteLine($"{request.X}x{request.Y}");
        return new ValueTask<MultiplyResult>(new MultiplyResult(request.X * request.Y));
    }
}