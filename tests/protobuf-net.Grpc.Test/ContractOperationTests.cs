using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.Grpc.Test
{
    public class ContractOperationTests
    {
        private readonly ITestOutputHelper _output;
        public ContractOperationTests(ITestOutputHelper output) => _output = output;
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
        public void GeneralPurposeSignatureCount()
        {
            Assert.Equal(38, ContractOperation.GeneralPurposeSignatureCount());
        }

        [Fact]
        public void ServerSignatureCount()
        {
            Assert.Equal(38, ServerInvokerLookup.GeneralPurposeSignatureCount());
        }

        [Fact]
        public void CheckAllMethodsConvered()
        {
            var expected =  typeof(IAllOptions).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(x => x.Name).ToHashSet();
            Assert.Equal(ContractOperation.SignatureCount, expected.Count);

            var attribs = GetType().GetMethod(nameof(CheckMethodIdentification))!.GetCustomAttributesData();
            foreach(var attrib in attribs)
            {
                if (attrib.AttributeType != typeof(InlineDataAttribute)) continue;

                foreach(var arg in attrib.ConstructorArguments)
                {
                    var vals = (ReadOnlyCollection<CustomAttributeTypedArgument>)arg.Value!;
                    var name = (string)vals[0].Value!;
                    expected.Remove(name);
                }
            }

            Assert.Empty(expected);
        }
#pragma warning disable CS0618
        [Theory]
        [InlineData(nameof(IAllOptions.Client_AsyncUnary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Client_BlockingUnary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallOptions, (int)ResultKind.Sync, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Client_ClientStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Client_Duplex), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Client_ServerStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None)]

        [InlineData(nameof(IAllOptions.Server_ClientStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Server_Duplex), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Server_ServerStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Server_Unary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None)]

        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.Both)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.Both)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.Request)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.Request)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.Response)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.Response)]

        [InlineData(nameof(IAllOptions.Shared_Duplex_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_Duplex_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None)]

        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.Request)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.Request)]

        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Response)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Response)]

        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Both)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Both)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Request)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Request)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Response)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Response)]

        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Response)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Response)]

        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.None)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Both)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Both)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Request)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Request)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Response)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Response)]
        public void CheckMethodIdentification(string name, Type from, Type to, MethodType methodType, int context, int result, int @void)
        {
            var method = typeof(IAllOptions).GetMethod(name);
            Assert.NotNull(method);
            if (!ContractOperation.TryIdentifySignature(method!, out var operation))
            {
                var sig = ContractOperation.GetSignature(method!);
                Assert.True(false, sig.ToString());
            }
            Assert.Equal(method, operation.Method);
            Assert.Equal(methodType, operation.MethodType);
            Assert.Equal((ContextKind)context, operation.Context);
            Assert.Equal(method!.Name, operation.Name);
            Assert.Equal((ResultKind)result, operation.Result);
            Assert.Equal(from, operation.From);
            Assert.Equal(to, operation.To);
            Assert.Equal((VoidKind)@void, operation.Void);
        }

        [Fact]
        public void WriteAllMethodSignatures()
        {
            var list = new List<(MethodType Kind, string Signature)>();
            var sb = new StringBuilder();
            foreach (var method in typeof(IAllOptions).GetMethods())
            {
                if (ContractOperation.TryIdentifySignature(method, out var operation))
                {
                    sb.Clear();
                    sb.Append(Sanitize(method.ReturnType)).Append(" Foo(");
                    var p = method.GetParameters();
                    for (int i = 0; i < p.Length; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        sb.Append(Sanitize(p[i].ParameterType));
                    }
                    sb.Append(")");
                    list.Add((operation.MethodType, sb.ToString()));
                }
            }

            foreach (var grp in list.GroupBy(x => x.Kind))
            {
                _output.WriteLine(grp.Key.ToString());
                _output.WriteLine("");
                foreach(var item in grp.OrderBy(x => x.Signature))
                    _output.WriteLine(item.Signature);
                _output.WriteLine("");
            }

            string Sanitize(Type type)
            {
                if (type == typeof(HelloRequest)) return "TRequest";
                if (type == typeof(HelloReply)) return "TReply";
                if (type == typeof(void)) return "void";
                if (type == typeof(ValueTask) || type == typeof(Task)
                    || type == typeof(CallOptions) || type == typeof(ServerCallContext)
                    || type == typeof(CallContext)) return type.Name;

                if (type == typeof(ValueTask<HelloReply>)) return "ValueTask<TReply>";
                if (type == typeof(Task<HelloReply>)) return "Task<TReply>";
                if (type == typeof(IAsyncEnumerable<HelloReply>)) return "IAsyncEnumerable<TReply>";
                if (type == typeof(IAsyncEnumerable<HelloRequest>)) return "IAsyncEnumerable<TRequest>";

                if (type == typeof(IServerStreamWriter<HelloReply>)) return "IServerStreamWriter<TReply>";
                if (type == typeof(IAsyncStreamReader<HelloRequest>)) return "IAsyncStreamReader<TRequest>";
                if (type == typeof(AsyncClientStreamingCall<HelloRequest, HelloReply>)) return "AsyncClientStreamingCall<TRequest,TReply>";
                if (type == typeof(AsyncDuplexStreamingCall<HelloRequest, HelloReply>)) return "AsyncDuplexStreamingCall<TRequest,TReply>";
                if (type == typeof(AsyncServerStreamingCall<HelloReply>)) return "AsyncServerStreamingCall<TReply>";
                if (type == typeof(AsyncUnaryCall<HelloReply>)) return "AsyncUnaryCall<TReply>";


                return "**" + type.Name + "**";
            }
        }

#pragma warning restore CS0618
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
