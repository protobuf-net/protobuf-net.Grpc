using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Hyper;
using MegaCorp;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        static async Task Main()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var channel = GrpcChannel.ForAddress("http://localhost:10042");
            var calc = new Calculator.CalculatorClient(channel);
            for (int i = 0; i < 5; i++)
            {
                using var ma = calc.MultiplyAsync(new MultiplyRequest { X = i, Y = i });
                var calcResult = await ma.ResponseAsync;
                Console.WriteLine(calcResult.Result);
            }

            var clock = new TimeService.TimeServiceClient(channel);
            using var subResult = clock.Subscribe(new Empty());
            var reader = subResult.ResponseStream;
            while (await reader.MoveNext(default))
            {
                var time = reader.Current.Time;
                Console.WriteLine($"The time is now {time}");
            }
        }
    }
}
