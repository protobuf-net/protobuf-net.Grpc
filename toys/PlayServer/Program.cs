using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Server;
using Shared_CS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PlayServer
{
    class Program
    {
        static async Task Main()
        {
            const int port = 10042;
            Server server = new Server
            {
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };
            int opCount = server.Services.AddCodeFirst(new MyServer(), log: Console.Out);
            server.Start();

            Console.WriteLine($"server listening to {opCount} operations on port {port}");
            Console.ReadKey();

            await server.ShutdownAsync();
        }
    }
}

internal class MyServer : ICalculator, IDuplex, IBidiStreamingService
{
    ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request)
    {
        Console.WriteLine($"{request.X}x{request.Y}");
        return new ValueTask<MultiplyResult>(new MultiplyResult(request.X * request.Y));
    }

    //IAsyncEnumerable<MultiplyResult> IDuplex.FullDuplexAsync(IAsyncEnumerable<MultiplyRequest> bar, CallContext context)
    //    => context.FullDuplexAsync(ProduceAsync, bar, ConsumeAsync);

    IAsyncEnumerable<MultiplyResult> IDuplex.SomeDuplexApiAsync(IAsyncEnumerable<MultiplyRequest> bar, CallContext context)
        => context.FullDuplexAsync(s_producer, bar, s_consumer);

    static readonly Func<CallContext, IAsyncEnumerable<MultiplyResult>> s_producer
        = ctx => ctx.As<MyServer>().ProduceAsync(ctx);
    static readonly Func<MultiplyRequest, CallContext, ValueTask> s_consumer
        = (req, ctx) => ctx.As<MyServer>().ConsumeAsync(req, ctx);



    private IAsyncEnumerable<MultiplyResult> ProduceAsync(CallContext context) => ProduceAsyncImpl(context.CancellationToken);
#pragma warning disable IDE0060
    private async IAsyncEnumerable<MultiplyResult> ProduceAsyncImpl([EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore IDE0060
    {
        for (int i = 0; i < 4; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2.5));

            var result = new MultiplyResult { Result = i };
            yield return result;
            Console.WriteLine($"[sent] {result.Result}");
        }
        Console.WriteLine("[server all done sending!]");
    }
    //async Task ConsumeAsync(IAsyncEnumerable<MultiplyRequest> bar, CallContext context)
    //{
    //    await foreach (var item in bar)
    //    {
    //        Console.WriteLine($"[rec] {item.X}, {item.Y} from {context.Server?.Peer}");
    //    }
    //}

    ValueTask ConsumeAsync(MultiplyRequest item, CallContext context)
    {
        Console.WriteLine($"[rec] {item.X}, {item.Y} from {context.ServerCallContext?.Peer}");
        return default;
    }

    public async IAsyncEnumerable<BidiStreamingResponse> TestAsync(IAsyncEnumerable<BidiStreamingRequest> requestStream,
    CallContext context)
    {
        await Task.Yield();
        Console.WriteLine("STARTING. TestAsync method call received.");
        //if (Always()) throw new InvalidOperationException("oops");
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(500, context.CancellationToken);
            yield return new BidiStreamingResponse { Payload = $"response {i}" };
        }

        //static bool Always() => true;
       // yield break;
    }
}