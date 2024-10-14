using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Server;
using Shared_CS;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
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
        //for (int i = 0; i < 2; i++)
        //{
        //    await Task.Delay(500, context.CancellationToken);
        //    yield return new BidiStreamingResponse { Payload = $"response {i}" };
        //}

        //static bool Always() => true;
        yield break;
    }

    public ValueTask<Stream> TestStreamAsync(TestStreamRequest request, CallContext options = default)
    {
        Console.WriteLine("Creating pipe...");
        var pipe = new Pipe();
        _ = Task.Run(async () =>
        {
            Exception? ex = null;
            try
            {
                Console.WriteLine($"Starting stream of length {request.Length}...");
                long remaining = request.Length;
                var rand = new Random(request.Seed);
                byte[] buffer = new byte[4096];

                while (remaining > 0)
                {
                    int chunkLen = (int)Math.Min(remaining, buffer.Length);
                    var chunk = new Memory<byte>(buffer, 0, chunkLen);
                    Console.WriteLine($"Sending {chunkLen}...");
                    Fill(rand, chunk.Span);
                    await pipe.Writer.WriteAsync(chunk, options.CancellationToken);
                    remaining -= chunkLen;
                }
            }
            catch (Exception fault)
            {
                Console.WriteLine("Fault: " + fault.Message);
                ex = fault;
            }
            finally
            {
                Console.WriteLine("Completing...");
                await pipe.Writer.CompleteAsync(ex);
            }

        });
        return new(pipe.Reader.AsStream());

        static void Fill(Random rand, Span<byte> buffer)
        {
            foreach (ref byte b in buffer)
            {
                b = (byte)rand.Next(0, 256);
            }
        }
    }
}