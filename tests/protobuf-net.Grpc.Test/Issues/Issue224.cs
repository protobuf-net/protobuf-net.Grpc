using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace protobuf_net.Grpc.Test.Issues
{
    public class Issue224
    {
        [Fact]
        public async Task CustomBinderExample()
        {
            var binder = new MyBinder();
            binder.Add(typeof(IMyService), "Foo");
            binder.Add(typeof(IMyService).GetMethod(nameof(IMyService.SomeMethodAsync))!, "Bar");

            // server
            var serverBinder = new TestServerBinder();
            Assert.Equal(1, serverBinder.Bind<IMyService>(null!, binderConfiguration: binder.BinderConfiguration));
            Assert.Equal("/Foo/Bar", Assert.Single(serverBinder.Methods));

            // client 
            var channel = new TestChannel("abc");
            IMyService service = channel.CreateGrpcService<IMyService>(binder.ClientFactory);
            await service.SomeMethodAsync();
            Assert.Equal("abc:/Foo/Bar", Assert.Single(channel.Calls));
        }
        public interface IMyService
        {
            ValueTask SomeMethodAsync(CancellationToken cancellation = default);
        }
        sealed class MyBinder : ServiceBinder
        {
            public MyBinder()
            {
                BinderConfiguration = BinderConfiguration.Create(binder: this);
                ClientFactory = ClientFactory.Create(BinderConfiguration);
            }

            readonly ConcurrentDictionary<Type, string> _services = new();
            readonly ConcurrentDictionary<MethodInfo, string> _operations = new();

            public override bool IsServiceContract(Type contractType, out string? name)
                => _services.TryGetValue(contractType, out name);
            public override bool IsOperationContract(MethodInfo method, out string? name)
                => _operations.TryGetValue(method, out name);

            public bool Add(Type service, string name)
                => _services.TryAdd(service, name);
            public bool Add(MethodInfo operation, string name)
                => _operations.TryAdd(operation, name);

            public BinderConfiguration BinderConfiguration { get; } 
            public ClientFactory ClientFactory { get; }
        }
    }
}
