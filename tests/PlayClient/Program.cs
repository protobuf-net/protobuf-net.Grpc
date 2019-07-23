using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using Shared_CS;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PlayClient
{
    static class Program
    {
        static async Task Main()
        {
            await TestChannel();
#if HTTPCLIENT
            await TestHttpClient();
#endif
        }

        static async Task TestCalculator(ICalculator calculator, [CallerMemberName] string? caller = null)
        {
            Console.WriteLine($"testing calculator ({caller})");
            var prod = await calculator.MultiplyAsync(new MultiplyRequest(4, 9));
            Console.WriteLine(prod.Result);
        }


        static async Task TestDuplex(IDuplex duplex, [CallerMemberName] string? caller = null)
        {
            Console.WriteLine($"testing duplex ({caller}) - manual");
            await foreach(var item in duplex.SomeDuplexApiAsync(Rand(10, TimeSpan.FromSeconds(1))))
            {
                Console.WriteLine($"[rec] {item.Result}");
            }

            Console.WriteLine($"testing duplex ({caller}) - auto duplex");
            var channel = System.Threading.Channels.Channel.CreateBounded<MultiplyRequest>(5);
            var ctx = new CallContext(state: channel.Writer);
            var result = duplex.SomeDuplexApiAsync(channel.AsAsyncEnumerable(ctx.CancellationToken));
            await ctx.FullDuplexAsync<MultiplyResult>(s_pumpQueue, result, s_handleResult);
        }

        static readonly Func<CallContext, ValueTask> s_pumpQueue = async ctx =>
        {
            var writer = ctx.As<System.Threading.Channels.ChannelWriter<MultiplyRequest>>();
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var item = new MultiplyRequest { X = 40 + i, Y = 40 + i };
                    await writer.WriteAsync(item, ctx.CancellationToken);
                    Console.WriteLine($"[d:sent] {item.X}, {item.Y}");
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                }
                writer.Complete();
                Console.WriteLine("[d:client all done sending!]");
            } catch(Exception ex) { writer.TryComplete(ex); }
        };

        static readonly Func<MultiplyResult, CallContext, ValueTask> s_handleResult = (result, ctx) =>
        {
            Console.WriteLine($"[d:rec] {result.Result}");
            return default;
        };

        static async IAsyncEnumerable<MultiplyRequest> Rand(int count, TimeSpan delay, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for(int i = 0; i < count; i++)
            {
                await Task.Delay(delay);
                var next = new MultiplyRequest { X = i, Y = i };
                yield return next;
                Console.WriteLine($"[sent] {next.X}, {next.Y}");
            }
            Console.WriteLine("[client all done sending!]");
        }

        static async Task TestChannel()
        {
            var channel = new Channel("localhost", 10042, ChannelCredentials.Insecure);
            try
            {
                var calculator = channel.CreateGrpcService<ICalculator>();
                await TestCalculator(calculator);

                var duplex = channel.CreateGrpcService<IDuplex>();
                await TestDuplex(duplex);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

#if HTTPCLIENT
        static async Task TestHttpClient()
        {
            HttpClientExtensions.AllowUnencryptedHttp2 = true;
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:10042") };

            var calculator = http.CreateGrpcService<ICalculator>();
            await TestCalculator(calculator);

            var duplex = http.CreateGrpcService<IDuplex>();
            await TestDuplex(duplex);
        }
#endif
    }
}