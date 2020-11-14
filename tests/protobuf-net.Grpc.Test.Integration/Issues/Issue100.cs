using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace protobuf_net.Grpc.Test.Integration.Issues
{
    public class Issue100 : IClassFixture<Issue100.Issue100ServerFixture>
    {
#if !NET472 // need ABW
        [Fact]
        public async Task MeasuredSerialize()
        {
            var obj = await ((ITest)_server).GetTestInstance();
            var ms = new MemoryStream();
            Serializer.Serialize(ms, obj); // regular serialize
            Assert.Equal(14, ms.Length);
            var expected = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
            Log(expected);
            Assert.Equal("0A-0C-0A-05-0A-03-64-65-66-12-03-61-62-63", expected);

            /*
Field #1: 0A String Length = 12, Hex = 0C, UTF8 = "  defabc"
As sub-object :
  Field #1: 0A String Length = 5, Hex = 0C, UTF8 = " def"
  As sub-object :
    Field #1: 0A String Length = 3, Hex = 0C, UTF8 = "def"
  Field #2: 65 String Length = 3, Hex = 66, UTF8 = "abc"
             */


            // now try measured
            if ((object)RuntimeTypeModel.Default is IMeasuredProtoOutput<IBufferWriter<byte>> writer)
            {
                using var measured = writer.Measure(obj);
                Assert.Equal(ms.Length, measured.Length);

                var abw = new ArrayBufferWriter<byte>();
                writer.Serialize(measured, abw);
                Assert.Equal(ms.Length, abw.WrittenCount);

                var mem = abw.WrittenMemory;
                Assert.True(MemoryMarshal.TryGetArray(mem, out var segment));
                var actual = BitConverter.ToString(segment.Array, segment.Offset, segment.Count);
                Log(actual);
                Assert.Equal(expected, actual);
            }
        }
#endif

        [Service]
        public interface ITest
        {
            ValueTask<TestObject> GetTestInstance();
        }


#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0051, IDE0052 // "unused" things; they are, but it depends on the TFM
        private readonly ITestOutputHelper _log;
        private readonly Issue100ServerFixture _server;
        private void Log(string message) => _log?.WriteLine(message);
#pragma warning restore IDE0051, IDE0052
#pragma warning restore IDE0079 // Remove unnecessary suppression

        private int Port => _server.Port;

        public Issue100(Issue100ServerFixture server, ITestOutputHelper log)
        {
            _server = server;
            _log = log;
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
        }

        public class Issue100ServerFixture : ITest, IDisposable
        {
            public int Port { get; } = PortManager.GetNextPort();

            public void Dispose()
            {
                _server.KillAsync();
                GC.SuppressFinalize(this);
            }

            private readonly Server? _server;
            public Issue100ServerFixture()
            {
                _server = new Server
                {
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                _server.Services.AddCodeFirst(this);
                _server.Start();
            }
            ValueTask<TestObject> ITest.GetTestInstance()
            {
                var obj = new TestObject
                {
                    Test = new TestThingy { SomeText = "abc", SomeText2 = "def" }
                };
                return new ValueTask<TestObject>(obj);
            }
        }

        [Fact]
        public async Task Issue100_UnmanagedClient()
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            try
            {
                var client = channel.CreateGrpcService<ITest>();
                var obj = await client.GetTestInstance();
                Assert.Equal("abc", obj.Test.SomeText);
                Assert.Equal("def", obj.Test.SomeText2);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

#if !(NET461 || NET472)
        [Fact]
        public async Task Issue100_ManagedClient()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<ITest>();
            var obj = await client.GetTestInstance();
            Assert.Equal("abc", obj.Test.SomeText);
            Assert.Equal("def", obj.Test.SomeText2);
        }
#endif


        [ProtoContract]
        public class TestObject
        {
            [ProtoMember(1)]
            public TestThingy Test { get; set; }
        }

        [ProtoContract]
        [ProtoInclude(1, typeof(TestThingy))]
        public abstract class TestBase
        {
            [ProtoMember(2)]
            public string SomeText { get; set; }

            public abstract string SomeText2 { get; set; }
        }

        [ProtoContract]
        public class TestThingy : TestBase
        {
            [ProtoMember(1)]
            public override string SomeText2 { get; set; }
        }
    }
}