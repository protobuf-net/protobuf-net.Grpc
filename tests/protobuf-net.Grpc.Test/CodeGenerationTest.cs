using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using ProtoBuf.Grpc;
using Xunit;

// Type or member is obsolete : internal types are not really obsolete, just used internally, which is precisely what we want to test
#pragma warning disable CS0618

namespace protobuf_net.Grpc.Test
{
    public class CodeGenerationTest
    {
        public class Binder : ServerBinder
        {
            protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
            {
                var builder = (ServerServiceDefinition.Builder)bindContext.State;
                switch (method.Type)
                {
                    case MethodType.Unary:
                        builder.AddMethod(method, stub.CreateDelegate<UnaryServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.ClientStreaming:
                        builder.AddMethod(method, stub.CreateDelegate<ClientStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.ServerStreaming:
                        builder.AddMethod(method, stub.CreateDelegate<ServerStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.DuplexStreaming:
                        builder.AddMethod(method, stub.CreateDelegate<DuplexStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    default:
                        return false;
                }
                return true;
            }
        }

        public static IEnumerable<object[]> IAllOptionsMethods => typeof(IAllOptions).GetMethods().Select(m => new object[] { m });

        [Theory]
        [MemberData(nameof(IAllOptionsMethods))]
        public async void GenerateIAllOptionsProxy(MethodInfo method)
        {
            if (!method.Name.StartsWith("Shared"))
                return;

            Assert.True(method.GetParameters().All(p =>
                p.ParameterType == typeof(HelloRequest) ||
                p.ParameterType == typeof(CallContext) ||
                p.ParameterType == typeof(CancellationToken) ||
                p.ParameterType == typeof(IAsyncEnumerable<HelloRequest>)));

            var builder = ServerServiceDefinition.CreateBuilder();
            new Binder().Bind(builder, null, new AllOptionsService());
            var serviceDefinition = builder.Build();
            // I don't understand why this is internal...
            var bindService = typeof(ServerServiceDefinition).GetMethod("BindService", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(bindService);

            var serviceBinder = new MockServiceBinder();
            // Assert.NotNull not (yet ?) identified as returning only if arg is not null...
            bindService!.Invoke(serviceDefinition, new object[] { serviceBinder });
            var callInvoker = new MockCallInvoker(serviceBinder);
            var factory = ProxyEmitter.CreateFactory<IAllOptions>(BinderConfiguration.Default);

            IAllOptions proxy = factory(callInvoker);

            var parameters = method.GetParameters().Select(p =>
                    p.ParameterType == typeof(HelloRequest) ? (object)new HelloRequest { Name = "John" } :
                    p.ParameterType == typeof(IAsyncEnumerable<HelloRequest>) ? (object)SomeRequests() :
                    p.ParameterType == typeof(CallContext) ? (object)new CallContext() :
                    p.ParameterType == typeof(CancellationToken) ? (object)CancellationToken.None : null)
                .ToArray();

            var result = method.Invoke(proxy, parameters);

            switch (result)
            {
                case HelloReply reply:
                    Assert.StartsWith("Hello", reply.Message);
                    break;
                case Task<HelloReply> replyTask:
                    Assert.StartsWith("Hello", (await replyTask).Message);
                    break;
                case ValueTask<HelloReply> replyTask:
                    Assert.StartsWith("Hello", (await replyTask).Message);
                    break;
                case IAsyncEnumerable<HelloReply> replyStream:
                    var enumerator = replyStream.GetAsyncEnumerator();
                    await enumerator.MoveNextAsync();
                    Assert.StartsWith("Hello", enumerator.Current.Message);
                    break;
                case Task task:
                    await task;
                    break;
                case ValueTask task:
                    await task;
                    break;
                case null:  // void-returning methods get a null result when called via Invoke
                    break;
                default:
                    Assert.True(false, $"Unexpected result type {result}");
                    break;
            }

#pragma warning disable 1998
            async IAsyncEnumerable<HelloRequest> SomeRequests()
            {
                for (int i = 0; i < 3; i++)
                    yield return new HelloRequest { Name = $"John {i + 1}"};
            }
#pragma warning restore 1998
        }
        public static IEnumerable<object[]> ILegacyServiceMethods => typeof(ILegacyService).GetMethods().Select(m => new object[] { m });

        [Theory]
        [MemberData(nameof(ILegacyServiceMethods))]
        public async Task GenerateILegacyServiceProxy(MethodInfo method)
        {
            Assert.True(method.GetParameters().All(p =>
                p.ParameterType == typeof(string) ||
                p.ParameterType == typeof(long) ||
                p.ParameterType == typeof(HelloRequest) ||
                p.ParameterType == typeof(CancellationToken)));

            var builder = ServerServiceDefinition.CreateBuilder();
            new Binder().Bind(builder, null, new LegacyService());
            var serviceDefinition = builder.Build();
            // I don't understand why this is internal...
            var bindService = typeof(ServerServiceDefinition).GetMethod("BindService", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(bindService);

            var serviceBinder = new MockServiceBinder();
            // Assert.NotNull not (yet ?) identified as returning only if arg is not null...
            bindService!.Invoke(serviceDefinition, new object[] { serviceBinder });
            var callInvoker = new MockCallInvoker(serviceBinder);
            var factory = ProxyEmitter.CreateFactory<ILegacyService>(BinderConfiguration.Default);

            ILegacyService proxy = factory(callInvoker);

            var parameters = method.GetParameters().Select(p =>
                    p.ParameterType == typeof(HelloRequest) ? (object)new HelloRequest { Name = "John" } :
                    p.ParameterType == typeof(string) ? (object)"John" :
                    p.ParameterType == typeof(long) ? (object)42 :
                    p.ParameterType == typeof(CancellationToken) ? (object)CancellationToken.None : null)
                .ToArray();

            var result = method.Invoke(proxy, parameters);

            const string expectedMessage = "Hello John, aged 42";
            switch (result)
            {
                case HelloReply reply:
                    Assert.Equal(expectedMessage, reply.Message);
                    break;
                case Task<HelloReply> replyTask:
                    Assert.Equal(expectedMessage, (await replyTask).Message);
                    break;
                case ValueTask<HelloReply> replyTask:
                    Assert.Equal(expectedMessage, (await replyTask).Message);
                    break;
                case Task task:
                    // a Task<something_unexpected> derives from Task, so would match here
                    Assert.Equal(typeof(Task), method.ReturnType);
                    await task;
                    break;
                case ValueTask task:
                    // Note that ValueTask<T> does *not* derive from ValueTask
                    await task;
                    break;
                case null:  // void-returning methods get a null result when called via Invoke
                    break;
                default:
                    Assert.True(false, $"Unexpected result type {result}");
                    break;
            }
        }
    }
}
