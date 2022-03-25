using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using static FooService;

RemoteCertificateValidationCallback trustAny = delegate { return true; };
Dictionary<string, (string unary, string clientStreamingBuffered, string clientStreamingNonBuffered, string serverStreamingBuffered, string serverStreamingNonBuffered, string duplex)> timings = new();

//using (var pipeServer = await ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10044), logger: ConsoleLogger.Debug).AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)))
//{
//    await Run(pipeServer);
//}
using (var namedPipe = await ConnectionFactory.ConnectNamedPipe("grpctest_buffer", logger: ConsoleLogger.Debug ).AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)))
{
    await Run(namedPipe);
}
//using (var namedPipePassThru = await ConnectionFactory.ConnectNamedPipe("grpctest_passthru", logger: ConsoleLogger.Debug).AsFrames(outputBufferSize: 0).CreateChannelAsync(TimeSpan.FromSeconds(5)))
//{
//    await Run(namedPipePassThru);
//}
//using (var namedPipeMerge = await ConnectionFactory.ConnectNamedPipe("grpctest_merge").AsFrames(true).CreateChannelAsync(TimeSpan.FromSeconds(5)))
//{
//    await Run(namedPipeMerge);
//}
using (var tcp = await ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10042)).AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)))
{
    await Run(tcp);
}
using (var tcpTls = await ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10043))
    .WithTls().AuthenticateAsClient("mytestserver", trustAny).AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)))
{
    await Run(tcpTls);
}

using (var namedPipeTls = await ConnectionFactory.ConnectNamedPipe("grpctest_tls").WithTls()
    .AuthenticateAsClient("mytestserver", trustAny).AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(50)))
{
    await Run(namedPipeTls);
}
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
    localServer.Bind<MyService>();
    using (var local = localServer.CreateLocalClient())
    {
        await Run(local);
    }
}


Console.WriteLine();
Console.WriteLine("| Scenario | Unary | Client-Streaming (b) | Client-Streaming (n) | Server-Streaming (b) | Server-Streaming (n) | Duplex |");
Console.WriteLine("| -------- | ----- | -------------------- | -------------------- | -------------------- | -------------------- | ------ |");
foreach (var (scenario, data) in timings.OrderBy(x => x.Key))
{
    Console.WriteLine($"| {scenario} | {data.unary} | {data.clientStreamingBuffered} | {data.clientStreamingNonBuffered} | {data.serverStreamingBuffered} | {data.serverStreamingNonBuffered} | {data.duplex} |");
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

        long clientStreamingBuffered = 0;
        for (int j = 0; j < repeatCount; j++)
        {
            var watch = Stopwatch.StartNew();
            using var call = client.ClientStreaming(options);
            const int OPCOUNT = 50000;
            int sum = 0;
            call.RequestStream.WriteOptions = MyService.Buffered;
            for (int i = 0; i < OPCOUNT; i++)
            {
                await call.RequestStream.WriteAsync(new FooRequest { Value = i });
                sum += i;
            }
            await call.RequestStream.CompleteAsync();
            var result = await call.ResponseAsync;
            if (result?.Value != sum) throw new InvalidOperationException("Incorrect response received: " + result);
            clientStreamingBuffered += ShowTiming(nameof(client.ClientStreaming) + " b", watch, OPCOUNT);
        }
        Console.WriteLine();

        long clientStreamingNonBuffered = 0;
        for (int j = 0; j < repeatCount; j++)
        {
            var watch = Stopwatch.StartNew();
            using var call = client.ClientStreaming(options);
            const int OPCOUNT = 50000;
            int sum = 0;
            call.RequestStream.WriteOptions = MyService.NonBuffered;
            for (int i = 0; i < OPCOUNT; i++)
            {
                await call.RequestStream.WriteAsync(new FooRequest { Value = i });
                sum += i;
            }
            await call.RequestStream.CompleteAsync();
            var result = await call.ResponseAsync;
            if (result?.Value != sum) throw new InvalidOperationException("Incorrect response received: " + result);
            clientStreamingNonBuffered += ShowTiming(nameof(client.ClientStreaming) + " nb", watch, OPCOUNT);
        }
        Console.WriteLine();

        long serverStreamingBuffered = 0;
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
            serverStreamingBuffered += ShowTiming(nameof(client.ServerStreaming) + " b", watch, OPCOUNT);
        }
        Console.WriteLine();

        long serverStreamingNonBuffered = 0;
        for (int j = 0; j < repeatCount; j++)
        {
            var watch = Stopwatch.StartNew();
            const int OPCOUNT = 50000;
            using var call = client.ServerStreaming(new FooRequest { Value = -OPCOUNT }, options);
            int count = 0;
            while (await call.ResponseStream.MoveNext())
            {
                var result = call.ResponseStream.Current;
                if (result?.Value != count) throw new InvalidOperationException("Incorrect response received: " + result);
                count++;
            }
            if (count != OPCOUNT) throw new InvalidOperationException("Incorrect response count received: " + count);
            serverStreamingNonBuffered += ShowTiming(nameof(client.ServerStreaming) + " nb", watch, OPCOUNT);
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
        timings.Add(caller, (
            AutoScale(unary / repeatCount, true),
            AutoScale(clientStreamingBuffered / repeatCount, true),
            AutoScale(clientStreamingNonBuffered / repeatCount, true),
            AutoScale(serverStreamingBuffered / repeatCount, true),
            AutoScale(serverStreamingNonBuffered / repeatCount, true),
            AutoScale(duplex / repeatCount, true)
        ));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[{channel.Target}]: {ex.Message}");
    }
    finally
    {
        try { await channel.ShutdownAsync(); }
        catch { }
    }

    static long ShowTiming(string label, Stopwatch watch, int operations)
    {
        watch.Stop();
        var nanos = (watch.ElapsedTicks * 1_000_000_000) / Stopwatch.Frequency;
        Console.WriteLine($"{label} ×{operations}: {AutoScale(nanos)}, {AutoScale(nanos / operations)}/op");
        return nanos / operations;
    }
    static string AutoScale(long nanos, bool forceNanos = false)
    {
        long qty = nanos;
        if (forceNanos) return $"{qty:###,###,##0}ns";
        if (qty < 10000) return $"{qty:#,##0}ns";
        qty /= 1000;
        if (qty < 10000) return $"{qty:#,##0}μs";
        qty /= 1000;
        if (qty < 10000) return $"{qty:#,##0}ms";

        return TimeSpan.FromMilliseconds(qty).ToString();
    }
}

sealed class ConsoleLogger : ILogger, IDisposable
{
    private static ILogger? s_Information, s_Debug, s_Error;
    public static ILogger Information => s_Information ??= new ConsoleLogger(LogLevel.Information);
    public static ILogger Debug => s_Debug ??= new ConsoleLogger(LogLevel.Debug);
    public static ILogger Error => s_Error ??= new ConsoleLogger(LogLevel.Error);

    private readonly LogLevel _level;
    private ConsoleLogger(LogLevel level) => _level = level;
    IDisposable ILogger.BeginScope<TState>(TState state) => this;

    void IDisposable.Dispose() { }

    bool ILogger.IsEnabled(LogLevel logLevel) => logLevel >= _level;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= _level)
        {
            (logLevel < LogLevel.Error ? Console.Out : Console.Error).WriteLine(formatter(state, exception));
        }
    }
}