#if NET472
using Grpc.Core;
#else
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
#endif
using ProtoBuf.Grpc.Server;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;
using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

var serverCert = new X509Certificate2("mytestserver.pfx", "password");
RemoteCertificateValidationCallback userCheck = (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    =>
{
    if (certificate is null)
    {
        Console.WriteLine($"No user-certificate received; rejecting ({sslPolicyErrors})");
        return false;
    }
    Console.WriteLine($"Received cert from user '{certificate?.Subject}'; {sslPolicyErrors}; trusting...");
    return true;
};

#if NET472
var interceptor = new MyInterceptor();
var svc1 = new MyContractFirstService();
var svc2 = new MyCodeFirstService();
ILogger logger = ConsoleLogger.Information;
Server gServer = new Server
{
    Ports = { new ServerPort("localhost", 5074, ServerCredentials.Insecure) },
    Services = {
        FooService.BindService(svc1),
    }
};

gServer.Services.AddCodeFirst(svc2, interceptors: new[] { interceptor });
gServer.Start();

var lServer = new LiteServer(logger);
lServer.ServiceBinder.Bind(svc1);
lServer.ServiceBinder.Intercept(interceptor).AddCodeFirst(svc2);

_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_merge", logger: logger).AsFrames(true));
_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_buffer", logger: logger).AsFrames());
_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_passthru", logger: logger).AsFrames(outputBufferSize: 0));
_ = lServer.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10042), logger: logger).AsStream().AsFrames());
_ = lServer.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10043), logger: logger).AsStream().WithTls().AuthenticateAsServer(serverCert).AsFrames());
_ = lServer.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10045), logger: logger).AsStream().WithTls(userCheck).AuthenticateAsServer(serverCert, clientCertificateRequired: true).AsFrames());
_ = lServer.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_tls", logger: logger).WithTls().AuthenticateAsServer(serverCert).AsFrames());
Console.WriteLine($"Servers running ({lServer.MethodCount} methods)... press any key");
Console.ReadKey();
lServer.Stop();
await gServer.ShutdownAsync();

#else
var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddSingleton<MyContractFirstService>().AddSingleton<IMyService, MyCodeFirstService>().AddSingleton<MyInterceptor>();
builder.Services.AddGrpc().AddServiceOptions<IMyService>(options =>
{   // add an interceptor just for IMyService (but not for MyContractFirstService)
    options.Interceptors.Add<MyInterceptor>();
});
builder.Services.AddCodeFirstGrpc();
builder.Services.AddSingleton<LiteServer>(services =>
{
    var logger = services.GetService<ILogger<LiteServer>>();
    var server = new LiteServer(logger);
    server.ServiceBinder.Bind(services.GetService<MyContractFirstService>());
    server.ServiceBinder.Intercept(services.GetService<MyInterceptor>()!).AddCodeFirst(services.GetService<IMyService>()!);
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_merge", logger: logger).AsFrames(true));
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_buffer", logger: logger).AsFrames());
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_passthru", logger: logger).AsFrames(outputBufferSize: 0));
    server.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10042), logger: logger).AsStream().AsFrames());
    server.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10043), logger: logger).AsStream().WithTls().AuthenticateAsServer(serverCert).AsFrames());
    server.ListenAsync(ConnectionFactory.ListenSocket(new IPEndPoint(IPAddress.Loopback, 10045), logger: logger).AsStream().WithTls(userCheck).AuthenticateAsServer(serverCert, clientCertificateRequired: true).AsFrames());
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_tls", logger: logger).WithTls().AuthenticateAsServer(serverCert).AsFrames());
    return server;
});
var app = builder.Build();

var grpc = app.Services.GetService<LiteServer>()!;

// Configure the HTTP request pipeline.
app.UseRouting().UseEndpoints(ep => ep.MapGrpcService<IMyService>());
app.MapGrpcService<MyContractFirstService>();
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