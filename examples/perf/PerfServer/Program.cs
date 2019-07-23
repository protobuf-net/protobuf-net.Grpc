using Grpc.Core;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using PerfTest;
using ProtoBuf.Grpc.Server;

namespace PerfServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // unmanaged server on 10050, managed server on 10051
            System.Console.WriteLine("Starting gRPC...");
            Server server = new Server
            {
                Ports = { new ServerPort("localhost", 10050, ServerCredentials.Insecure) },
            };
            server.Services.Add(VanillaGrpc.BindService(new VanillaGrpcServer()));
            server.Services.AddCodeFirst(new ProtobufNetGrpcServer());
            server.Start();
            System.Console.WriteLine("Native gRPC listening on :10050");
            System.Console.WriteLine("Starting Kestrel...");
            CreateHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
              WebHost.CreateDefaultBuilder(args)
                .ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(10051, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                })
                .UseStartup<Startup>();
    }
}
