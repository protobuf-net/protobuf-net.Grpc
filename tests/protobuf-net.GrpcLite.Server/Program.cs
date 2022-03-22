using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();

var server = new LiteServer();
_ = server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_merge").AsFrames(true));
_ = server.ListenAsync(ConnectionFactory.ListenNamedPipe("grpctest_nomerge").AsFrames(false));
builder.Services.AddSingleton(server); // keep it alive

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<MyService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");



app.Run();
