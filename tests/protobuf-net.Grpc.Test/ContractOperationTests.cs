using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.Grpc.Test
{
    public class ContractOperationTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;

        [Fact]
        public void SimpleInterface()
        {
            var all = ContractOperation.ExpandInterfaces(typeof(IC));
            Assert.Single(all);
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
            Assert.Equal(90, ContractOperation.GeneralPurposeSignatureCount());
        }

        [Fact]
        public void ServerSignatureCount()
        {
            Assert.Equal(90, ServerInvokerLookup.GeneralPurposeSignatureCount());
        }

        [Fact]
        public void CheckAllMethodsCovered()
        {
            var expected = new HashSet<string>(typeof(IAllOptions).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(x => x.Name));
            Assert.Equal(ContractOperation.SignatureCount, expected.Count);

            var attribs = GetType().GetMethod(nameof(CheckMethodIdentification))!.GetCustomAttributesData();
            foreach (var attrib in attribs)
            {
                if (attrib.AttributeType != typeof(InlineDataAttribute)) continue;

                foreach (var arg in attrib.ConstructorArguments)
                {
                    var vals = (ReadOnlyCollection<CustomAttributeTypedArgument>)arg.Value!;
                    var name = (string)vals[0].Value!;
                    expected.Remove(name);
                }
            }

            Assert.Empty(expected);
        }


        [Service]
        [SubService]
        public interface IIServiceWithBothServiceAndSubServiceAttributes
        {
            void SomeMethod();
        }

        [Fact]
        public void WhenInterfaceHasAttributesServiceAndSubService_Throw()
        {
            var config = BinderConfiguration.Default;
            Action activation = () => ContractOperation.ExpandWithInterfacesMarkedAsSubService(
                config.Binder,
                typeof(IIServiceWithBothServiceAndSubServiceAttributes));
            Assert.Throws<ArgumentException>(activation.Invoke);
        }

        public class ServiceContractClassInheritsServiceAndSubServiceAttributes
        : IIServiceWithBothServiceAndSubServiceAttributes, IGrpcService
        {
            public void SomeMethod()
            {
            }
        }

        [Fact]
        public void WhenServiceContractClassImplementsInterfaceHavingAttributesServiceAndServiceInheritable_Throw()
        {
            var config = BinderConfiguration.Default;
            Action activation = () => ContractOperation.ExpandWithInterfacesMarkedAsSubService(
                config.Binder,
                typeof(ServiceContractClassInheritsServiceAndSubServiceAttributes));
            Assert.Throws<ArgumentException>(activation.Invoke);
        }

        [Fact]
        public void MultiLevelSubServiceBindings()
        {
            // server
            var serverBinder = new TestServerBinder();
            Assert.Equal(5, serverBinder.Bind<IOuter>(null!));
            var methods = string.Join(",", serverBinder.Methods.OrderBy(_ => _));

            Assert.Equal("/other/C,/other/D,/outer/A,/outer/B,/outer/C", methods);
        }

        [Service("outer")]
        public interface IOuter : IMiddle, IOtherMiddle
        {
            void A();
        }

        [SubService]
        public interface IMiddle : IInner
        {
            void B();
        }
        [SubService]
        public interface IInner
        {
            void C();
        }

        [Service("other")]
        public interface IOtherMiddle : IInner
        {
            void D();
        }

#pragma warning disable CS0618 // Empty
        [Theory]
        [InlineData(nameof(IAllOptions.Client_AsyncUnary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Client_BlockingUnary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallOptions, (int)ResultKind.Sync, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Client_ClientStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None, (int)ResultKind.Grpc)]
        [InlineData(nameof(IAllOptions.Client_Duplex), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None, (int)ResultKind.Grpc)]
        [InlineData(nameof(IAllOptions.Client_ServerStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallOptions, (int)ResultKind.Grpc, (int)VoidKind.None, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Server_ClientStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Grpc)]
        [InlineData(nameof(IAllOptions.Server_Duplex), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Grpc)]
        [InlineData(nameof(IAllOptions.Server_ServerStreaming), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Server_Unary), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.ServerCallContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_CancellationToken), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Sync, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_CancellationToken_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Sync, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_CancellationToken_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Sync, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Sync, (int)VoidKind.Response, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Sync, (int)VoidKind.Response, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_BlockingUnary_CancellationToken_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Sync, (int)VoidKind.Response, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Shared_Duplex_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_Duplex_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_Duplex_CancellationToken), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]

        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_CancellationToken), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.AsyncEnumerable, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.AsyncEnumerable, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_CancellationToken_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.AsyncEnumerable, (int)VoidKind.Request, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_CancellationToken), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_CancellationToken_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.AsyncEnumerable)]

        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_CancellationToken), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_CancellationToken_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_CancellationToken_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_TaskUnary_CancellationToken_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_CancellationToken), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.AsyncEnumerable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_CancellationToken_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.AsyncEnumerable)]

        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_CancellationToken), typeof(HelloRequest), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_CancellationToken_VoidVoid), typeof(Empty), typeof(Empty), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.Both, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_CancellationToken_VoidVal), typeof(Empty), typeof(HelloReply), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_Context_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_NoContext_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskUnary_CancellationToken_ValVoid), typeof(HelloRequest), typeof(Empty), MethodType.Unary, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.Sync)]

        // observable
        [InlineData(nameof(IAllOptions.Shared_Duplex_Context_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CallContext, (int)ResultKind.Observable, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_Duplex_NoContext_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.NoContext, (int)ResultKind.Observable, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_Duplex_CancellationToken_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.DuplexStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.Observable, (int)VoidKind.None, (int)ResultKind.Observable)]

        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_Context_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.Observable, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_NoContext_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.Observable, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_CancellationToken_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.Observable, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_Context_VoidVal_Observable), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.Observable, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_NoContext_VoidVal_Observable), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.Observable, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_CancellationToken_VoidVal_Observable), typeof(Empty), typeof(HelloReply), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.Observable, (int)VoidKind.Request, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_Context_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_NoContext_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_CancellationToken_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_Context_ValVoid_Observable), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_NoContext_ValVoid_Observable), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_TaskClientStreaming_CancellationToken_ValVoid_Observable), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.Task, (int)VoidKind.Response, (int)ResultKind.Observable)]

        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_Context_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_NoContext_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_CancellationToken_Observable), typeof(HelloRequest), typeof(HelloReply), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.None, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_Context_ValVoid_Observable), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_NoContext_ValVoid_Observable), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.Observable)]
        [InlineData(nameof(IAllOptions.Shared_ValueTaskClientStreaming_CancellationToken_ValVoid_Observable), typeof(HelloRequest), typeof(Empty), MethodType.ClientStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTask, (int)VoidKind.Response, (int)ResultKind.Observable)]

        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_T_Stream_NoContext), typeof(Empty), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.TaskStream, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_T_Stream_CancellationToken), typeof(Empty), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.TaskStream, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_T_Stream_Context), typeof(Empty), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.TaskStream, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_T_Stream_Arg_NoContext), typeof(HelloRequest), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.TaskStream, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_T_Stream_Arg_CancellationToken), typeof(HelloRequest), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.TaskStream, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_T_Stream_Arg_Context), typeof(HelloRequest), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.TaskStream, (int)VoidKind.None, (int)ResultKind.Sync)]

        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_VT_Stream_NoContext), typeof(Empty), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTaskStream, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_VT_Stream_CancellationToken), typeof(Empty), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTaskStream, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_VT_Stream_Context), typeof(Empty), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTaskStream, (int)VoidKind.Request, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_VT_Stream_Arg_NoContext), typeof(HelloRequest), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.NoContext, (int)ResultKind.ValueTaskStream, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_VT_Stream_Arg_CancellationToken), typeof(HelloRequest), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CancellationToken, (int)ResultKind.ValueTaskStream, (int)VoidKind.None, (int)ResultKind.Sync)]
        [InlineData(nameof(IAllOptions.Shared_ServerStreaming_VT_Stream_Arg_Context), typeof(HelloRequest), typeof(BytesValue), MethodType.ServerStreaming, (int)ContextKind.CallContext, (int)ResultKind.ValueTaskStream, (int)VoidKind.None, (int)ResultKind.Sync)]
        public void CheckMethodIdentification(string name, Type from, Type to, MethodType methodType, int context, int result, int @void, int arg)
        {
            var method = typeof(IAllOptions).GetMethod(name);
            Assert.NotNull(method);
            var config = BinderConfiguration.Default;
            if (!ContractOperation.TryIdentifySignature(method!, config, out var operation, null))
            {
                var sig = ContractOperation.GetSignature(config.MarshallerCache, method!, null);
                Assert.Fail(sig.ToString());
            }
            Assert.Equal(method, operation.Method);
            Assert.Equal(methodType, operation.MethodType);
            Assert.Equal((ContextKind)context, operation.Context);
            Assert.Equal(method!.Name, operation.Name);
            Assert.Equal((ResultKind)arg, operation.Arg);
            Assert.Equal((ResultKind)result, operation.Result);
            Assert.Equal(from, operation.From);
            Assert.Equal(to, operation.To);
            Assert.Equal((VoidKind)@void, operation.Void);
        }

        [Fact]
        public void BindServer()
        {
            var expected = typeof(IAllOptions).GetMethods().Select(m => m.Name).Where(s => !s.StartsWith("Client_") && !s.StartsWith("Server_")).ToArray();
            Array.Sort(expected);
            var server = new TestBinder(_output);
            var obj = new MyServer();
            int count = server.Bind(this, typeof(MyServer), null, obj);
            _output.WriteLine($"Bound: {count} methods");
            Assert.Equal(expected, server.Collect());
        }

        internal sealed class TestBinder(ITestOutputHelper log) : ServerBinder
        {
            private readonly List<string> _methods = [];
            protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
            {
                try
                {
                    switch (method.Type)
                    {
                        case MethodType.Unary:
                            stub.CreateDelegate<UnaryServerMethod<TRequest, TResponse>>();
                            break;
                        case MethodType.ClientStreaming:
                            stub.CreateDelegate<ClientStreamingServerMethod<TRequest, TResponse>>();
                            break;
                        case MethodType.ServerStreaming:
                            stub.CreateDelegate<ServerStreamingServerMethod<TRequest, TResponse>>();
                            break;
                        case MethodType.DuplexStreaming:
                            stub.CreateDelegate<DuplexStreamingServerMethod<TRequest, TResponse>>();
                            break;
                        default:
                            return false;
                    }
                }
                catch (Exception ex)
                {
                    log.WriteLine($"Failed to bind {stub.Method.Name}: {ex.Message}");
                    return false;
                }
                _methods.Add(stub.Method.Name);
                return true;
            }

            public string[] Collect()
            {
                _methods.Sort();
                var arr = _methods.ToArray();
                _methods.Clear(); // reset
                return arr;
            }
        }

        [Fact]
        public void EmitClientProxy()
        {
            int errorCount = 0;
            HashSet<string> permitted = [
                "Call options not supported: IAllOptions.Client_BlockingUnary",
                "Call options not supported: IAllOptions.Client_AsyncUnary",
                "Call options not supported: IAllOptions.Client_ClientStreaming",
                "Call options not supported: IAllOptions.Client_Duplex",
                "Call options not supported: IAllOptions.Client_ServerStreaming",
            ];
            var factory = ProxyEmitter.CreateFactory<IAllOptions>(BinderConfiguration.Default, s =>
            {
                if (!s.Contains("IAllOptions.Server_") && !permitted.Contains(s))
                {
                    errorCount++;
                }
                _output.WriteLine(s);
            });

            Assert.NotNull(factory(NullCallInvoker.Instance));
            Assert.Equal(0, errorCount);
        }

        sealed class NullCallInvoker : CallInvoker
        {
            private NullCallInvoker() { }
            public static CallInvoker Instance { get; } = new NullCallInvoker();

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            {
                throw new NotSupportedException();
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            {
                throw new NotSupportedException();
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            {
                throw new NotSupportedException();
            }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            {
                throw new NotSupportedException();
            }

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            {
                throw new NotSupportedException();
            }
        }

        [Fact]
        public void WriteAllMethodSignatures()
        {
            var list = new List<(MethodType Kind, string Signature)>();
            var sb = new StringBuilder();
            var binder = BinderConfiguration.Default;
            foreach (var method in typeof(IAllOptions).GetMethods())
            {
                if (ContractOperation.TryIdentifySignature(method, binder, out var operation, null))
                {
                    sb.Clear();
                    sb.Append(Sanitize(method.ReturnType)).Append(" Foo(");
                    var p = method.GetParameters();
                    for (int i = 0; i < p.Length; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        sb.Append(Sanitize(p[i].ParameterType));
                    }
                    sb.Append(')');
                    list.Add((operation.MethodType, sb.ToString()));
                }
            }

            foreach (var grp in list.GroupBy(x => x.Kind))
            {
                _output.WriteLine(grp.Key.ToString());
                _output.WriteLine("");
                foreach (var (kind, signature) in grp.OrderBy(x => x.Signature))
                    _output.WriteLine(signature);
                _output.WriteLine("");
            }

            static string Sanitize(Type type)
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

    class MyServer : IAllOptions
    {
        public AsyncUnaryCall<HelloReply> Client_AsyncUnary(HelloRequest request, CallOptions options)
        {
            throw new NotSupportedException();
        }

        public HelloReply Client_BlockingUnary(HelloRequest request, CallOptions options)
        {
            throw new NotSupportedException();
        }

        public AsyncClientStreamingCall<HelloRequest, HelloReply> Client_ClientStreaming(CallOptions options)
        {
            throw new NotSupportedException();
        }

        public AsyncDuplexStreamingCall<HelloRequest, HelloReply> Client_Duplex(CallOptions options)
        {
            throw new NotSupportedException();
        }

        public AsyncServerStreamingCall<HelloReply> Client_ServerStreaming(HelloRequest request, CallOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Server_ClientStreaming(IAsyncStreamReader<HelloRequest> request, ServerCallContext context)
        {
            throw new NotSupportedException();
        }

        public Task Server_Duplex(IAsyncStreamReader<HelloRequest> request, IServerStreamWriter<HelloReply> response, ServerCallContext context)
        {
            throw new NotSupportedException();
        }

        public Task Server_ServerStreaming(HelloRequest request, IServerStreamWriter<HelloReply> response, ServerCallContext context)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Server_Unary(HelloRequest request, ServerCallContext context)
        {
            throw new NotSupportedException();
        }

        public HelloReply Shared_BlockingUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void Shared_BlockingUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public HelloReply Shared_BlockingUnary_CancellationToken_VoidVal(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void Shared_BlockingUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public HelloReply Shared_BlockingUnary_Context(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public void Shared_BlockingUnary_Context_ValVoid(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public HelloReply Shared_BlockingUnary_Context_VoidVal(CallContext context)
        {
            throw new NotSupportedException();
        }

        public void Shared_BlockingUnary_Context_VoidVoid(CallContext context)
        {
            throw new NotSupportedException();
        }

        public HelloReply Shared_BlockingUnary_NoContext(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public void Shared_BlockingUnary_NoContext_ValVoid(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public HelloReply Shared_BlockingUnary_NoContext_VoidVal()
        {
            throw new NotSupportedException();
        }

        public void Shared_BlockingUnary_NoContext_VoidVoid()
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_Duplex_CancellationToken(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_Duplex_CancellationToken_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_Duplex_Context(IAsyncEnumerable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_Duplex_Context_Observable(IObservable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_Duplex_NoContext(IAsyncEnumerable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_Duplex_NoContext_Observable(IObservable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_CancellationToken(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_ServerStreaming_CancellationToken_Observable(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_CancellationToken_VoidVal(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_ServerStreaming_CancellationToken_VoidVal_Observable(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_ServerStreaming_Context_Observable(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context_VoidVal(CallContext context)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_ServerStreaming_Context_VoidVal_Observable(CallContext context)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_ServerStreaming_NoContext_Observable(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext_VoidVal()
        {
            throw new NotSupportedException();
        }

        public IObservable<HelloReply> Shared_ServerStreaming_NoContext_VoidVal_Observable()
        {
            throw new NotSupportedException();
        }

        public Task<Stream> Shared_ServerStreaming_T_Stream_Arg_CancellationToken(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> Shared_ServerStreaming_T_Stream_Arg_Context(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> Shared_ServerStreaming_T_Stream_Arg_NoContext(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> Shared_ServerStreaming_T_Stream_CancellationToken(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> Shared_ServerStreaming_T_Stream_Context(CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> Shared_ServerStreaming_T_Stream_NoContext()
        {
            throw new NotSupportedException();
        }

        public ValueTask<Stream> Shared_ServerStreaming_VT_Stream_Arg_CancellationToken(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<Stream> Shared_ServerStreaming_VT_Stream_Arg_Context(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask<Stream> Shared_ServerStreaming_VT_Stream_Arg_NoContext(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public ValueTask<Stream> Shared_ServerStreaming_VT_Stream_CancellationToken(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<Stream> Shared_ServerStreaming_VT_Stream_Context(CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask<Stream> Shared_ServerStreaming_VT_Stream_NoContext()
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskClientStreaming_CancellationToken(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskClientStreaming_CancellationToken_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskClientStreaming_CancellationToken_ValVoid(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskClientStreaming_CancellationToken_ValVoid_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskClientStreaming_Context_Observable(IObservable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskClientStreaming_Context_ValVoid(IAsyncEnumerable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskClientStreaming_Context_ValVoid_Observable(IObservable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskClientStreaming_NoContext_Observable(IObservable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskClientStreaming_NoContext_ValVoid(IAsyncEnumerable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskClientStreaming_NoContext_ValVoid_Observable(IObservable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskUnary_CancellationToken_VoidVal(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskUnary_Context(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskUnary_Context_ValVoid(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskUnary_Context_VoidVal(CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskUnary_Context_VoidVoid(CallContext context)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskUnary_NoContext(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskUnary_NoContext_ValVoid(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<HelloReply> Shared_TaskUnary_NoContext_VoidVal()
        {
            throw new NotSupportedException();
        }

        public Task Shared_TaskUnary_NoContext_VoidVoid()
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_CancellationToken(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_CancellationToken_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskClientStreaming_CancellationToken_ValVoid(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskClientStreaming_CancellationToken_ValVoid_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_Context_Observable(IObservable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskClientStreaming_Context_ValVoid(IAsyncEnumerable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskClientStreaming_Context_ValVoid_Observable(IObservable<HelloRequest> request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_NoContext_Observable(IObservable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskClientStreaming_NoContext_ValVoid(IAsyncEnumerable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskClientStreaming_NoContext_ValVoid_Observable(IObservable<HelloRequest> request)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskUnary_CancellationToken_VoidVal(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskUnary_Context(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskUnary_Context_ValVoid(HelloRequest request, CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskUnary_Context_VoidVal(CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskUnary_Context_VoidVoid(CallContext context)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskUnary_NoContext_ValVoid(HelloRequest request)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext_VoidVal()
        {
            throw new NotSupportedException();
        }

        public ValueTask Shared_ValueTaskUnary_NoContext_VoidVoid()
        {
            throw new NotSupportedException();
        }
    }
}
