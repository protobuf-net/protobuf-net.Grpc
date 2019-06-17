using Grpc.Core;
using MegaCorp;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Internal;
using Shared_CS;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CS0618
namespace Client_CS
{
    class Program
    {
        static async Task Main()
        {
            ClientFactory.AllowUnencryptedHttp2 = true;
            using (var http = new HttpClient { BaseAddress = new Uri("http://localhost:10042") })
            {
                var calculator = ClientFactory.Create<ICalculator>(http);
                var result = await calculator.MultiplyAsync(new MultiplyRequest { X = 12, Y = 4 });
                Console.WriteLine(result.Result); // 48

                var clock = ClientFactory.Create<ITimeService>(http);
                var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                var options = new CallOptions(cancellationToken: cancel.Token);
                await foreach(var time in clock.SubscribeAsync(Empty.Instance, new CallContext(options)))
                {
                    Console.WriteLine($"The time is now: {time.Time}");
                }
            }
        }
    }
}
