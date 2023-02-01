#define FAIL

using Grpc.Core;
using Hyper;
using System;
using System.Threading.Tasks;
using MegaCorp;
using Google.Protobuf.WellKnownTypes;

namespace MyServer
{
    class Program
    {
        static async Task Main()
        {
            var calc = new MyCalculator();
            var clock = new MyClock();
            Server server = new Server
            {
                Ports = { new ServerPort("localhost", 10042, ServerCredentials.Insecure) },
                Services = {
                    Calculator.BindService(calc),
                    TimeService.BindService(clock),
                }
            };
            server.Start();
            Console.WriteLine("Server running... press any key");
            Console.ReadKey();
            await server.ShutdownAsync();
        }
    }

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
            for(int i = 0; i < 5; i++)
            {
                await responseStream.WriteAsync(new TimeResult { Time = Timestamp.FromDateTime(DateTime.UtcNow) });
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
