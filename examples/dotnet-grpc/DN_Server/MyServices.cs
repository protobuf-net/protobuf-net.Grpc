using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hyper;
using MegaCorp;
using System;
using System.Threading.Tasks;

namespace DN_Server
{
    class MyCalculator : Calculator.CalculatorBase
    {
        public override Task<MultiplyResult> Multiply(MultiplyRequest request, ServerCallContext context)
        {
            Console.WriteLine($"Processing request from {context.Peer}");
            var result = request.X * request.Y;
            return Task.FromResult(new MultiplyResult { Result = result });
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
}
