using Grpc.Net.Client;
using Hyper;
using System.Diagnostics;

namespace Client
{
    class Program
    {
        static async Task Main()
        {
            Task[] clients = new Task[20];
            int success = 0, fail = 0;
            CancellationTokenSource cts = new();
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var watch = Stopwatch.StartNew();
            _ = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                    Console.WriteLine($"Success: {Volatile.Read(ref success):###,##0}, Fail: {Volatile.Read(ref fail):###,##0}");
                }
            });
            for (int i = 0; i < clients.Length; i++)
            {
                clients[i] = Task.Run(() => RunClient());
            }
            await Task.WhenAll(clients);
            cts.Cancel();
            watch.Stop();
            Console.WriteLine($"Total: {Volatile.Read(ref success) + Volatile.Read(ref fail):###,##0} in {watch.Elapsed}");

            async Task RunClient()
            {
                var channel = GrpcChannel.ForAddress("http://localhost:10042");
                var calc = new Calculator.CalculatorClient(channel);
                MultiplyRequest req = new MultiplyRequest { X = 2, Y = 4 };
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await calc.MultiplyAsync(req);
                        Interlocked.Increment(ref success);
                    }
                    catch
                    {
                        Interlocked.Increment(ref fail);
                    }
                }
                await channel.ShutdownAsync();
            }
        }
    }
}
