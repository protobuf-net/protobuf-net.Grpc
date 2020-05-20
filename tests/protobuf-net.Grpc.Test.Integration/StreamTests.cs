using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.Grpc.Test.Integration
{
    public class StreamTestsFixture : IAsyncDisposable
    {
        private Server? _server;

        public ITestOutputHelper? Output { get; private set; }
        public void SetOutput(ITestOutputHelper? output) => Output = output;
        public void Log(string message)
        {
            var tmp = Output;
            if (tmp is object)
            {
                lock (tmp)
                {
                    tmp.WriteLine(message);
                }
            }
        }
        public StreamTestsFixture() { }

        public int Port { get; private set; }
        public void Init(int port)
        {
            if (_server == null)
            {
                Port = port;
                _server = new Server
                {
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                _server.Services.AddCodeFirst(new StreamServer(this));
                _server.Start();
            }
            else if (Port != port)
            {
                throw new InvalidOperationException("Cannot change port on the fixture instance");
            }
        }

        public ValueTask DisposeAsync() => _server == null ? default : new ValueTask(_server.ShutdownAsync());
    }

    [Service]
    public interface IStreamAPI
    {
        IAsyncEnumerable<Foo> DuplexEcho(IAsyncEnumerable<Foo> values, CallContext ctx = default);


        IAsyncEnumerable<Foo> FullDuplex(IAsyncEnumerable<Foo> values, CallContext ctx = default);

        IAsyncEnumerable<Foo> ServerStreaming(Foo value, CallContext ctx = default);

        ValueTask<Foo> ClientStreaming(IAsyncEnumerable<Foo> values, CallContext ctx = default);

        ValueTask<Foo> UnaryAsync(Foo value, CallContext ctx = default);

        Foo UnaryBlocking(Foo value, CallContext ctx = default);
    }

    public enum Scenario
    {
        RunToCompletion,
        FaultBeforeYield,
        FaultBeforeHeaders,
        FaultBeforeTrailers,
        YieldNothing,
        FaultAfterYield,
        TakeNothingBadProducer,  // does not observe cancellation
        TakeNothingGoodProducer, // observes cancellation
        FaultSuccessBadProducer,  // does not observe cancellation
        FaultSuccessGoodProducer, // observes cancellation
    }

    class StreamServer : IStreamAPI
    {
        readonly StreamTestsFixture _fixture;
        internal StreamServer(StreamTestsFixture fixture)
            => _fixture = fixture;
        public void Log(string message) => _fixture.Log(message);

        static Scenario GetScenario(in CallContext ctx)
        {
            var header = ctx.RequestHeaders.GetString(nameof(Scenario));
            return !string.IsNullOrWhiteSpace(header) && Enum.TryParse<Scenario>(header, out var tmp) ? tmp : Scenario.RunToCompletion;
        }

        async IAsyncEnumerable<Foo> IStreamAPI.DuplexEcho(IAsyncEnumerable<Foo> values, CallContext ctx)
        {
            Log("server checking scenario");
            var scenario = GetScenario(ctx);

            if (scenario == Scenario.FaultBeforeHeaders) Throw("before headers");

            var sCtx = ctx.ServerCallContext!;
            Log("server yielding response headers");
            await sCtx.WriteResponseHeadersAsync(new Metadata { { "prekey", "preval" } });

            Log("server setting response status in advance");
            sCtx.Status = new Status(StatusCode.OK, "resp detail");
            sCtx.ResponseTrailers.Add("postkey", "postval");

            if (scenario == Scenario.FaultBeforeYield) Throw("before yield");

            switch (scenario)
            {
                case Scenario.FaultSuccessBadProducer:
                case Scenario.FaultSuccessGoodProducer:
                    Log("server faulting with success");
                    throw new RpcException(Status.DefaultSuccess); // another way of expressing yield break
                case Scenario.TakeNothingBadProducer:
                case Scenario.TakeNothingGoodProducer:
                    break;
                default:
                    await foreach (var value in values.WithCancellation(ctx.CancellationToken))
                    {
                        Log($"server received {value.Bar}");
                        switch (scenario)
                        {
                            case Scenario.YieldNothing:
                                break;
                            default:
                                Log($"server yielding {value.Bar}");
                                yield return value;
                                break;
                        }
                    }
                    break;
            }
            Log("server is done yielding");
            if (scenario == Scenario.FaultAfterYield) Throw("after yield");

            Log("server is complete");
        }

        static void Throw(string state)
            => throw new RpcException(new Status(StatusCode.Internal, state + " detail"),
            new Metadata { { "faultkey", state + " faultval" } }, state + " message");

        async ValueTask<Foo> IStreamAPI.UnaryAsync(Foo value, CallContext ctx)
        {
            var scenario = GetScenario(ctx);
            var sCtx = ctx.ServerCallContext!;

            Log($"unary scenario {scenario}, value {value.Bar}");

            if (scenario == Scenario.FaultBeforeHeaders) Throw("before headers");
            await sCtx.WriteResponseHeadersAsync(new Metadata { { "prekey", "preval" } });

            if (scenario == Scenario.FaultBeforeTrailers) Throw("before trailers");
            sCtx.ResponseTrailers.Add("postkey", "postval");

            if (scenario == Scenario.FaultSuccessGoodProducer)
                throw new RpcException(Status.DefaultSuccess);

            Log("server is complete");
            return value;
        }

        Foo IStreamAPI.UnaryBlocking(Foo value, CallContext ctx)
        {
            try
            {
                return ((IStreamAPI)this).UnaryAsync(value, ctx).Result; // sync-over-async for this test only
            }
            catch (RpcException ex)
            {
                Log($"RpcException: {ex.StatusCode}, '{ex.Message}', {ex.Trailers?.Count ?? 0} trailers");
                throw;
            }
        }

        async ValueTask<Foo> IStreamAPI.ClientStreaming(IAsyncEnumerable<Foo> values, CallContext ctx)
        {
            int sum = 0;
            await foreach (var item in values.WithCancellation(ctx.CancellationToken))
            {
                sum += item.Bar;
            }
            return new Foo { Bar = sum };
        }

        async IAsyncEnumerable<Foo> IStreamAPI.ServerStreaming(Foo value, CallContext ctx)
        {
            await ctx.ServerCallContext!.WriteResponseHeadersAsync(new Metadata { { "req", value.Bar } });
            var fault = ctx.RequestHeaders.GetInt32("fault");
            int sum = 0;
            void AddSum(Metadata to) => to.Add("sum", sum);
            for (int i = 0; i < value.Bar; i++)
            {
                if (i == fault)
                {
                    var faultTrailers = new Metadata();
                    AddSum(faultTrailers);
                    throw new RpcException(new Status(StatusCode.Internal, "oops"), faultTrailers);
                }
                sum += i;
                await Task.Yield();
                yield return new Foo { Bar = i };
                ctx.CancellationToken.ThrowIfCancellationRequested();
            }

            AddSum(ctx.ServerCallContext!.ResponseTrailers);
        }

        IAsyncEnumerable<Foo> IStreamAPI.FullDuplex(IAsyncEnumerable<Foo> values, CallContext ctx)
        {
            if (ctx.RequestHeaders.GetString("mode") == "byitem")
                return WrapByItem(values, ctx);
            return ctx.FullDuplexAsync(Producer, values, Consumer);
        }

        async IAsyncEnumerable<Foo> WrapByItem(IAsyncEnumerable<Foo> values, CallContext ctx)
        {
            // this is a different "item by item callback" API
            int count = 0, sum = 0;
            await foreach (var item in ctx.FullDuplexAsync(Producer, values, (val, ctx) =>
             {
                 count++;
                 sum += val.Bar;
                 return default;
             }))
            {
                yield return item;
            }
            ctx.ServerCallContext?.ResponseTrailers.Add("count", count);
            ctx.ServerCallContext?.ResponseTrailers.Add("sum", sum);
        }

        private async ValueTask Consumer(IAsyncEnumerable<Foo> source, CallContext ctx)
        {
            int sum = 0, count = 0;
            int stop = ctx.RequestHeaders.GetInt32("stop") ?? -1;
            if (stop != 0)
            {
                await foreach (var item in source.WithCancellation(ctx.CancellationToken))
                {
                    count++;
                    sum += item.Bar;

                    if (stop > 0 && --stop == 0) break;
                }
            }
            ctx.ServerCallContext?.ResponseTrailers.Add("count", count);
            ctx.ServerCallContext?.ResponseTrailers.Add("sum", sum);
        }

        private async IAsyncEnumerable<Foo> Producer(CallContext ctx)
        {
            var count = ctx.RequestHeaders.GetInt32("produce")!.Value;
            for (int i = 0; i < count; i++)
            {
                yield return new Foo { Bar = i };
                await Task.Delay(10, ctx.CancellationToken);
            }
        }
    }

    [ProtoContract]
    public class Foo
    {
        [ProtoMember(1)]
        public int Bar { get; set; }
    }


    public class NativeStreamTests : StreamTests
    {
        public NativeStreamTests(StreamTestsFixture fixture, ITestOutputHelper log) : base(10043, fixture, log) { }
        protected override IAsyncDisposable CreateClient(out IStreamAPI client)
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            client = channel.CreateGrpcService<IStreamAPI>();
            return new DisposableChannel(channel);
        }
        sealed class DisposableChannel : IAsyncDisposable
        {
            private readonly Channel _channel;
            public DisposableChannel(Channel channel)
                => _channel = channel;
            public ValueTask DisposeAsync() => new ValueTask(_channel.ShutdownAsync());
        }
    }

#if NETCOREAPP3_1
    public class ManagedStreamTests : StreamTests
    {
        public override bool IsManagedClient => true;
        public ManagedStreamTests(StreamTestsFixture fixture, ITestOutputHelper log) : base(10044, fixture, log) { }
        protected override IAsyncDisposable CreateClient(out IStreamAPI client)
        {
            var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            client = http.CreateGrpcService<IStreamAPI>();
            return new DisposableChannel(http);
        }
        sealed class DisposableChannel : IAsyncDisposable
        {
            private readonly global::Grpc.Net.Client.GrpcChannel _channel;
            public DisposableChannel(global::Grpc.Net.Client.GrpcChannel channel)
                => _channel = channel;
            public async ValueTask DisposeAsync()
            {
                await _channel.ShutdownAsync();
                _channel.Dispose();
            }
        }
    }
#endif

    public abstract class StreamTests : IClassFixture<StreamTestsFixture>, IDisposable
    {
        protected int Port => _fixture.Port;
        private readonly StreamTestsFixture _fixture;
        public StreamTests(int port, StreamTestsFixture fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            fixture.Init(port);
            fixture?.SetOutput(log);
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
        }

        public virtual bool IsManagedClient => false;

        public void Dispose() => _fixture?.SetOutput(null);

        protected abstract IAsyncDisposable CreateClient(out IStreamAPI client);

        const int DEFAULT_SIZE = 20;
        [Theory]
        [InlineData(Scenario.RunToCompletion, DEFAULT_SIZE, CallContextFlags.None)]
        [InlineData(Scenario.RunToCompletion, DEFAULT_SIZE, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.YieldNothing, 0, CallContextFlags.IgnoreStreamTermination)]
        [InlineData(Scenario.YieldNothing, 0, CallContextFlags.IgnoreStreamTermination | CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.YieldNothing, 0, CallContextFlags.None)]
        [InlineData(Scenario.YieldNothing, 0, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.TakeNothingGoodProducer, 0, CallContextFlags.IgnoreStreamTermination)]
        [InlineData(Scenario.TakeNothingGoodProducer, 0, CallContextFlags.IgnoreStreamTermination | CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.TakeNothingGoodProducer, 0, CallContextFlags.None)]
        [InlineData(Scenario.TakeNothingGoodProducer, 0, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultSuccessGoodProducer, 0, CallContextFlags.IgnoreStreamTermination)]
        [InlineData(Scenario.FaultSuccessGoodProducer, 0, CallContextFlags.IgnoreStreamTermination | CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultSuccessGoodProducer, 0, CallContextFlags.None)]
        [InlineData(Scenario.FaultSuccessGoodProducer, 0, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.TakeNothingBadProducer, 0, CallContextFlags.IgnoreStreamTermination)]
        [InlineData(Scenario.TakeNothingBadProducer, 0, CallContextFlags.IgnoreStreamTermination | CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultSuccessBadProducer, 0, CallContextFlags.IgnoreStreamTermination)]
        [InlineData(Scenario.FaultSuccessBadProducer, 0, CallContextFlags.IgnoreStreamTermination | CallContextFlags.CaptureMetadata)]
        public async Task DuplexEcho(Scenario scenario, int expectedCount, CallContextFlags flags)
        {
            await using var svc = CreateClient(out var client);

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);

            bool haveCheckedHeaders = false;
            var values = new List<int>(expectedCount);
            await foreach (var item in client.DuplexEcho(For(scenario, DEFAULT_SIZE), ctx))
            {
                CheckHeaderState();
                values.Add(item.Bar);
            }
            _fixture?.Log("after await foreach");
            CheckHeaderState();
            Assert.Equal(string.Join(",", Enumerable.Range(0, expectedCount)), string.Join(",", values));

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {   // check trailers
                Assert.Equal("postval", ctx.ResponseTrailers().GetString("postkey"));

                var status = ctx.ResponseStatus();
                Assert.Equal(StatusCode.OK, status.StatusCode);
                switch (scenario)
                {
                    case Scenario.FaultSuccessGoodProducer:
                    case Scenario.FaultSuccessBadProducer:
                        Assert.Equal("", status.Detail);
                        break;
                    default:
                        Assert.Equal("resp detail", status.Detail);
                        break;
                }
            }

            void CheckHeaderState()
            {
                if (haveCheckedHeaders) return;
                haveCheckedHeaders = true;
                if ((flags & CallContextFlags.CaptureMetadata) != 0)
                {
                    Assert.Equal("preval", ctx.ResponseHeaders().GetString("prekey"));
                }
            }
        }

        [Theory]
        [InlineData(Scenario.TakeNothingBadProducer, 0, CallContextFlags.None)]
        [InlineData(Scenario.TakeNothingBadProducer, 0, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultSuccessBadProducer, 0, CallContextFlags.None)]
        [InlineData(Scenario.FaultSuccessBadProducer, 0, CallContextFlags.CaptureMetadata)]
        public async Task DuplexEchoBadProducer(Scenario scenario, int expectedCount, CallContextFlags flags)
        {
            await using var svc = CreateClient(out var client);

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);

            bool haveCheckedHeaders = false;
            var values = new List<int>(expectedCount);

            var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await foreach (var item in client.DuplexEcho(For(scenario, DEFAULT_SIZE), ctx))
                {
                    CheckHeaderState();
                    values.Add(item.Bar);
                }
            });
            Assert.Equal("A message could not be sent because the server had already terminated the connection; this exception can be suppressed by specifying the IgnoreStreamTermination flag when creating the CallContext", ex.Message);
            Assert.Equal(string.Join(",", Enumerable.Range(0, expectedCount)), string.Join(",", values));

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {   // check trailers
                var noTrailers = Assert.Throws<InvalidOperationException>(() => ctx.ResponseTrailers());
                Assert.Equal("Trailers are not yet available", noTrailers.Message);

                var status = ctx.ResponseStatus();
                Assert.Equal(StatusCode.OK, status.StatusCode);
                Assert.Equal("", status.Detail);
            }

            void CheckHeaderState()
            {
                if (haveCheckedHeaders) return;
                haveCheckedHeaders = true;
                if ((flags & CallContextFlags.CaptureMetadata) != 0)
                {
                    Assert.Equal("preval", ctx.ResponseHeaders().GetString("prekey"));
                }
            }
        }

        [Theory]
        [InlineData(Scenario.FaultAfterYield, DEFAULT_SIZE, "after yield", CallContextFlags.None)]
        [InlineData(Scenario.FaultAfterYield, DEFAULT_SIZE, "after yield", CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultBeforeYield, 0, "before yield", CallContextFlags.None)]
        [InlineData(Scenario.FaultBeforeYield, 0, "before yield", CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultBeforeHeaders, 0, "before headers", CallContextFlags.None)]
        [InlineData(Scenario.FaultBeforeHeaders, 0, "before headers", CallContextFlags.CaptureMetadata)]
        public async Task DuplexEchoFault(Scenario scenario, int expectedCount, string marker, CallContextFlags flags)
        {
            await using var svc = CreateClient(out var client);

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);

            bool haveCheckedHeaders = false;
            var values = new List<int>(expectedCount);

            var rpc = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await foreach (var item in client.DuplexEcho(For(scenario, DEFAULT_SIZE), ctx))
                {
                    CheckHeaderState();
                    values.Add(item.Bar);
                }
            });
            Assert.Equal(StatusCode.Internal, rpc.Status.StatusCode);
            Assert.Equal(marker + " detail", rpc.Status.Detail);
            Assert.Equal(marker + " faultval", rpc.Trailers.GetString("faultkey"));

            _fixture?.Log("after await foreach");
            CheckHeaderState();
            Assert.Equal(string.Join(",", Enumerable.Range(0, expectedCount)), string.Join(",", values));

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {   // check trailers
                Assert.Equal(marker + " faultval", ctx.ResponseTrailers().GetString("faultkey"));

                var status = ctx.ResponseStatus();
                Assert.Equal(StatusCode.Internal, status.StatusCode);
                Assert.Equal(marker + " detail", status.Detail);
            }

            void CheckHeaderState()
            {
                if (haveCheckedHeaders) return;
                haveCheckedHeaders = true;

                if ((flags & CallContextFlags.CaptureMetadata) != 0)
                {
                    switch (scenario)
                    {
                        case Scenario.FaultBeforeHeaders:
                            Assert.Null(ctx.ResponseHeaders().GetString("prekey"));
                            break;
                        default:
                            Assert.Equal("preval", ctx.ResponseHeaders().GetString("prekey"));
                            break;
                    }
                }
            }
        }

        IAsyncEnumerable<Foo> For(Scenario scenario, int count, int from = 0, int millisecondsDelay = 10)
            => ForImpl(_fixture, count, from, millisecondsDelay, scenario switch
            {
                Scenario.FaultSuccessBadProducer => false,
                Scenario.TakeNothingBadProducer => false,
                _ => true
            }, default);
        private static async IAsyncEnumerable<Foo> ForImpl(StreamTestsFixture fixture, int count, int from, int millisecondsDelay,
            bool checkForCancellation, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            void CheckForCancellation(string when)
            {
                if (checkForCancellation && cancellationToken.IsCancellationRequested)
                {
                    fixture.Log("cancellation detected in producer " + when);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            void Log(string message) => fixture?.Log(message);

            Log("starting producer");
            try
            {
                for (int i = 0; i < count; i++)
                {
                    await Task.Delay(millisecondsDelay);
                    CheckForCancellation("before yield");

                    Log($"producer yielding {i}");
                    yield return new Foo { Bar = i + from };
                    CheckForCancellation("after yield");
                }

                Log($"producer ran to completion");
            }
            finally
            {
                Log("exiting producer");
            }
        }

        [Theory]
        [InlineData(Scenario.FaultBeforeHeaders, CallContextFlags.None)]
        [InlineData(Scenario.FaultBeforeHeaders, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultBeforeTrailers, CallContextFlags.None)]
        [InlineData(Scenario.FaultBeforeTrailers, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultSuccessGoodProducer, CallContextFlags.None)]
        [InlineData(Scenario.FaultSuccessGoodProducer, CallContextFlags.CaptureMetadata)]
        public async Task AsyncUnaryFault(Scenario scenario, CallContextFlags flags)
        {
            await using var svc = CreateClient(out var client);

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);

            var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.UnaryAsync(new Foo { Bar = 42 }, ctx));
            CheckStatus(ex.Status);
            switch (scenario)
            {
                case Scenario.FaultBeforeHeaders:
                    Assert.Equal("before headers faultval", ex.Trailers.GetString("faultkey"));
                    break;
                case Scenario.FaultBeforeTrailers:
                    Assert.Equal("before trailers faultval", ex.Trailers.GetString("faultkey"));
                    break;
                case Scenario.FaultSuccessGoodProducer:
                    Assert.Null(ex.Trailers.GetString("faultkey"));
                    break;
                default:
                    throw new NotImplementedException();
            }

            void CheckStatus(Status status)
            {
                switch (scenario)
                {
                    case Scenario.FaultBeforeHeaders:
                        Assert.Equal(StatusCode.Internal, status.StatusCode);
                        Assert.Equal("before headers detail", status.Detail);
                        break;
                    case Scenario.FaultBeforeTrailers:
                        Assert.Equal(StatusCode.Internal, status.StatusCode);
                        Assert.Equal("before trailers detail", status.Detail);
                        break;
                    case Scenario.FaultSuccessGoodProducer:
                        if (IsManagedClient)
                        {
                            Assert.Equal(StatusCode.OK, status.StatusCode);
                            Assert.Equal("", status.Detail);
                        }
                        else
                        {   // see https://github.com/grpc/grpc-dotnet/issues/915
                            Assert.Equal(StatusCode.Internal, status.StatusCode);
                            Assert.Equal("Failed to deserialize response message.", status.Detail);
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {
                CheckStatus(ctx.ResponseStatus());
                switch (scenario)
                {
                    case Scenario.FaultBeforeHeaders:
                        Assert.Null(ctx.ResponseHeaders().GetString("prekey"));
                        Assert.Null(ctx.ResponseTrailers().GetString("postkey"));
                        Assert.Equal("before headers faultval", ctx.ResponseTrailers().GetString("faultkey"));
                        break;
                    case Scenario.FaultBeforeTrailers:
                        Assert.Equal("preval", ctx.ResponseHeaders().GetString("prekey"));
                        Assert.Null(ctx.ResponseTrailers().GetString("postkey"));
                        Assert.Equal("before trailers faultval", ctx.ResponseTrailers().GetString("faultkey"));
                        break;
                    case Scenario.FaultSuccessGoodProducer:
                        Assert.Equal("preval", ctx.ResponseHeaders().GetString("prekey"));
                        Assert.Equal("postval", ctx.ResponseTrailers().GetString("postkey"));
                        Assert.Null(ctx.ResponseTrailers().GetString("faultkey"));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

        }

        [Theory]
        [InlineData(Scenario.RunToCompletion, CallContextFlags.None)]
        [InlineData(Scenario.RunToCompletion, CallContextFlags.CaptureMetadata)]

        public async Task AsyncUnarySuccess(Scenario scenario, CallContextFlags flags)
        {
            await using var svc = CreateClient(out var client);

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);

            var result = await client.UnaryAsync(new Foo { Bar = 42 }, ctx);
            Assert.Equal(42, result.Bar);

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {
                var status = ctx.ResponseStatus();
                Assert.Equal(StatusCode.OK, status.StatusCode);
                Assert.Equal("", status.Detail);
                Assert.Equal("preval", ctx.ResponseHeaders().GetString("prekey"));
                Assert.Equal("postval", ctx.ResponseTrailers().GetString("postkey"));
            }
        }

        [Theory]
        [InlineData(Scenario.FaultBeforeHeaders, CallContextFlags.None)]
        [InlineData(Scenario.FaultBeforeHeaders, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultBeforeTrailers, CallContextFlags.None)]
        [InlineData(Scenario.FaultBeforeTrailers, CallContextFlags.CaptureMetadata)]
        [InlineData(Scenario.FaultSuccessGoodProducer, CallContextFlags.None)]
        [InlineData(Scenario.FaultSuccessGoodProducer, CallContextFlags.CaptureMetadata)]
        public async Task BlockingUnaryFault(Scenario scenario, CallContextFlags flags)
        {
            await using var svc = CreateClient(out var client);

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);

            var ex = Assert.Throws<RpcException>(() => client.UnaryBlocking(new Foo { Bar = 42 }, ctx));
            CheckStatus(ex.Status);
            switch (scenario)
            {
                case Scenario.FaultBeforeHeaders:
                    Assert.Equal("before headers faultval", ex.Trailers.GetString("faultkey"));
                    break;
                case Scenario.FaultBeforeTrailers:
                    Assert.Equal("before trailers faultval", ex.Trailers.GetString("faultkey"));
                    break;
                case Scenario.FaultSuccessGoodProducer:
                    Assert.Null(ex.Trailers.GetString("faultkey"));
                    break;
                default:
                    throw new NotImplementedException();
            }

            void CheckStatus(Status status)
            {
                switch (scenario)
                {
                    case Scenario.FaultBeforeHeaders:
                        Assert.Equal(StatusCode.Internal, status.StatusCode);
                        Assert.Equal("before headers detail", status.Detail);
                        break;
                    case Scenario.FaultBeforeTrailers:
                        Assert.Equal(StatusCode.Internal, status.StatusCode);
                        Assert.Equal("before trailers detail", status.Detail);
                        break;
                    case Scenario.FaultSuccessGoodProducer:
                        if (IsManagedClient)
                        {
                            Assert.Equal(StatusCode.OK, status.StatusCode);
                            Assert.Equal("", status.Detail);
                        }
                        else
                        {   // see https://github.com/grpc/grpc-dotnet/issues/916
                            Assert.Equal(StatusCode.Internal, status.StatusCode);
                            Assert.Equal("Failed to deserialize response message.", status.Detail);
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {
                CheckStatus(ctx.ResponseStatus());
            }

        }

        [Theory]
        [InlineData(Scenario.RunToCompletion, CallContextFlags.None)]
        [InlineData(Scenario.RunToCompletion, CallContextFlags.CaptureMetadata)]

        public async Task BlockingUnarySuccess(Scenario scenario, CallContextFlags flags)
        {
            await using var svc = CreateClient(out var client);

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);

            var result = client.UnaryBlocking(new Foo { Bar = 42 }, ctx);
            Assert.Equal(42, result.Bar);

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {
                var status = ctx.ResponseStatus();
                Assert.Equal(StatusCode.OK, status.StatusCode);
                Assert.Equal("", status.Detail);
                // headers are not available via blocking unary success
            }
        }

        [Theory]
        [InlineData(8, 10)]
        [InlineData(8, 20, 15)]
        [InlineData(0, 10)]
        [InlineData(0, 20, 15)]
        [InlineData(8, 10, -1, true)]
        [InlineData(8, 20, 15, true)]
        [InlineData(0, 10, -1, true)]
        [InlineData(0, 20, 15, true)]
        public async Task FullDuplexAsync(int send, int produce, int stopReadingAfter = -1, bool byItem = false)
        {
            await using var svc = CreateClient(out var client);

            var reqHeaders = new Metadata();
            reqHeaders.Add("produce", produce);
            if (byItem) reqHeaders.Add("mode", "byitem");
            if (stopReadingAfter >= 0) reqHeaders.Add("stop", stopReadingAfter);

            var ctx = new CallContext(new CallOptions(headers: reqHeaders), CallContextFlags.CaptureMetadata);

            int got = 0;
            await foreach (var reply in client.FullDuplex(For(Scenario.RunToCompletion, send), ctx))
            {
                got++;
            }
            Assert.Equal(produce, got);
            var trailers = ctx.ResponseTrailers();

            if (stopReadingAfter < 0) stopReadingAfter = send;
            stopReadingAfter = Math.Min(send, stopReadingAfter);
            Assert.Equal(stopReadingAfter, trailers.GetInt32("count"));
            Assert.Equal(Enumerable.Range(0, stopReadingAfter).Sum(), trailers.GetInt32("sum"));
        }

        [Fact]
        public async Task ClientStreaming()
        {
            await using var svc = CreateClient(out var client);

            var result = await client.ClientStreaming(For(Scenario.RunToCompletion, 10));
            Assert.Equal(Enumerable.Range(0, 10).Sum(), result.Bar);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ServerStreaming(bool fault)
        {
            await using var svc = CreateClient(out var client);

            int count = 0;
            var reqHeaders = new Metadata();
            if (fault) reqHeaders.Add("fault", 5);
            var ctx = new CallContext(new CallOptions(headers: reqHeaders), flags: CallContextFlags.CaptureMetadata);

            var seq = client.ServerStreaming(new Foo { Bar = 10 }, ctx);
            if (fault)
            {
                var ex = await Assert.ThrowsAsync<RpcException>(async () =>
                {
                    await foreach (var item in seq) { }
                });
                Assert.Equal("oops", ex.Status.Detail);
                Assert.Equal(StatusCode.Internal, ex.Status.StatusCode);
                Assert.Equal(10, ctx.ResponseHeaders().GetInt32("req"));

                // managed client doesn't seem to get fault trailers on server-streaming
                var expect = Enumerable.Range(0, 5).Sum();
                Assert.Equal(expect, ctx.ResponseTrailers().GetInt32("sum"));
                Assert.Equal(expect, ex.Trailers.GetInt32("sum"));
            }
            else
            {
                await foreach (var item in seq)
                {
                    Assert.Equal(count, item.Bar);
                    count++;
                }
                Assert.Equal(StatusCode.OK, ctx.ResponseStatus().StatusCode);
                Assert.Equal("", ctx.ResponseStatus().Detail);
                Assert.Equal(10, ctx.ResponseHeaders().GetInt32("req"));
                Assert.Equal(10, count);
                var expect = Enumerable.Range(0, 10).Sum();
                Assert.Equal(expect, ctx.ResponseTrailers().GetInt32("sum"));
            }
        }
    }
}
