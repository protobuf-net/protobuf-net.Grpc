using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Lite;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static FooService;


//using (var managedHttp = GrpcChannel.ForAddress("http://localhost:5074"))
//{
//    await Run(managedHttp);
//}
//using (var managedHttps = GrpcChannel.ForAddress("https://localhost:7074"))
//{
//    await Run(managedHttps);
//}
//{
//    var unmanagedHttp = new Channel("localhost", 5074, ChannelCredentials.Insecure);
//    await Run(unmanagedHttp);
//}
// TLS with Grpc.Core is a PITA to configure, so... meh

using (var namedPipe = await ConnectionFactory.ConnectNamedPipe("grpctest_merge").AsFrames(true).CreateChannelAsync(TimeSpan.FromSeconds(5)))
{
    await Run(namedPipe);
}
using (var namedPipe = await ConnectionFactory.ConnectNamedPipe("grpctest_nomerge").AsFrames(true).CreateChannelAsync(TimeSpan.FromSeconds(5)))
{
    await Run(namedPipe);
}


static async Task Run(ChannelBase channel, [CallerArgumentExpression("channel")] string caller = "")
{
    try
    {
        var invoker = channel.CreateCallInvoker();
        Console.WriteLine($"Connecting to {channel.Target} ({caller}, {invoker.GetType().Name})...");
        var client = new FooServiceClient(invoker);

        {
            using (var call = client.UnaryAsync(new FooRequest { Value = 42 }))
            {
                var result = await call.ResponseAsync;
                if (result?.Value != 42) throw new InvalidOperationException("Incorrect response received: " + result);
                Console.WriteLine("(success)");
            }
        }

        for (int j = 0; j < 5; j++)
        {
            var watch = Stopwatch.StartNew();
            const int OPCOUNT = 5 * 1024;
            for (int i = 0; i < OPCOUNT; i++)
            {
                using var call = client.UnaryAsync(new FooRequest { Value = i });
                var result = await call.ResponseAsync;

                if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
            }
            ShowTiming(nameof(client.UnaryAsync), watch, OPCOUNT);
        }

        for (int j = 0; j < 5; j++)
        {
            var watch = Stopwatch.StartNew();
            using var call = client.ClientStreaming();
            const int OPCOUNT = 50 * 1024;
            int sum = 0;
            for (int i = 0; i < OPCOUNT; i++)
            {
                await call.RequestStream.WriteAsync(new FooRequest { Value = i });
                sum += i;
            }
            await call.RequestStream.CompleteAsync();
            var result = await call.ResponseAsync;
            if (result?.Value != sum) throw new InvalidOperationException("Incorrect response received: " + result);
            ShowTiming(nameof(client.ClientStreaming), watch, OPCOUNT);
        }

        for (int j = 0; j < 5; j++)
        {
            var watch = Stopwatch.StartNew();
            const int OPCOUNT = 100 * 1024;
            using var call = client.ServerStreaming(new FooRequest { Value = OPCOUNT });
            int count = 0;
            while (await call.ResponseStream.MoveNext())
            {
                var result = call.ResponseStream.Current;
                if (result?.Value != count) throw new InvalidOperationException("Incorrect response received: " + result);
                count++;
            }
            if (count != OPCOUNT) throw new InvalidOperationException("Incorrect response count received: " + count);
            ShowTiming(nameof(client.ServerStreaming), watch, OPCOUNT);
        }

        for (int j = 0; j < 5; j++)
        {
            var watch = Stopwatch.StartNew();
            const int OPCOUNT = 20 * 1024;
            using var call = client.Duplex();

            for (int i = 0; i < OPCOUNT; i++)
            {
                await call.RequestStream.WriteAsync(new FooRequest { Value = i });
                if (!await call.ResponseStream.MoveNext()) throw new InvalidOperationException("Duplex stream terminated early");
                var result = call.ResponseStream.Current;
                if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
            }
            await call.RequestStream.CompleteAsync();
            if (await call.ResponseStream.MoveNext()) throw new InvalidOperationException("Duplex stream ran over");
            ShowTiming(nameof(client.Duplex), watch, OPCOUNT);
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[{channel.Target}]: {ex.Message}");
    }
    finally
    {
        await channel.ShutdownAsync();
        Console.WriteLine();
    }

    static void ShowTiming(string label, Stopwatch watch, int operations)
    {
        watch.Stop();
        var micros = (watch.ElapsedTicks * 1_000_000) / Stopwatch.Frequency;
        Console.WriteLine($"{label}×{operations}: {AutoScale(micros)}, {AutoScale(micros / operations)}/op");

        static string AutoScale(long micros)
        {
            long qty = micros;
            if (qty < 10000) return $"{qty}μs";
            qty /= 1000;
            if (qty < 10000) return $"{qty}ms";

            return TimeSpan.FromMilliseconds(qty).ToString();
        }
    }
}