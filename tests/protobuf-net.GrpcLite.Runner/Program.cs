using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;
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

{
    using var localServer = new LiteServer();
    localServer.ManualBind<MyService>();
    using (var local = localServer.CreateLocalClient())
    {
        await Run(local);
    }
}


//using (var namedPipe = await ConnectionFactory.ConnectNamedPipe("grpctest_merge").AsFrames(true).CreateChannelAsync(TimeSpan.FromSeconds(5)))
//{
//    await Run(namedPipe);
//}
//using (var namedPipe = await ConnectionFactory.ConnectNamedPipe("grpctest_nomerge").AsFrames(true).CreateChannelAsync(TimeSpan.FromSeconds(5)))
//{
//    await Run(namedPipe);
//}

// THIS ONE NEEDS INVESTIGATION; TLS doesn't handshake
//using (var namedPipeTls = await ConnectionFactory.ConnectNamedPipe("grpctest_tls").WithTls().AuthenticateAsClient("mytestserver").AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(50)))
//{
//    await Run(namedPipeTls);
//}


static async Task Run(ChannelBase channel, [CallerArgumentExpression("channel")] string caller = "", int repeatCount = 10)
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
        }

        //for (int j = 0; j < repeatCount; j++)
        //{
        //    var watch = Stopwatch.StartNew();
        //    const int OPCOUNT = 10000;
        //    for (int i = 0; i < OPCOUNT; i++)
        //    {
        //        using var call = client.UnaryAsync(new FooRequest { Value = i }, options);
        //        var result = await call.ResponseAsync;

        //        if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
        //    }
        //    ShowTiming(nameof(client.UnaryAsync), watch, OPCOUNT);
        //}

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
            Console.WriteLine("d");
            var result = await call.ResponseAsync; // FIX: not completing
            Console.WriteLine("e");
            if (result?.Value != sum) throw new InvalidOperationException("Incorrect response received: " + result);
            ShowTiming(nameof(client.ClientStreaming), watch, OPCOUNT);
        }

        //for (int j = 0; j < repeatCount; j++)
        //{
        //    var watch = Stopwatch.StartNew();
        //    const int OPCOUNT = 100 * 1024;
        //    using var call = client.ServerStreaming(new FooRequest { Value = OPCOUNT },options);
        //    int count = 0;
        //    while (await call.ResponseStream.MoveNext())
        //    {
        //        var result = call.ResponseStream.Current;
        //        if (result?.Value != count) throw new InvalidOperationException("Incorrect response received: " + result);
        //        count++;
        //    }
        //    if (count != OPCOUNT) throw new InvalidOperationException("Incorrect response count received: " + count);
        //    ShowTiming(nameof(client.ServerStreaming), watch, OPCOUNT);
        //}

        //for (int j = 0; j < repeatCount; j++)
        //{
        //    var watch = Stopwatch.StartNew();
        //    const int OPCOUNT = 20 * 1024;
        //    using var call = client.Duplex(options);

        //    for (int i = 0; i < OPCOUNT; i++)
        //    {
        //        await call.RequestStream.WriteAsync(new FooRequest { Value = i });
        //        if (!await call.ResponseStream.MoveNext()) throw new InvalidOperationException("Duplex stream terminated early");
        //        var result = call.ResponseStream.Current;
        //        if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
        //    }
        //    await call.RequestStream.CompleteAsync();
        //    if (await call.ResponseStream.MoveNext()) throw new InvalidOperationException("Duplex stream ran over");
        //    ShowTiming(nameof(client.Duplex), watch, OPCOUNT);
        //}
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