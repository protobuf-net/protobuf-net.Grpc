using Grpc.Core;
using Grpc.Net.Client;
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
        public const int Port = 10043;
        private readonly Server _server;

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
        public StreamTestsFixture()
        {

            _server = new Server
            {
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            _server.Services.AddCodeFirst(new StreamServer(this));
            _server.Start();
        }

        public ValueTask DisposeAsync() => new ValueTask(_server.ShutdownAsync());
    }

    [Service]
    public interface IStreamAPI
    {
        IAsyncEnumerable<Foo> DuplexEcho(IAsyncEnumerable<Foo> values, CallContext ctx = default);
    }

    public enum Scenario
    {
        RunToCompletion,
        FaultBeforeYield,
        FaultBeforeHeaders,
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

        async IAsyncEnumerable<Foo> IStreamAPI.DuplexEcho(IAsyncEnumerable<Foo> values, CallContext ctx)
        {
            Log("server checking scenario");
            var header = ctx.RequestHeaders.GetValue(nameof(Scenario));
            var scenario = !string.IsNullOrWhiteSpace(header) && Enum.TryParse<Scenario>(header, out var tmp) ? tmp : Scenario.RunToCompletion;

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

            if (scenario == Scenario.FaultAfterYield) Throw("after yield");

            static void Throw(string state)
                => throw new RpcException(new Status(StatusCode.Internal, state + " detail"),
                new Metadata { { "faultkey", state + " faultval" } }, state + " message");
        }
    }

    [ProtoContract]
    public class Foo
    {
        [ProtoMember(1)]
        public int Bar { get; set; }
    }

    public class StreamTests : IClassFixture<StreamTestsFixture>, IDisposable
    {
        private readonly StreamTestsFixture _fixture;

        public StreamTests(StreamTestsFixture fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            fixture?.SetOutput(log);
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
        }

        public void Dispose() => _fixture?.SetOutput(null);

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
            using var http = GrpcChannel.ForAddress($"http://localhost:{StreamTestsFixture.Port}");
            var client = http.CreateGrpcService<IStreamAPI>();

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
            Assert.Equal(string.Join(',', Enumerable.Range(0, expectedCount)), string.Join(',', values));

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {   // check trailers
                Assert.Equal("postval", ctx.ResponseTrailers().GetValue("postkey"));

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
                    Assert.Equal("preval", ctx.ResponseHeaders().GetValue("prekey"));
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
            using var http = GrpcChannel.ForAddress($"http://localhost:{StreamTestsFixture.Port}");
            var client = http.CreateGrpcService<IStreamAPI>();

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
            Assert.Equal(string.Join(',', Enumerable.Range(0, expectedCount)), string.Join(',', values));

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {   // check trailers
                Assert.Equal("postval", ctx.ResponseTrailers().GetValue("postkey"));

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
                    Assert.Equal("preval", ctx.ResponseHeaders().GetValue("prekey"));
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
            using var http = GrpcChannel.ForAddress($"http://localhost:{StreamTestsFixture.Port}");
            var client = http.CreateGrpcService<IStreamAPI>();

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
            Assert.Equal(marker + " faultval", rpc.Trailers.GetValue("faultkey"));

            _fixture?.Log("after await foreach");
            CheckHeaderState();
            Assert.Equal(string.Join(',', Enumerable.Range(0, expectedCount)), string.Join(',', values));

            if ((flags & CallContextFlags.CaptureMetadata) != 0)
            {   // check trailers
                Assert.Equal(marker + " faultval", ctx.ResponseTrailers().GetValue("faultkey"));

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
                            Assert.Null(ctx.ResponseHeaders().GetValue("prekey"));
                            break;
                        default:
                            Assert.Equal("preval", ctx.ResponseHeaders().GetValue("prekey"));
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
    }
}
