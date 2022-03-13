using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.GrpcLite.Test
{
    public class EndToEndTests : IClassFixture<TestServerHost>
    {
        private readonly TestServerHost Server;
        private string Name => Server.Name;
        private ILogger Logger(string name) => _output.CreateLogger(name);
        public EndToEndTests(TestServerHost server, ITestOutputHelper output)
        {
            Server = server;
            _output = output;
        }

        private readonly ITestOutputHelper _output;

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
        CancellationTokenSource After() => After(DefaultTimeout);

        CancellationTokenSource After(TimeSpan timeout)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            return cts;
        }

        private LogCapture? ServerLog(string prefix = "server")
            => Server.WithLog(_output, prefix);

        [Fact]
        public async Task CanCallUnarySync()
        {
            using var log = ServerLog();
            using var timeout = After();
            await using var client = await ConnectionFactory.ConnectNamedPipe(Name, logger: Logger("client")).CreateChannelAsync(timeout.Token);
            var proxy = new FooService.FooServiceClient(client);

            var response = proxy.Unary(new FooRequest { Value = 42 }, default(CallOptions).WithCancellationToken(timeout.Token));

            Assert.NotNull(response);
            Assert.Equal(42, response.Value);
            timeout.Cancel();
        }

        [Fact]
        public async Task CanCallUnaryAsync()
        {
            using var log = ServerLog();
            using var timeout = After();
            await using var client = await ConnectionFactory.ConnectNamedPipe(Name, logger: Logger("client")).CreateChannelAsync(timeout.Token);

            var proxy = new FooService.FooServiceClient(client);

            using var call = proxy.UnaryAsync(new FooRequest { Value = 42 }, default(CallOptions).WithCancellationToken(timeout.Token));
            var response = await call.ResponseAsync;

            Assert.NotNull(response);
            Assert.Equal(42, response.Value);
            timeout.Cancel();
        }

        [Fact]
        public async Task CanCallDuplexAsync()
        {
            using var log = ServerLog();
            using var timeout = After();
            await using var client = await ConnectionFactory.ConnectNamedPipe(Name, logger: Logger("client")).CreateChannelAsync(timeout.Token);
            var proxy = new FooService.FooServiceClient(client);
            
            using var call = proxy.Duplex();
            for (int i = 0; i < 10; i++)
            {
                await call.RequestStream.WriteAsync(new FooRequest { Value = i });
                // await pipe.RequestStream.CompleteAsync();
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.True(await call.ResponseStream.MoveNext(timeout.Token));
                Assert.Equal(i, call.ResponseStream.Current.Value);
            }

            timeout.Cancel();
        }
    }

    public sealed class LogCapture : IDisposable
    {
        private readonly TestServerHost host;
        private readonly ITestOutputHelper output;
        private readonly string prefix;

        public LogCapture(TestServerHost host, ITestOutputHelper output, string prefix)
        {
            this.host = host;
            this.output = output;
            this.prefix = prefix;
            host.Log += OnLog;
        }

        private void OnLog(string message) => output.WriteLine("[" + prefix + "] " + message);

        public void Dispose() => host.Log -= OnLog;
    }

    public class TestServerHost : IDisposable, ILogger
    {
        public event Action<string>? Log;
        public LogCapture? WithLog(ITestOutputHelper output, string prefix)
            => output is null ? default : new LogCapture(this, output, prefix);

        private readonly CancellationTokenSource _cts;
        public string Name { get; }

        public TestServerHost()
        {
            _cts = new CancellationTokenSource();

            Name = Guid.NewGuid().ToString();
            var server = new NamedPipeServer(logger: this);
            var svc = new MyService();
            svc.Log += message => Log?.Invoke(message);
            server.ManualBind<MyService>(svc);

            _ = server.ListenOneAsync(Name, _cts.Token);
        }

        public void Dispose() => _cts.Cancel();

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Log?.Invoke(formatter(state, exception));

        bool ILogger.IsEnabled(LogLevel logLevel) => Log is not null;

        IDisposable ILogger.BeginScope<TState>(TState state) => null!;
    }

    class MyService : FooService.FooServiceBase
    {
        public event Action<string>? Log;

        private void OnLog(string message) => Log?.Invoke(message);
        public override async Task<FooResponse> Unary(FooRequest request, ServerCallContext context)
        {
            OnLog($"unary starting; received {request.Value}");
            await Task.Yield();
            OnLog("unary returning");
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
    }
}
