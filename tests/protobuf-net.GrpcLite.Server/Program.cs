#if !NET472
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
#endif
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;
using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

var cert = new X509Certificate2("mytestserver.pfx", "password");

#if NET472
var svc = new MyService();
ILogger logger = ConsoleLogger.Information;
Server gServer = new Server
{
    Ports = { new ServerPort("localhost", 5074, ServerCredentials.Insecure) },
    Services = {
        FooService.BindService(svc),
    }
};
gServer.Start();

var lServer = new LiteServer(logger);
lServer.Bind(svc);
_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_merge", logger: logger).AsFrames(true));
_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_buffer", logger: logger).AsFrames());
_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_passthru", logger: logger).AsFrames(outputBufferSize: 0));
_ = lServer.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10042), logger: logger).AsFrames());
_ = lServer.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10043), logger: logger).WithTls().AuthenticateAsServer(cert).AsFrames());
_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_tls", logger: logger).WithTls().AuthenticateAsServer(cert).AsFrames());

Console.WriteLine($"Servers running ({lServer.MethodCount} methods)... press any key");
Console.ReadKey();
await gServer.ShutdownAsync();

#else
var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.

builder.Services.AddGrpc();
builder.Services.AddSingleton<MyService>();
builder.Services.AddSingleton<LiteServer>(services =>
{
    var logger = services.GetService<ILogger<LiteServer>>();
    var server = new LiteServer(logger);
    server.Bind(services.GetService<MyService>());
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_merge", logger: logger).AsFrames(true));
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_buffer", logger: logger).AsFrames());
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_passthru", logger: logger).AsFrames(outputBufferSize: 0));
    server.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10042), logger: logger).AsFrames());
    server.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10043), logger: logger).WithTls().AuthenticateAsServer(cert).AsFrames());
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_tls", logger: logger).WithTls().AuthenticateAsServer(cert).AsFrames());
    return server;
});

var app = builder.Build();

var grpc = app.Services.GetService<LiteServer>()!;

// Configure the HTTP request pipeline.
app.MapGrpcService<MyService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");



app.Run();
#endif
/* non-working attempt to get gRPC and TCP endpoint working together
 * outcome: no gRPC bound (I'm guessing I need to add moar endpoints?)
 * what I want is: regular aspnet like above, but with a raw pipeline
 * TCP server, too

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Connections;

using ProtoBuf.Grpc.Lite;
using ProtoBuf.Grpc.Lite.Connections;
using protobuf_net.GrpcLite.Test;
using System.Net;
using System.Security.Cryptography.X509Certificates;

var cert = new X509Certificate2("mytestserver.pfx", "password");
var builder = WebHost.CreateDefaultBuilder().ConfigureServices(services =>
{
    services.AddGrpc();
    services.AddSingleton<MyService>();
    services.AddSingleton<MyHandler>();
    services.AddSingleton<LiteServer>(services =>
    {
        var logger = services.GetService<ILogger<LiteServer>>();
        var server = new LiteServer(logger);
        server.Bind(services.GetService<MyService>());
        server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_merge", logger: logger).AsFrames(true));
        server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_buffer", logger: logger).AsFrames());
        server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_passthru", logger: logger).AsFrames(outputBufferSize: 0));
        server.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10042), logger: logger).AsFrames());
        server.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10043), logger: logger).WithTls().AuthenticateAsServer(cert).AsFrames());
        server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_tls", logger: logger).WithTls().AuthenticateAsServer(cert).AsFrames());
        return server;
    });
}).UseKestrel(options =>
{
    options.Listen(new IPEndPoint(IPAddress.Loopback, 10044), o => o.UseConnectionHandler<MyHandler>());
}).Configure((ctx, app) =>
{
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapGrpcService<MyService>();
    });
});
await builder.Build().RunAsync();

public sealed class MyHandler : ConnectionHandler
{
    private readonly LiteServer _server;

    public MyHandler(LiteServer server) => _server = server;

    public override Task OnConnectedAsync(ConnectionContext connection)
        => _server.ListenAsync(connection.Transport.AsFrames());
}
*/