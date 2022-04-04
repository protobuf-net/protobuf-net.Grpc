using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Lite;
using protobuf_net.GrpcLite.Test;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

static class Program
{
    static readonly Dictionary<string, (string unarySequential, string unaryConcurrent, string clientStreamingBuffered, string clientStreamingNonBuffered, string serverStreamingBuffered, string serverStreamingNonBuffered, string duplex)> timings = new();

    [Flags]
    public enum Tests
    {
        None = 0,
        NamedPipe = 1 << 0,
        NamedPipeTls = 1 << 1,
        NamedPipePassThru = 1 << 2,
        NamedPipeMerge = 1 << 3,
        Tcp = 1 << 4,
        TcpTls = 1 << 5,
        TcpKestrel = 1 << 6,
        Unmanaged = 1 << 7,
        Local = 1 << 8,
        Managed = 1 << 9,
        ManagedTls = 1 << 10,
        TcpSAEA = 1 << 11,
        TcpTlsClientCert = 1 << 12,
    }
    [Flags]
    public enum CodeStyle
    {
        None = 0,
        ContractFirst = 1 << 0,
        CodeFirst = 1 << 1,
        Parallel = 1 << 2,
    }
    static async Task<int> Main(string[] args)
    {
        try
        {
            var userCert = new X509Certificate2("fred.pfx", "password");
            RemoteCertificateValidationCallback trustAny = (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
                =>
            {
                Console.WriteLine($"Received cert from server '{certificate?.Subject}'; {sslPolicyErrors}; trusting...");
                return true;
            };
            LocalCertificateSelectionCallback selectCert = (object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate? remoteCertificate, string[] acceptableIssuers)
                =>
            {
                Console.WriteLine($"Being challenged for cert from server '{remoteCertificate?.Subject}' for {targetHost}; providing {userCert?.Subject}...");
                return userCert!;
            };
            Tests tests = Tests.None;
            CodeStyle styles = CodeStyle.None;
            if (args is not null)
            {
                tests = Tests.None;
                styles = CodeStyle.None;
                foreach (var arg in args)
                {
                    if (Enum.TryParse(arg, true, out Tests test))
                        tests |= test;
                    else if (Enum.TryParse(arg, true, out CodeStyle style))
                        styles |= style;
                    else
                    {
                        foreach (var val in Enum.GetValues(typeof(Tests)))
                        {
                            Console.WriteLine(val);
                        }
                        foreach (var val in Enum.GetValues(typeof(CodeStyle)))
                        {
                            Console.WriteLine(val);
                        }
                        return -1;
                    }
                }
            }
            if (styles == CodeStyle.None)
            {
                styles = CodeStyle.CodeFirst | CodeStyle.ContractFirst | CodeStyle.Parallel;
            }
            if (tests == Tests.None)
            {
                // reasonable defaults
                tests = Tests.NamedPipe | Tests.Local | Tests.Tcp | Tests.Unmanaged | Tests.TcpTls | Tests.TcpTlsClientCert | Tests.NamedPipeTls
                    | Tests.ManagedTls;
#if NET472
                tests |= Tests.TcpSAEA; // something glitching here on net6; probably fixable
#else
                tests |= Tests.Managed; // net472 doesn't like non-TLS gRPC, even with the feature-flag set
#endif
            }
            Console.WriteLine($"Running tests: {tests} for: {styles}");

            async Task ExecuteAsync<T>(Tests test, Func<ValueTask<T>> channelCreator, int repeatCount = 5, bool runClientStreaming = true) where T : ChannelBase
            {
                T? channel = null;
                try
                {
                    if ((tests & test) != 0)
                    {
                        channel = await channelCreator();
                        if ((styles & CodeStyle.ContractFirst) != 0)
                        {
                            await RunContractFirst(channel, test, repeatCount, runClientStreaming);
                        }
                        if ((styles & CodeStyle.CodeFirst) != 0)
                        {
                            await RunCodeFirst(channel, test, repeatCount, runClientStreaming);
                        }
                        if ((styles & CodeStyle.Parallel) != 0)
                        {
                            await RunParallel(channel, test, repeatCount, runClientStreaming);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
                finally
                {
                    if (channel is not null)
                    {
                        try { await channel.ShutdownAsync(); } catch { }
                        switch (channel)
                        {
                            case IAsyncDisposable ad:
                                try { await ad.DisposeAsync(); } catch { }
                                break;
                            case IDisposable d:
                                try { d.Dispose(); } catch { }
                                break;
                        }
                    }
                }
            }

            GrpcChannelOptions grpcChannelOptions = new();
#if NET472
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            grpcChannelOptions.HttpHandler = new WinHttpHandler();
            //const bool ManagedClientStreaming = false;
#else
            //const bool ManagedClientStreaming = true;
#endif
            const bool ManagedClientStreaming = true; // always try, even if we think it is doomed

            LiteServer? localServer = null;
            if ((tests & Tests.Local) != 0)
            {
                var svc1 = new MyContractFirstService();
                var svc2 = new MyCodeFirstService();
                localServer = new LiteServer();
                localServer.ServiceBinder.Bind(svc1);
                localServer.ServiceBinder.Intercept(new MyInterceptor()).AddCodeFirst(svc2);
            }

            await ExecuteAsync(Tests.TcpKestrel, () => ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10044)).AsStream().AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.NamedPipe, () => ConnectionFactory.ConnectNamedPipe("grpctest_buffer").AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.NamedPipePassThru, () => ConnectionFactory.ConnectNamedPipe("grpctest_passthru").AsFrames(outputBufferSize: 0).CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.NamedPipeMerge, () => ConnectionFactory.ConnectNamedPipe("grpctest_merge").AsFrames(true).CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.Tcp, () => ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10042)).AsStream().AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.TcpSAEA, () => ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10042)).AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.TcpTls, () => ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10043)).AsStream().WithTls(trustAny).AuthenticateAsClient("mytestserver").AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.TcpTlsClientCert, () => ConnectionFactory.ConnectSocket(new IPEndPoint(IPAddress.Loopback, 10045)).AsStream().WithTls(trustAny, selectCert).AuthenticateAsClient("mytestserver").AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(5)));
            await ExecuteAsync(Tests.NamedPipeTls, () => ConnectionFactory.ConnectNamedPipe("grpctest_tls").WithTls(trustAny).AuthenticateAsClient("mytestserver").AsFrames().CreateChannelAsync(TimeSpan.FromSeconds(50)));
            await ExecuteAsync(Tests.Managed, () => new ValueTask<GrpcChannel>(GrpcChannel.ForAddress("http://localhost:5074", grpcChannelOptions)), runClientStreaming: ManagedClientStreaming);
            await ExecuteAsync(Tests.ManagedTls, () => new ValueTask<GrpcChannel>(GrpcChannel.ForAddress("https://localhost:7074", grpcChannelOptions)), runClientStreaming: ManagedClientStreaming);
            await ExecuteAsync(Tests.Unmanaged, () => new ValueTask<Channel>(new Channel("localhost", 5074, ChannelCredentials.Insecure)));
            await ExecuteAsync(Tests.Local, () => new ValueTask<LiteChannel>(localServer!.CreateLocalClient()));

            if (localServer is not null)
            {
                localServer?.Stop();
                localServer = null;
            }

            Console.WriteLine();
            Console.WriteLine("| Scenario | Unary (seq) | Unary (con) | Client-Streaming (b) | Client-Streaming (n) | Server-Streaming (b) | Server-Streaming (n) | Duplex |");
            Console.WriteLine("| -------- | ----------- | ------------| -------------------- | -------------------- | -------------------- | -------------------- | ------ |");
            foreach (var pair in timings.OrderBy(x => x.Key))
            {
                var scenario = pair.Key;
                var data = pair.Value;
                Console.WriteLine($"| {scenario} | {data.unarySequential} | {data.unaryConcurrent} | {data.clientStreamingBuffered} | {data.clientStreamingNonBuffered} | {data.serverStreamingBuffered} | {data.serverStreamingNonBuffered} | {data.duplex} |");
            }
            timings.Clear();
            Console.WriteLine();
            Console.WriteLine("A: contract-first (.proto/protoc), B: code-first (protobuf-net)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("press any key");
            Console.ReadKey();
            return -1;
        }
    }
    static Task RunParallel(Func<Task> operation, int times = 1)
    {
        if (times == 0) return Task.CompletedTask;
        if (times == 1) return operation();
        var tasks = new Task[times];
        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = operation();
        return Task.WhenAll(tasks);
    }
    async static Task RunContractFirst(ChannelBase channel, Tests test, int repeatCount, bool runClientStreaming = true)
    {
        try
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            var options = new CallOptions(cancellationToken: cts.Token);

            FooService.FooServiceClient client;
            {
                var invoker = channel.CreateCallInvoker();
                Console.WriteLine($"Connecting to {channel.Target} ({test}, {invoker.GetType().Name})...");
                client = new FooService.FooServiceClient(invoker);

                using var call = client.UnaryAsync(new FooRequest { Value = 42 }, options);
                var result = await call.ResponseAsync;
                if (result?.Value != 42) throw new InvalidOperationException("Incorrect response received: " + result);
                Console.WriteLine("(validated)");
            }

            long unarySequential = 0, unaryConcurrent = 0, clientStreamingBuffered = 0, clientStreamingNonBuffered = 0, serverStreamingBuffered = 0, serverStreamingNonBuffered = 0, duplex = 0;

            try
            {
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
                    unarySequential += ShowTiming(nameof(client.UnaryAsync) + " (sequential)", watch, OPCOUNT);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                unarySequential = int.MinValue;
            }
            Console.WriteLine();


            try
            {
                for (int j = 0; j < repeatCount; j++)
                {
                    var watch = Stopwatch.StartNew();
                    const int OPCOUNT = 1000, CONCURRENCY = 10;
                    await RunParallel(async () =>
                    {
                        for (int i = 0; i < OPCOUNT; i++)
                        {
                            using var call = client.UnaryAsync(new FooRequest { Value = i }, options);
                            var result = await call.ResponseAsync;

                            if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
                        }
                    }, CONCURRENCY);
                    unaryConcurrent += ShowTiming(nameof(client.UnaryAsync) + " (concurrent)", watch, OPCOUNT * CONCURRENCY);
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                unaryConcurrent = int.MinValue;
            }

            if (runClientStreaming)
            {
                try
                {
                    for (int j = 0; j < repeatCount; j++)
                    {
                        var watch = Stopwatch.StartNew();
                        using var call = client.ClientStreaming(options);
                        const int OPCOUNT = 50000;
                        int sum = 0;
                        call.RequestStream.WriteOptions = MyContractFirstService.Buffered;
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
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    clientStreamingBuffered = int.MinValue;
                }
                Console.WriteLine();

                try
                {
                    for (int j = 0; j < repeatCount; j++)
                    {
                        var watch = Stopwatch.StartNew();
                        using var call = client.ClientStreaming(options);
                        const int OPCOUNT = 50000;
                        int sum = 0;
                        call.RequestStream.WriteOptions = MyContractFirstService.NonBuffered;
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
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    clientStreamingNonBuffered = int.MinValue;
                }
                Console.WriteLine();
            }
            else
            {
                clientStreamingNonBuffered = clientStreamingBuffered = int.MinValue;
            }

            try
            {
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
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                serverStreamingBuffered = int.MinValue;
            }
            Console.WriteLine();

            try
            {
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
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                serverStreamingNonBuffered = int.MinValue;
            }
            Console.WriteLine();

            if (runClientStreaming)
            {
                try
                {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                    static async Task WriteAsync(IClientStreamWriter<FooRequest> output, int opCount)
                    {
                        try
                        {
                            for (int i = 0; i < opCount; i++)
                                await output.WriteAsync(new FooRequest { Value = i });
                            await output.CompleteAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex.Message);
                        }
                    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously


                    for (int j = 0; j < repeatCount; j++)
                    {
                        var watch = Stopwatch.StartNew();
                        const int OPCOUNT = 10000;
                        using var call = client.Duplex(options);

                        var writer = Task.Run(() => WriteAsync(call.RequestStream, OPCOUNT));
                        int i = 0;
                        while (await call.ResponseStream.MoveNext())
                        {
                            var result = call.ResponseStream.Current;
                            if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
                            i++;
                        }
                        await writer;
                        if (i != OPCOUNT) throw new InvalidOperationException("Duplex length mismatch");
                        duplex += ShowTiming(nameof(client.Duplex), watch, OPCOUNT);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    duplex = int.MinValue;
                }
                Console.WriteLine();
            }
            else
            {
                duplex = int.MinValue;
            }

            // store the average nanos-per-op
            timings.Add("A:" + test.ToString(), (
                AutoScale(unarySequential / repeatCount, true),
                AutoScale(unaryConcurrent / repeatCount, true),
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
            timings["A:" + test.ToString()] = ("err", "err", "err", "err", "err", "err", "err");
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
            if (nanos < 0) return "n/a";
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
    async static Task RunParallel(ChannelBase channel, Tests test, int repeatCount, bool runClientStreaming = true)
    {
        try
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            CallContext options = new CallOptions(cancellationToken: cts.Token);

            IMyService client;
            {
                var invoker = channel.CreateCallInvoker();
                Console.WriteLine($"Connecting to {channel.Target} ({test}, {invoker.GetType().Name})...");
                client = channel.CreateGrpcService<IMyService>();

                var result = await client.UnaryAsync(new CodeFirstRequest { Value = 42 }, options);
                if (result?.Value != 42) throw new InvalidOperationException("Incorrect response received: " + result);
                if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                Console.WriteLine("(validated)");
            }

            long unaryConcurrent = 0;
            const long unarySequential = -1, clientStreamingBuffered = -1, clientStreamingNonBuffered = -1, serverStreamingBuffered = -1, serverStreamingNonBuffered = -1, duplex = -1;

            try
            {
                for (int j = 0; j < repeatCount; j++)
                {
                    var watch = Stopwatch.StartNew();
                    const int OPCOUNT = 10, CONCURRENCY = 5000;
                    await RunParallel(async () =>
                    {
                        await Task.Yield();
                        for (int i = 0; i < OPCOUNT; i++)
                        {
                            var result = await client.UnaryAsync(new CodeFirstRequest { Value = i }, options);

                            if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
                            if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                        }
                    }, CONCURRENCY);
                    unaryConcurrent += ShowTiming(nameof(client.UnaryAsync) + " (concurrent)", watch, OPCOUNT * CONCURRENCY);
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                unaryConcurrent = int.MinValue;
            }

            // store the average nanos-per-op
            timings.Add("B:" + test.ToString(), (
                AutoScale(unarySequential / repeatCount, true),
                AutoScale(unaryConcurrent / repeatCount, true),
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
            timings["B:" + test.ToString()] = ("err", "err", "err", "err", "err", "err", "err");
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
            if (nanos < 0) return "n/a";
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
    async static Task RunCodeFirst(ChannelBase channel, Tests test, int repeatCount, bool runClientStreaming = true)
    {
        try
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            CallContext options = new CallOptions(cancellationToken: cts.Token);

            IMyService client;
            {
                var invoker = channel.CreateCallInvoker();
                Console.WriteLine($"Connecting to {channel.Target} ({test}, {invoker.GetType().Name})...");
                client = channel.CreateGrpcService<IMyService>();

                var result = await client.UnaryAsync(new CodeFirstRequest { Value = 42 }, options);
                if (result?.Value != 42) throw new InvalidOperationException("Incorrect response received: " + result);
                if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                Console.WriteLine("(validated)");
            }

            long unarySequential = 0, unaryConcurrent = 0, clientStreamingBuffered = 0, clientStreamingNonBuffered = 0, serverStreamingBuffered = 0, serverStreamingNonBuffered = 0, duplex = 0;

            try
            {
                for (int j = 0; j < repeatCount; j++)
                {
                    var watch = Stopwatch.StartNew();
                    const int OPCOUNT = 10000;
                    for (int i = 0; i < OPCOUNT; i++)
                    {
                        var result = await client.UnaryAsync(new CodeFirstRequest { Value = i }, options);

                        if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
                        if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                    }
                    unarySequential += ShowTiming(nameof(client.UnaryAsync) + " (sequential)", watch, OPCOUNT);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                unarySequential = int.MinValue;
            }
            Console.WriteLine();


            try
            {
                for (int j = 0; j < repeatCount; j++)
                {
                    var watch = Stopwatch.StartNew();
                    const int OPCOUNT = 1000, CONCURRENCY = 10;
                    await RunParallel(async () =>
                    {
                        for (int i = 0; i < OPCOUNT; i++)
                        {
                            var result = await client.UnaryAsync(new CodeFirstRequest { Value = i }, options);

                            if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
                            if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                        }
                    }, CONCURRENCY);
                    unaryConcurrent += ShowTiming(nameof(client.UnaryAsync) + " (concurrent)", watch, OPCOUNT * CONCURRENCY);
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                unaryConcurrent = int.MinValue;
            }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            static async IAsyncEnumerable<CodeFirstRequest> Generate(int opCount)
            {
                for (int i = 0; i < opCount; i++)
                    yield return new CodeFirstRequest { Value = i };
            }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

            clientStreamingBuffered = int.MinValue;
            if (runClientStreaming)
            {
                try
                {
                    const int OPCOUNT = 50000;
                    var sum = Enumerable.Range(0, OPCOUNT).Sum();
                    for (int j = 0; j < repeatCount; j++)
                    {
                        var watch = Stopwatch.StartNew();
                        var result = await client.ClientStreamingAsync(Generate(OPCOUNT), options);
                        if (result?.Value != sum) throw new InvalidOperationException("Incorrect response received: " + result);
                        if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                        clientStreamingNonBuffered += ShowTiming(nameof(client.ClientStreamingAsync) + " b", watch, OPCOUNT);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    clientStreamingNonBuffered = int.MinValue;
                }
                Console.WriteLine();
            }
            else
            {
                clientStreamingNonBuffered = int.MinValue;
            }

            serverStreamingBuffered = int.MinValue;
            try
            {
                for (int j = 0; j < repeatCount; j++)
                {
                    var watch = Stopwatch.StartNew();
                    const int OPCOUNT = 50000;
                    int count = 0;
                    await foreach (var result in client.ServerStreamingAsync(new CodeFirstRequest { Value = OPCOUNT }, options))
                    {
                        if (result?.Value != count) throw new InvalidOperationException("Incorrect response received: " + result);
                        if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                        count++;
                    }
                    if (count != OPCOUNT) throw new InvalidOperationException("Incorrect response count received: " + count);
                    serverStreamingNonBuffered += ShowTiming(nameof(client.ServerStreamingAsync) + " b", watch, OPCOUNT);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                serverStreamingNonBuffered = int.MinValue;
            }
            Console.WriteLine();

            if (runClientStreaming)
            {
                try
                {
                    for (int j = 0; j < repeatCount; j++)
                    {
                        var watch = Stopwatch.StartNew();
                        const int OPCOUNT = 10000;

                        int i = 0;
                        await foreach (var result in client.DuplexAsync(Generate(OPCOUNT), options))
                        {
                            if (result?.Value != i) throw new InvalidOperationException("Incorrect response received: " + result);
                            if (!result.Done) throw new InvalidOperationException("Interceptor failed!");
                            i++;
                        }
                        if (i != OPCOUNT) throw new InvalidOperationException("Duplex length mismatch");

                        duplex += ShowTiming(nameof(client.DuplexAsync), watch, OPCOUNT);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    duplex = int.MinValue;
                }
                Console.WriteLine();
            }
            else
            {
                duplex = int.MinValue;
            }

            // store the average nanos-per-op
            timings.Add("B:" + test.ToString(), (
                AutoScale(unarySequential / repeatCount, true),
                AutoScale(unaryConcurrent / repeatCount, true),
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
            timings["B:" + test.ToString()] = ("err", "err", "err", "err", "err", "err", "err");
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
            if (nanos < 0) return "n/a";
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


}