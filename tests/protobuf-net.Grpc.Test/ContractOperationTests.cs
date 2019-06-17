using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Reflection;
using Xunit;

namespace protobuf_net.Grpc.Test
{
    public class ContractOperationTests
    {
        [Fact]
        public void SimpleInterface()
        {
            var all = ContractOperation.ExpandInterfaces(typeof(IC));
            Assert.Equal(1, all.Count);
            Assert.Contains(typeof(IC), all);
        }
        [Fact]
        public void NestedInterfaces()
        {
            var all = ContractOperation.ExpandInterfaces(typeof(IB));
            Assert.Equal(4, all.Count);
            Assert.Contains(typeof(IB), all);
            Assert.Contains(typeof(ID), all);
            Assert.Contains(typeof(IE), all);
            Assert.Contains(typeof(IF), all);
        }
        [Fact]
        public void SublclassInterfaces()
        {
            var all = ContractOperation.ExpandInterfaces(typeof(C));
            Assert.Equal(6, all.Count);
            Assert.Contains(typeof(IA), all);
            Assert.Contains(typeof(IB), all);
            Assert.Contains(typeof(IC), all);
            Assert.Contains(typeof(ID), all);
            Assert.Contains(typeof(IE), all);
            Assert.Contains(typeof(IF), all);
        }

        [Fact]
        public void AllOptionsAccountedFor()
        {
            Assert.Equal(23, typeof(IAllOptions).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length);
        }

        [Theory]
        [InlineData(nameof(IAllOptions.Client_AsyncUnary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallOptions, (int)ResultKind.Grpc)]
        [InlineData(nameof(IAllOptions.Client_BlockingUnary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallOptions, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Client_ClientStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc)]
        [InlineData(nameof(IAllOptions.Client_Duplex), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc)]
        [InlineData(nameof(IAllOptions.Client_ServerStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc)]

        [InlineData(nameof(IAllOptions.Server_ClientStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task)]
        [InlineData(nameof(IAllOptions.Server_Duplex), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task)]
        [InlineData(nameof(IAllOptions.Server_ServerStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task)]
        [InlineData(nameof(IAllOptions.Server_Unary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.ServerCallContext, (int)ResultKind.Task)]

        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Shared_Duplex_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_Duplex_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable)]

        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable)]

        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.Task)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.Task)]

        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task)]

        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTask)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTask)]

        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask)]
        public void CheckMethodIdentification(string name, Type from, Type to, MethodType methodType, int context, int result)
        {
            var method = typeof(IAllOptions).GetMethod(name);
            Assert.NotNull(method);
            Assert.True(ContractOperation.TryIdentifySignature(method!, out var operation));
            Assert.Equal(method, operation.Method);
            Assert.Equal(methodType, operation.MethodType);
            Assert.Equal((ContextKind)context, operation.Context);
            Assert.Equal(method!.Name, operation.Name);
            Assert.Equal((ResultKind)result, operation.Result);
            Assert.Equal(from, operation.From);
            Assert.Equal(to, operation.To);
        }


        class C : B, IC { }
        class B : A, IB { }
        class A : IA { }
        interface IC { }
        interface IB : ID { }
        interface IA { }
        interface ID : IE, IF { }
        interface IE { }
        interface IF { }
    }
}
