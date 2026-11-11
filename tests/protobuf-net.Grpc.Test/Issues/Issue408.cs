using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace protobuf_net.Grpc.Test.Issues
{
    // the proxy-emission counter is process-global, so this class must not run
    // in parallel with other tests that may emit proxies
    [CollectionDefinition(nameof(Issue408), DisableParallelization = true)]
    public class Issue408Collection { }

    [Collection(nameof(Issue408))]
    public class Issue408
    {
        [Fact(Timeout = 120_000)]
        public async Task ConcurrentFirstClientCreationDoesNotThrow()
        {
            const int Rounds = 200;
            int threads = Math.Min(Math.Max(Environment.ProcessorCount, 4), 8);
            var invoker = new NullCallInvoker();
            var cancellationToken = TestContext.Current.CancellationToken;

            for (int round = 0; round < Rounds; round++)
            {
                var factory = ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Default]));
                using var barrier = new Barrier(threads);
                var tasks = new Task[threads];
                for (int i = 0; i < threads; i++)
                {
                    tasks[i] = Task.Factory.StartNew(() =>
                    {
                        barrier.SignalAndWait(cancellationToken);
                        factory.CreateClient<IRaceService>(invoker);
                    }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }
                await Task.WhenAll(tasks);
            }
        }

        [Fact(Timeout = 120_000)]
        public async Task ConcurrentFirstClientCreationEmitsSingleProxy()
        {
            int threads = Math.Min(Math.Max(Environment.ProcessorCount, 4), 8);
            var invoker = new NullCallInvoker();
            var cancellationToken = TestContext.Current.CancellationToken;
            var typeIndex = typeof(ProxyEmitter).GetField("_typeIndex", BindingFlags.NonPublic | BindingFlags.Static)!;

            var factory = ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Default]));
            int before = (int)typeIndex.GetValue(null)!;
            using var barrier = new Barrier(threads);
            var tasks = new Task[threads];
            for (int i = 0; i < threads; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    barrier.SignalAndWait(cancellationToken);
                    factory.CreateClient<IRaceService>(invoker);
                }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            await Task.WhenAll(tasks);
            int after = (int)typeIndex.GetValue(null)!;
            Assert.Equal(1, after - before);
        }

        [ServiceContract]
        public interface IRaceService
        {
            [OperationContract]
            ValueTask<RaceMessage> Call(RaceMessage value);
        }

        [DataContract]
        public class RaceMessage { }

        private sealed class NullCallInvoker : CallInvoker
        {
            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        }
    }
}
