using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using MegaCorp;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using Shared_CS;

namespace Client_CS
{
    class Program
    {
        static async Task Main()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress("http://localhost:10042");
            var calculator = http.CreateGrpcService<ICalculator>();
            var result = await calculator.MultiplyAsync(new MultiplyRequest { X = 12, Y = 4 });
            Console.WriteLine(result.Result); // 48

            var clock = http.CreateGrpcService<ITimeService>();
            var counter = http.CreateGrpcService<ICounter>();
            using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var options = new CallOptions(cancellationToken: cancel.Token);

            try
            {
                await foreach (var time in clock.SubscribeAsync(new CallContext(options)))
                {
                    Console.WriteLine($"The time is now: {time.Time}");
                    var currentInc = await counter.IncrementAsync(new IncrementRequest { Inc = 1 });
                    Console.WriteLine($"Time recieved {currentInc.Result} times");
                }
            }
            catch (RpcException ex) { Console.WriteLine(ex); }
            catch (OperationCanceledException) { }
            Console.WriteLine("Press [Enter] to exit");
            Console.ReadLine();
        }
    }
}
