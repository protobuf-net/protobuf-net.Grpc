using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.

var cert = new X509Certificate2("mytestserver.pfx", "password");
builder.Services.AddGrpc();
builder.Services.AddSingleton<MyService>();
builder.Services.AddSingleton<LiteServer>(services =>
{
    var logger = services.GetService<ILogger<LiteServer>>();
    var server = new LiteServer(logger);
    server.ManualBind(services.GetService<MyService>());
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_merge", logger).AsFrames(true));
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_nomerge", logger).AsFrames(false));
    server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_tls", logger).WithTls().AuthenticateAsServer(
        new SslServerAuthenticationOptions
        {
            ServerCertificate = cert
        }).AsFrames());
    return server;
});

var app = builder.Build();

var grpc = app.Services.GetService<LiteServer>()!;

// Configure the HTTP request pipeline.
app.MapGrpcService<MyService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");



app.Run();
