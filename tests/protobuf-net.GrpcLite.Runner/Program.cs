using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static FooService;

Dictionary<string, (string unary, string clientStreaming, string serverStreaming, string duplex)> timings = new();

using (var managedHttp = GrpcChannel.ForAddress("http://localhost:5074"))
{
    await Run(managedHttp);
}
using (var managedHttps = GrpcChannel.ForAddress("https://localhost:7074"))
{
    await Run(managedHttps);
}
{
    var unmanagedHttp = new Channel("localhost", 5074, ChannelCredentials.Insecure);
    await Run(unmanagedHttp);
}

{
    using var localServer = new LiteServer();
    localServer.ManualBind<MyService>();
    using (var local = localServer.CreateLocalClient())
    {
        await Run(local);
    }
}


using (var namedPipeMerge = await ConnectionFactory.ConnectNamedPipe("grpctest_merge").AsFrames(true).CreateChannelAsync(TimeSpan.FromSeconds(5)))
{
    await Run(namedPipeMerge);
}
using (var namedPipeVanilla = await ConnectionFactory.ConnectNamedPipe("grpctest_nomerge").AsFrames(false).CreateChannelAsync(TimeSpan.FromSeconds(5)))
{
    await Run(namedPipeVanilla);
}
// THIS ONE NEEDS INVESTIGATION; TLS doesn't handshake
//using (var namedPipeTls = await ConnectionFactory.ConnectNamedPipe("grpctest_tls").WithTls().AuthenticateAsClient("mytestserver").AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(50)))
//{
//    await Run(namedPipeTls);
//}

Console.WriteLine();
Console.WriteLine("| Scenario | Unary | Client-Streaming | Server-Streaming | Duplex |");
Console.WriteLine("| -------- | ----- | ---------------- | ---------------- | ------ |");
foreach (var (scenario, data) in timings.OrderBy(x => x.Key))
{
    Console.WriteLine($"| {scenario} | {data.unary} | {data.clientStreaming} | {data.serverStreaming} | {data.duplex} |");
}


async Task Run(ChannelBase channel, [CallerArgumentExpression("channel")] string caller = "", int repeatCount = 10)
{
    try
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(60));
        var options = new CallOptions(cancellationToken: cts.Token);

        var invoker = channel.CreateCallInvoker();
        Console.WriteLine($"Connecting to {channel.Target} ({caller}, {invoker.GetType().Name})...");
        var client = new FooServiceClient(invoker);

        using (var call = client.UnaryAsync(new FooRequest { Value = 42 }, options))
        {
            var result = await call.ResponseAsync;
            if (result?.Value != 42) throw new InvalidOperationException("Incorrect response received: " + result);
            Console.WriteLine();
        }

        long unary = 0;
        for (int j = 0; j < repeatCount; j++)
        {
            var watch = Stopwatch.StartNew();
            const int OPCOUNT = 10000;
            for (int i = 0; i < OPCOUNT; i++)
            {
                using var call = client.UnaryAsync(new FooRequest { Value = i }, options);
                var result = await call.ResponseAsync;

                if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
            }
            unary += ShowTiming(nameof(client.UnaryAsync), watch, OPCOUNT);
        }
        Console.WriteLine();

        long clientStreaming = 0;
        for (int j = 0; j < repeatCount; j++)
        {
            var watch = Stopwatch.StartNew();
            using var call = client.ClientStreaming(options);
            const int OPCOUNT = 50000;
            int sum = 0;
            for (int i = 0; i < OPCOUNT; i++)
            {
                await call.RequestStream.WriteAsync(new FooRequest { Value = i });
                sum += i;
            }
            await call.RequestStream.CompleteAsync();
            var result = await call.ResponseAsync;
            if (result?.Value != sum) throw new InvalidOperationException("Incorrect response received: " + result);
            clientStreaming += ShowTiming(nameof(client.ClientStreaming), watch, OPCOUNT);
        }
        Console.WriteLine();

        long serverStreaming = 0;
        for (int j = 0; j < repeatCount; j++)
        {
            var watch = Stopwatch.StartNew();
            const int OPCOUNT = 50000;
            using var call = client.ServerStreaming(new FooRequest { Value = OPCOUNT }, options);
            int count = 0;
            while (await call.ResponseStream.MoveNext())
            {
                var result = call.ResponseStream.Current;
                if (result?.Value != count) throw new InvalidOperationException("Incorrect response received: " + result);
                count++;
            }
            if (count != OPCOUNT) throw new InvalidOperationException("Incorrect response count received: " + count);
            serverStreaming += ShowTiming(nameof(client.ServerStreaming), watch, OPCOUNT);
        }
        Console.WriteLine();

        long duplex = 0;
        for (int j = 0; j < repeatCount; j++)
        {
            var watch = Stopwatch.StartNew();
            const int OPCOUNT = 25000;
            using var call = client.Duplex(options);

            for (int i = 0; i < OPCOUNT; i++)
            {
                await call.RequestStream.WriteAsync(new FooRequest { Value = i });
                if (!await call.ResponseStream.MoveNext()) throw new InvalidOperationException("Duplex stream terminated early");
                var result = call.ResponseStream.Current;
                if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
            }
            await call.RequestStream.CompleteAsync();
            if (await call.ResponseStream.MoveNext()) throw new InvalidOperationException("Duplex stream ran over");
            duplex += ShowTiming(nameof(client.Duplex), watch, OPCOUNT);
        }
        Console.WriteLine();
        // store the average nanos-per-op
        timings.Add(caller, (AutoScale(unary / repeatCount), AutoScale(clientStreaming / repeatCount),
            AutoScale(serverStreaming / repeatCount), AutoScale(duplex / repeatCount)));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[{channel.Target}]: {ex.Message}");
    }
    finally
    {
        await channel.ShutdownAsync();
    }

    static long ShowTiming(string label, Stopwatch watch, int operations)
    {
        watch.Stop();
        var nanos = (watch.ElapsedTicks * 1_000_000_000) / Stopwatch.Frequency;
        Console.WriteLine($"{label} ×{operations}: {AutoScale(nanos)}, {AutoScale(nanos / operations)}/op");
        return nanos / operations;
    }
    static string AutoScale(long nanos)
    {
        long qty = nanos;
        if (qty < 10000) return $"{qty}ns";
        qty /= 1000;
        if (qty < 10000) return $"{qty}μs";
        qty /= 1000;
        if (qty < 10000) return $"{qty}ms";

        return TimeSpan.FromMilliseconds(qty).ToString();
    }
}