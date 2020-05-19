using Grpc.Core;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ProtoBuf.Grpc.Server;
using ProtoBuf;
using System.Collections.Generic;
using ProtoBuf.Grpc;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Linq;
using ProtoBuf.Grpc.Configuration;

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
                lock(tmp)
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
            _server.Services.AddCodeFirst(new StreamServer());
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
        YieldNothing,
        FaultAfterYield,
    }

    class StreamServer : IStreamAPI
    {
        async IAsyncEnumerable<Foo> IStreamAPI.DuplexEcho(IAsyncEnumerable<Foo> values, CallContext ctx)
        {
            var header = ctx.RequestHeaders.GetValue(nameof(Scenario));
            var scenario = !string.IsNullOrWhiteSpace(header) && Enum.TryParse<Scenario>(header, out var tmp) ? tmp : Scenario.RunToCompletion;

            if (scenario == Scenario.YieldNothing) yield break;
            if (scenario == Scenario.FaultBeforeYield) throw new InvalidOperationException("before yield");

            await foreach (var value in values.WithCancellation(ctx.CancellationToken))
            {
                yield return value;
            }

            if (scenario == Scenario.FaultAfterYield) throw new InvalidOperationException("after yield");
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

        [Theory]
        [InlineData(Scenario.RunToCompletion, 10, CallContextFlags.None)]
        [InlineData(Scenario.YieldNothing, 0, CallContextFlags.IgnoreStreamTermination)]
        [InlineData(Scenario.FaultBeforeYield, 0, CallContextFlags.IgnoreStreamTermination)]
        [InlineData(Scenario.FaultAfterYield, 10, CallContextFlags.None)]
        public async Task DuplexEcho(Scenario scenario, int expectedCount, CallContextFlags flags)
        {
            using var http = GrpcChannel.ForAddress($"http://localhost:{StreamTestsFixture.Port}");
            var client = http.CreateGrpcService<IStreamAPI>();

            var ctx = new CallContext(new CallOptions(headers: new Metadata { { nameof(Scenario), scenario.ToString() } }), flags);
            var values = new List<int>(10);
            await foreach(var item in client.DuplexEcho(For(10), ctx))
            {
                values.Add(item.Bar);
            }
            Assert.Equal(string.Join(',', Enumerable.Range(0, expectedCount)), string.Join(',', values));
        }

        IAsyncEnumerable<Foo> For(int count, int from = 0, int millisecondsDelay = 10)
            => ForImpl(_fixture, count, from, millisecondsDelay, default);
        private static async IAsyncEnumerable<Foo> ForImpl(StreamTestsFixture fixture, int count, int from, int millisecondsDelay, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            void CheckForCancellation(string when)
            {
                if (cancellationToken.IsCancellationRequested)
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
