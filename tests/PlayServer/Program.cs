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

internal class MyServer : ICalculator, IDuplex
{
    ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request)
    {
        Console.WriteLine($"{request.X}x{request.Y}");
        return new ValueTask<MultiplyResult>(new MultiplyResult(request.X * request.Y));
    }

    //IAsyncEnumerable<MultiplyResult> IDuplex.Foo(IAsyncEnumerable<MultiplyRequest> bar, CallContext context)
    //    => context.FullDuplex(bar, ProduceAsync, ConsumeAsync);

    static readonly Func<CallContext, IAsyncEnumerable<MultiplyResult>> s_producer
        = ctx => ctx.As<MyServer>().ProduceAsync(ctx, ctx.CancellationToken);
    static readonly Func<MultiplyRequest, CallContext, ValueTask> s_consumer
        = (req, ctx) => ctx.As<MyServer>().ConsumeAsync(req, ctx);

    IAsyncEnumerable<MultiplyResult> IDuplex.FullDuplexAsync(IAsyncEnumerable<MultiplyRequest> bar, CallContext context)
        => context.FullDuplexAsync(bar, s_producer, s_consumer);

    //async IAsyncEnumerable<MultiplyResult> IDuplex.Foo(IAsyncEnumerable<MultiplyRequest> bar, CallContext context)
    //{
    //    bool allGood = false;
    //    try
    //    {
    //        await foreach (var item in context.FullDuplex(bar, s_producer, s_consumer))
    //        {
    //            yield return item;
    //        }
    //        Console.WriteLine("server completed with success");
    //        allGood = true;
    //    }
    //    finally
    //    {
    //        if (!allGood) Console.WriteLine("server completed with fault");
    //    }
    //    //catch (Exception ex)
    //    //{
    //    //    Console.WriteLine("server completed with fault: " + ex.Message);
    //    //}
    //}

    async IAsyncEnumerable<MultiplyResult> ProduceAsync(CallContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
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
        Console.WriteLine($"[rec] {item.X}, {item.Y} from {context.Server?.Peer}");
        return default;
    }
}