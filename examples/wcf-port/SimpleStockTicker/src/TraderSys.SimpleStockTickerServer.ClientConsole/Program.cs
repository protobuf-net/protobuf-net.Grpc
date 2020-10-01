using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using TraderSys.SimpleStockTickerServer.Shared;

namespace TraderSys.SimpleStockTickerServer.ClientConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = channel.CreateGrpcService<IStockTickerService>();

            var request = new SubscribeRequest();
            request.Symbols.AddRange(args);
            var updates = client.Subscribe(request);

            var tokenSource = new CancellationTokenSource();
            var task = DisplayAsync(updates, tokenSource.Token);

            WaitForExitKey();

            tokenSource.Cancel();
            await task;
        }

        static async Task DisplayAsync(IAsyncEnumerable<StockTickerUpdate> updates, CancellationToken token)
        {
            try
            {
                await foreach (var update in updates.WithCancellation(token))
                {
                    Console.WriteLine($"{update.Symbol}: {update.Price}");
                }
            }
            catch (RpcException e)
            {
                if (e.StatusCode == StatusCode.Cancelled)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Finished.");
            }
        }

        static void WaitForExitKey()
        {
            Console.WriteLine("Press E to exit...");

            char ch = ' ';

            while (ch != 'e')
            {
                ch = char.ToLowerInvariant(Console.ReadKey().KeyChar);
            }
        }
    }
}