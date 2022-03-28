using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.GrpcLite.Test;

[SetLoggingSource]
public class EndToEndTests : IClassFixture<TestServerHost>
{
    public enum ConnectionKind
    {
        Null,
        NamedPipeVanilla,
        NamedPipeBuffered,
        NamedPipeMerged,
    }

    private ValueTask<LiteChannel> ConnectAsync(ConnectionKind kind, out CallOptions options, CancellationToken cancellationToken)
    {
        options = default(CallOptions).WithCancellationToken(cancellationToken);
        switch (kind)
        {
            case ConnectionKind.Null:
                return new(Server.ConnectLocal());
            case ConnectionKind.NamedPipeVanilla:
                return ConnectionFactory.ConnectNamedPipe(Name, logger: Logger).AsFrames(mergeWrites: false, outputBufferSize: 0).CreateChannelAsync(cancellationToken);
            case ConnectionKind.NamedPipeMerged:
                return ConnectionFactory.ConnectNamedPipe(Name, logger: Logger).AsFrames(mergeWrites: true, outputBufferSize: 0).CreateChannelAsync(cancellationToken);
            case ConnectionKind.NamedPipeBuffered:
                return ConnectionFactory.ConnectNamedPipe(Name, logger: Logger).AsFrames(mergeWrites: false, outputBufferSize: -1).CreateChannelAsync(cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private readonly TestServerHost Server;
    private string Name => Server.Name;
    private ILogger Logger { get; }
    public EndToEndTests(TestServerHost server, ITestOutputHelper output)
    {
        Server = server;
        _output = output;
        Logger = _output.CreateLogger("");
    }

    private readonly ITestOutputHelper _output;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    CancellationTokenSource After() => After(DefaultTimeout);

    CancellationTokenSource After(TimeSpan timeout)
    {
        var cts = new CancellationTokenSource();
        //if (!Debugger.IsAttached)
        cts.CancelAfter(timeout);
        return cts;
    }

    private LogCapture? ServerLog() => Server.WithLog(_output);

    public static IEnumerable<object[]> StandardRuns()
    {
        foreach (ConnectionKind kind in Enum.GetValues<ConnectionKind>())
        {
            yield return new object[] { kind, 1 };
            yield return new object[] { kind, 10 };
            yield return new object[] { kind, 100 };
            yield return new object[] { kind, 1000 };
        }
    }

    [Theory]
    [MemberData(nameof(StandardRuns))]
    public async Task UnarySync(ConnectionKind kind, int count)
    {
        using var log = ServerLog();
        using var timeout = After();
        await using var client = await ConnectAsync(kind, out var options, timeout.Token);
        var proxy = new FooService.FooServiceClient(client);

        for (int i = 0; i < count; i++)
        {
            Logger.Information($"issuing {nameof(proxy.Unary)}...");
            var response = proxy.Unary(new FooRequest { Value = 42 }, options);
            Logger.Information($"got response: {response}");

            Assert.NotNull(response);
            Assert.Equal(42, response.Value);
        }
        timeout.Cancel();
    }

    [Theory]
    [MemberData(nameof(StandardRuns))]
    public async Task UnaryAsync(ConnectionKind kind, int count)
    {
        using var log = ServerLog();
        using var timeout = After();
        await using var client = await ConnectAsync(kind, out var options, timeout.Token);
        var proxy = new FooService.FooServiceClient(client);

        for (int i = 0; i < count; i++)
        {
            Logger.Debug($"issuing {nameof(proxy.UnaryAsync)}...");
            using var call = proxy.UnaryAsync(new FooRequest { Value = 42 }, options);
            Logger.Debug("awaiting response...");
            var response = await call.ResponseAsync;
            Logger.Debug($"got response: {response}");

            Assert.NotNull(response);
            Assert.Equal(42, response.Value);
        }

        timeout.Cancel();
    }

    [Theory]
    [MemberData(nameof(StandardRuns))]
    public async Task SendReceiveMetadata(ConnectionKind kind, int count)
    {
        using var log = ServerLog();
        using var timeout = After();
        await using var client = await ConnectAsync(kind, out var options, timeout.Token);
        var proxy = new FooService.FooServiceClient(client);

        for (int i = 0; i < count; i++)
        {
            var s = i.ToString();
            options = options.WithHeaders(new Metadata
            {
                new Metadata.Entry("i", s)
            });
            Logger.Debug($"issuing {nameof(proxy.UnaryAsync)}...");
            using var call = proxy.UnaryAsync(new FooRequest { Value = 42 }, options);
            Logger.Debug("awaiting response...");

            var metadata = await call.ResponseHeadersAsync;
            var entry = Assert.Single(metadata);
            Assert.Equal("header-i", entry.Key);
            Assert.Equal(s, entry.Value);

            var response = await call.ResponseAsync;
            Logger.Debug($"got response: {response}");

            Assert.NotNull(response);
            Assert.Equal(42, response.Value);

            metadata = call.GetTrailers();
            entry = Assert.Single(metadata);
            Assert.Equal("trailer-i", entry.Key);
            Assert.Equal(s, entry.Value);
        }

        timeout.Cancel();
    }

    [Theory]
    [MemberData(nameof(StandardRuns))]
    public async Task Duplex(ConnectionKind kind, int count)
    {
        using var log = ServerLog();
        using var timeout = After();
        await using var client = await ConnectAsync(kind, out var options, timeout.Token);

        var proxy = new FooService.FooServiceClient(client);

        using var call = proxy.Duplex(options);
        for (int i = 0; i < count; i++)
        {
            Logger.Information($"writing {i}...");
            await call.RequestStream.WriteAsync(new FooRequest { Value = i });
        }
        await call.RequestStream.CompleteAsync();
        Logger.Information($"all writes complete");
        for (int i = 0; i < count; i++)
        {
            Logger.Information($"reading {i}...");
            Assert.True(await call.ResponseStream.MoveNext(timeout.Token), nameof(call.ResponseStream.MoveNext));
            Assert.Equal(i, call.ResponseStream.Current.Value);
        }
        Assert.False(await call.ResponseStream.MoveNext(timeout.Token), nameof(call.ResponseStream.MoveNext));
        Logger.Information($"all reads complete");

        timeout.Cancel();
    }

    [Theory]
    [MemberData(nameof(StandardRuns))]
    public async Task ServerStreaming_Buffered(ConnectionKind kind, int count)
    {
        using var log = ServerLog();
        using var timeout = After();
        await using var client = await ConnectAsync(kind, out var options, timeout.Token);

        var proxy = new FooService.FooServiceClient(client);

        using var call = proxy.ServerStreaming(new FooRequest { Value = count }, options);
        count = Math.Abs(count); // we use -ve value to say "don't buffer"
        for (int i = 0; i < count; i++)
        {
            Logger.Information($"reading {i}...");
            Assert.True(await call.ResponseStream.MoveNext(timeout.Token), nameof(call.ResponseStream.MoveNext));
            Assert.Equal(i, call.ResponseStream.Current.Value);
        }
        Assert.False(await call.ResponseStream.MoveNext(timeout.Token), nameof(call.ResponseStream.MoveNext));
        Logger.Information($"all reads complete");

        timeout.Cancel();
    }
    [Theory]
    [MemberData(nameof(StandardRuns))]
    public Task ServerStreaming_NonBuffered(ConnectionKind kind, int count)
        => ServerStreaming_Buffered(kind, -count);


    [Theory]
    [MemberData(nameof(StandardRuns))]
    public async Task ClientStreaming_Buffered(ConnectionKind kind, int count)
    {
        using var log = ServerLog();
        using var timeout = After();
        await using var client = await ConnectAsync(kind, out var options, timeout.Token);
        var proxy = new FooService.FooServiceClient(client);

        using var call = proxy.ClientStreaming(options);
        int sum = 0;

        if (count < 0)
        {
            call.RequestStream.WriteOptions = MyService.NonBuffered;
            count = -count;
        }
        else
        {
            call.RequestStream.WriteOptions = MyService.Buffered;
        }
        for (int i = 0; i < count; i++)
        {
            await call.RequestStream.WriteAsync(new FooRequest { Value = i });
            sum += i;
        }
        await call.RequestStream.CompleteAsync();
        var resp = await call.ResponseAsync;
        Assert.Equal(sum, resp.Value);

        timeout.Cancel();
    }
    [Theory]
    [MemberData(nameof(StandardRuns))]
    public Task ClientStreaming_NonBuffered(ConnectionKind kind, int count)
        => ClientStreaming_Buffered(kind, -count);


}

public sealed class LogCapture : IDisposable
{
    private readonly TestServerHost host;
    private readonly ITestOutputHelper output;

    public LogCapture(TestServerHost host, ITestOutputHelper output)
    {
        this.host = host;
        this.output = output;
        host.Log += OnLog;
    }

    private void OnLog(string message) => output?.WriteLine(message);

    public void Dispose() => host.Log -= OnLog;
}

public class TestServerHost : IDisposable, ILogger
{
    public event Action<string>? Log;
    public LogCapture? WithLog(ITestOutputHelper output)
        => output is null ? default : new LogCapture(this, output);

    private readonly LiteServer _server;
    public string Name { get; }

    public LiteChannel ConnectLocal() => _server.CreateLocalClient();

    public TestServerHost()
    {

        Name = Guid.NewGuid().ToString();
        _server = new LiteServer(logger: this);
        var svc = new MyService();
        svc.Log += message => this.Information(message);
        _server.Bind<MyService>(svc);

        Debug.WriteLine($"starting listener {Name}...");
        _server.ListenAsync(ConnectionFactory.ListenNamedPipe(Name, logger: this));
    }

    public void Dispose() => _server.Stop();

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Log?.Invoke(BasicLogger.Format(logLevel, eventId, state, exception, formatter, "", 0));

    bool ILogger.IsEnabled(LogLevel logLevel) => Log is not null;

    IDisposable ILogger.BeginScope<TState>(TState state) => null!;
}

public class MyService : FooService.FooServiceBase
{
    public event Action<string>? Log;
    
    private void OnLog(string message) => Log?.Invoke(message);
    public override async Task<FooResponse> Unary(FooRequest request, ServerCallContext context)
    {
        OnLog($"unary starting; received {request.Value}");
        await Task.Yield();
        OnLog("unary returning");
        var headers = context.RequestHeaders;
        if (headers is not null && headers.Count != 0)
        {
            var respHeaders = new Metadata();
            foreach (var e in headers)
            {
                respHeaders.Add(new Metadata.Entry("header-" + e.Key, e.Value));
            }
            await context.WriteResponseHeadersAsync(respHeaders);

            foreach (var e in headers)
            {
                context.ResponseTrailers.Add(new Metadata.Entry("trailer-" + e.Key, e.Value));
            }
        }
        return new FooResponse { Value = request.Value };
    }

    public override async Task Duplex(IAsyncStreamReader<FooRequest> requestStream, IServerStreamWriter<FooResponse> responseStream, ServerCallContext context)
    {
        OnLog("duplex starting");
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            var value = requestStream.Current;
            OnLog($"duplex received {value.Value}");
            await responseStream.WriteAsync(new FooResponse {  Value = value.Value });
        }
        OnLog("duplex returning");
    }

    public static readonly WriteOptions
        Buffered = new WriteOptions(WriteFlags.BufferHint),
        NonBuffered = new WriteOptions(0);

    public override async Task ServerStreaming(FooRequest request, IServerStreamWriter<FooResponse> responseStream, ServerCallContext context)
    {
        var count = request.Value;
        if (count < 0)
        {
            count = -count;
            responseStream.WriteOptions = NonBuffered;
        }
        else
        {
            responseStream.WriteOptions = Buffered;
        }
        for (int i = 0; i < count; i++)
        {
            await responseStream.WriteAsync(new FooResponse { Value = i });
        }
    }
    public override async Task<FooResponse> ClientStreaming(IAsyncStreamReader<FooRequest> requestStream, ServerCallContext context)
    {
        OnLog("client-streaming starting");
        int sum = 0;
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            var value = requestStream.Current;
            OnLog($"client-streaming received {value.Value}");
            sum += value.Value;
        }
        OnLog("client-streaming returning");
        return new FooResponse { Value = sum };
    }
}
