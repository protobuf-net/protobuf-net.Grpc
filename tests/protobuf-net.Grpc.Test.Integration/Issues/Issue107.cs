using Grpc.Core;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.Grpc.Test.Integration.Issues
{
    public class Issue107 : IClassFixture<Issue107.MyServer<Issue107.Derived<string>>>
    {

        public abstract class MyServerBase // exists purely for .cctor init timing reasons
        {
            static MyServerBase()
            {
                RuntimeTypeModel.Default[typeof(Base<string>)].AddSubType(42, typeof(Derived<string>));
            }
        }
        public class MyServer<T> : MyServerBase, IDisposable, ICalculatorService<T>
        {
            Server _server;
            public MyServer()
            {
                _server = new Server
                {
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                _server.Services.AddCodeFirst(this, interceptors: new[] { SimpleRpcExceptionsInterceptor.Instance });
                _server.Start();
            }
            public int Port { get; } = PortManager.GetNextPort();

            void IDisposable.Dispose() => _ = _server.ShutdownAsync();

            async IAsyncEnumerable<T> ICalculatorService<T>.Duplex(IAsyncEnumerable<T> input)
            {
                await foreach (var value in input)
                {
                    yield return value;
                }
            }
        }

        private readonly MyServer<Derived<string>> _server;
        private readonly ITestOutputHelper _log;
        private void Log(string message) => _log?.WriteLine(message);
        private int Port => _server.Port;
        public Issue107(ITestOutputHelper log, MyServer<Derived<string>> server)
        {
            _log = log;
            _server = server;
        }

        [Fact]
        public void IsSubTypeConfigured()
        {
            var subType = Assert.Single(RuntimeTypeModel.Default[typeof(Base<string>)].GetSubtypes());
            Assert.Equal(42, subType.FieldNumber);
            Assert.Equal(typeof(Derived<string>), subType.DerivedType.Type);
        }

        [Fact]
        public async Task GenericsTest()
        {
            Channel channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            var calculator = channel.CreateGrpcService<ICalculatorService<Derived<string>>>();

            var input = new[] { "1", "2" };
            var inputString = string.Join(",", input);
            Log($"Sending: {inputString}");

            var stream = calculator.Duplex(ToAsyncEnumerable(input.Select(x => new Derived<string> { Value = x })));

            var output = await ToArrayAsync(stream);
            var outputString = string.Join(",", output.Select(x => x.Value));
            Log($"Received: {inputString}");
            Assert.Equal(inputString, outputString);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        async static IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var item in source)
                yield return item;
        }
        async static ValueTask<T[]> ToArrayAsync<T>(IAsyncEnumerable<T> source)
        {
            var list = new List<T>();
            await foreach (var item in source.ConfigureAwait(false))
                list.Add(item);
            return list.ToArray();
        }

        [DataContract]
        public class Derived<T> : Base<T>
        {
        }

        [DataContract]
        public class Base<T>
        {
            [DataMember(Order = 1)]
            public T Value { get; set; }
        }

        [ServiceContract]
        public interface ICalculatorService<T>
        {
            IAsyncEnumerable<T> Duplex(IAsyncEnumerable<T> input);
        }
    }
}
