using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.Grpc.Test.Integration
{
    public class IOStreamTestsFixture : IAsyncDisposable
    {
        private Server? _server;

        public ITestOutputHelper? Output { get; private set; }
        public void SetOutput(ITestOutputHelper? output) => Output = output;
        public void Log(string message)
        {
            var tmp = Output;
            if (tmp is object)
            {
                lock (tmp)
                {
                    tmp.WriteLine(DateTime.Now + ":" + message);
                }
            }
        }
        public IOStreamTestsFixture() { }

        public int Port { get; } = PortManager.GetNextPort();
        public void Init()
        {
            if (_server == null)
            {
                _server = new Server
                {
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                _server.Services.AddCodeFirst(new IOStreamServer(this));
                _server.Start();
            }
        }

        public ValueTask DisposeAsync() => _server == null ? default : new ValueTask(_server.ShutdownAsync());
    }

    [Service]
    public interface IIOStreamAPI
    {
        Stream SomeMethod(Stream value);
    }

    class IOStreamServer : IIOStreamAPI
    {
        readonly IOStreamTestsFixture _fixture;
        internal IOStreamServer(IOStreamTestsFixture fixture)
            => _fixture = fixture;
        public void Log(string message)
        {
            Debug.WriteLine(message);
            _fixture.Log(message);
        }

        public Stream SomeMethod(Stream value) => value;
        //{
        //    var ms = new MemoryStream();
        //    value.CopyTo(ms);
        //    ms.Position = 0;
        //    return ms;
        //}
    }

    public class NativeIOStreamTests : IOStreamTests
    {
        public NativeIOStreamTests(IOStreamTestsFixture fixture, ITestOutputHelper log) : base(fixture, log) { }
        protected override IAsyncDisposable CreateClient(out IIOStreamAPI client)
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            client = channel.CreateGrpcService<IIOStreamAPI>();
            return new DisposableChannel(channel);
        }
        sealed class DisposableChannel : IAsyncDisposable
        {
            private readonly Channel _channel;
            public DisposableChannel(Channel channel)
                => _channel = channel;
            public ValueTask DisposeAsync() => new ValueTask(_channel.ShutdownAsync());
        }
    }

#if !(NET461 || NET472)
    public class ManagedIOStreamTests : IOStreamTests
    {
        public override bool IsManagedClient => true;
        public ManagedIOStreamTests(IOStreamTestsFixture fixture, ITestOutputHelper log) : base(fixture, log) { }
        protected override IAsyncDisposable CreateClient(out IIOStreamAPI client)
        {
            var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            client = http.CreateGrpcService<IIOStreamAPI>();
            return new DisposableChannel(http);
        }
        sealed class DisposableChannel : IAsyncDisposable
        {
            private readonly global::Grpc.Net.Client.GrpcChannel _channel;
            public DisposableChannel(global::Grpc.Net.Client.GrpcChannel channel)
                => _channel = channel;
            public async ValueTask DisposeAsync()
            {
                await _channel.ShutdownAsync();
                _channel.Dispose();
            }
        }
    }
#endif

    public abstract class IOStreamTests : IClassFixture<IOStreamTestsFixture>, IDisposable
    {

        protected int Port => _fixture.Port;
        private readonly IOStreamTestsFixture _fixture;
        public IOStreamTests(IOStreamTestsFixture fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            fixture.Init();
            fixture?.SetOutput(log);
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
        }

        public virtual bool IsManagedClient => false;

        public void Dispose()
        {
            _fixture?.SetOutput(null);
            GC.SuppressFinalize(this);
        }

        protected abstract IAsyncDisposable CreateClient(out IIOStreamAPI client);

        [Fact]
        public async Task RoundTripStream()
        {
            var path1 = Path.GetTempFileName();
            var path2 = Path.GetTempFileName();
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                const int LENGTH = 20000;
                int remaining = LENGTH;
                var rand = new Random();
                using var file1 = File.Create(path1);
                while (remaining > 0)
                {
                    rand.NextBytes(buffer);
                    int take = Math.Min(remaining, buffer.Length);
                    await file1.WriteAsync(buffer, 0, take);
                    remaining -= take;
                }
                file1.Position = 0; // rewind
                Assert.Equal(LENGTH, file1.Length);

                await using (CreateClient(out var client))
                {
                    var recv = client.SomeMethod(file1);

                    using var file2 = File.Create(path2);
                    await recv.CopyToAsync(file2);

                    Assert.Equal(LENGTH, file2.Length);
                    file1.Position = 0;
                    file2.Position = 0;
                    for (int i = 0; i < LENGTH; i++)
                    {
                        var b = file1.ReadByte();
                        Assert.True(b >= 0);
                        Assert.Equal(b, file2.ReadByte());
                    }
                    Assert.True(file1.ReadByte() < 0);
                    Assert.True(file2.ReadByte() < 0);
                }
            }
            finally
            {
                try { File.Delete(path1); } catch { }
                try { File.Delete(path2); } catch { }
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

    }
}
