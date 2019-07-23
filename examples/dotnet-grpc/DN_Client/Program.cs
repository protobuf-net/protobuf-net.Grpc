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

            // don't @ me about HttpClient/Dispose
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:10042") };

            var calc = GrpcClient.Create<Calculator.CalculatorClient>(http);
            for (int i = 0; i < 5; i++)
            {
                using var ma = calc.MultiplyAsync(new MultiplyRequest { X = i, Y = i });
                var calcResult = await ma.ResponseAsync;
                Console.WriteLine(calcResult.Result);
            }

            var clock = GrpcClient.Create<TimeService.TimeServiceClient>(http);
            using var subResult = clock.Subscribe(new Empty());
            using var reader = subResult.ResponseStream;
            while (await reader.MoveNext(default))
            {
                var time = reader.Current.Time;
                Console.WriteLine($"The time is now {time}");
            }
        }
    }
}
