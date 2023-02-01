#define FAIL
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hyper;
using MegaCorp;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<MyCalculator>();
app.MapGrpcService<MyClock>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();


class MyCalculator : Calculator.CalculatorBase
{
    public override Task<MultiplyResult> Multiply(MultiplyRequest request, ServerCallContext context)
    {
#if FAIL
        throw new InvalidOperationException("The cogs are misaligned");
#else
        var result = request.X * request.Y;
        return Task.FromResult(new MultiplyResult { Result = result });
#endif
    }
}
class MyClock : TimeService.TimeServiceBase
{
    public override async Task Subscribe(Empty request,
        IServerStreamWriter<TimeResult> responseStream, ServerCallContext context)
    {
        for (int i = 0; i < 5; i++)
        {
            await responseStream.WriteAsync(new TimeResult { Time = Timestamp.FromDateTime(DateTime.UtcNow) });
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}