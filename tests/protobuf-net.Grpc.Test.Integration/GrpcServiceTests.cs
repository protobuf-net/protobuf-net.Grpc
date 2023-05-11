#if !(NET462 || NET472)
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.Grpc.Test.Integration
{
    [DataContract]
    public class Apply
    {
        public Apply() { }
        public Apply(int x, int y) => (X, Y) = (x, y);

        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }
    }

    [DataContract]
    public class ApplyResponse
    {
        public ApplyResponse() { }
        public ApplyResponse(int result) => Result = result;

        [DataMember(Order = 1)]
        public int Result { get; set; }
    }

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
    public class ApplyServices : IGrpcService
    {
        public Task<ApplyResponse> Add(Apply request) => Task.FromResult(new ApplyResponse(request.X + request.Y));
        public Task<ApplyResponse> Mul(Apply request) => Task.FromResult(new ApplyResponse(request.X * request.Y));
        public Task<ApplyResponse> Sub(Apply request) => Task.FromResult(new ApplyResponse(request.X - request.Y));
        public Task<ApplyResponse> Div(Apply request) => Task.FromResult(new ApplyResponse(request.X / request.Y));
    }
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression

    [Service]
    public interface IInterceptedService
    {
        ValueTask<ApplyResponse> Add(Apply request);
    }
    public class InterceptedService : IInterceptedService
    {
        public ValueTask<ApplyResponse> Add(Apply request) => new ValueTask<ApplyResponse>(new ApplyResponse(request.X + request.Y));
    }

    [Service]
    public interface IOwnedMemoryService
    {
        ValueTask<WriteOnce> RoundTripZeroCopy(WriteOnce payload);
        ValueTask<WriteOnce> RoundTripClone(WriteOnce payload);
    }

    public class OwnedMemoryService : IOwnedMemoryService
    {
        public ValueTask<WriteOnce> RoundTripClone(WriteOnce payload)
        {
            using (payload) // we are responsible for cleaning this up
            {
                // lease a separate payload to return
                var copy = WriteOnce.Create(payload.Length);
                payload.Memory.CopyTo(copy.Memory);
                return new(copy);
            }
        }

        public ValueTask<WriteOnce> RoundTripZeroCopy(WriteOnce payload)
        {
            // return the same data *without disposing the original*
            return new(payload); 
        }
    }

    public sealed partial class WriteOnce : IMemoryOwner<byte>
    {
#if DEBUG
        
        private static long s_totalBytesInUse;
        public static long TotalBytesInUse => Volatile.Read(ref s_totalBytesInUse);
        public static void Reset() => Volatile.Write(ref s_totalBytesInUse, 0);

        static partial void AddTotalBytes(int delta) => Interlocked.Add(ref s_totalBytesInUse, delta);

#endif

        static partial void AddTotalBytes(int delta); // DEBUG only

        public int Length => _length;
        private byte[] _oversized;
        private int _length;

        public static WriteOnce Empty { get; } = new WriteOnce(Array.Empty<byte>(), 0);
        public static WriteOnce Create(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0) return Empty;
            var arr = ArrayPool<byte>.Shared.Rent(length);
            AddTotalBytes(arr.Length);
            return new WriteOnce(arr, length);
        }

        private WriteOnce(byte[] oversized, int length)
        {
            _oversized = oversized;
            _length = length;
        }

        public Memory<byte> Memory => new Memory<byte>(_oversized, 0, _length);

        public static Marshaller<WriteOnce> Marshaller { get; } = Marshallers.Create<WriteOnce>(
            Serialize, Deserialize);

        private static void Serialize(WriteOnce value, SerializationContext context)
        {
            // write then dispose; this is not a mistake - we want values to be recycled
            context.SetPayloadLength(value.Length);
            context.GetBufferWriter().Write(value.Memory.Span); // handles chunking internally
            context.Complete();
            value.Dispose();
        }

        public void Dispose()
        {
            var arr = Interlocked.Exchange(ref _oversized, Array.Empty<byte>());
            if (arr.Length != 0)
            {
                ArrayPool<byte>.Shared.Return(arr);
                AddTotalBytes(-arr.Length);
            }
        }

        private static WriteOnce Deserialize(DeserializationContext context)
        {
            var value = Create(context.PayloadLength);
            context.PayloadAsReadOnlySequence().CopyTo(value.Memory.Span);
            return value;
        }
    }

    [Serializable]
    public class AdhocRequest
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    [Serializable]
    public class AdhocResponse
    {
        public int Z { get; set; }
    }
    [Service]
    public interface IAdhocService
    {
        AdhocResponse AdhocMethod(AdhocRequest request);
    }

    public class AdhocService : IAdhocService
    {
        public AdhocResponse AdhocMethod(AdhocRequest request)
            => new AdhocResponse { Z = request.X + request.Y };
    }

    static class AdhocConfig
    {
        public static ClientFactory ClientFactory { get; }
            = ClientFactory.Create(BinderConfiguration.Create(new[] {
                    // we'll allow multiple marshallers to take a stab; protobuf-net first,
                    // then try BinaryFormatter for anything that protobuf-net can't handle

                    ProtoBufMarshallerFactory.Default,
#pragma warning disable CS0618 // Type or member is obsolete
                    BinaryFormatterMarshallerFactory.Default, // READ THE NOTES ON NOT DOING THIS
#pragma warning restore CS0618 // Type or member is obsolete
                    }));
    }

    public class GrpcServiceFixture : IAsyncDisposable
    {
        public int Port { get; } = PortManager.GetNextPort();
        private readonly Server _server;

        private readonly Interceptor _interceptor;
        public ITestOutputHelper? Output { get; set; }
        public void Log(string message) => Output?.WriteLine(message);
        public GrpcServiceFixture()
        {
            BinderConfiguration bc = AdhocConfig.ClientFactory; // could also be BinderConfiguration.Default
            bc.SetMarshaller(WriteOnce.Marshaller);
            _interceptor = new TestInterceptor(this);
#pragma warning disable CS0618 // Type or member is obsolete
            BinaryFormatterMarshallerFactory.I_Have_Read_The_Notes_On_Not_Using_BinaryFormatter = true;
            BinaryFormatterMarshallerFactory.I_Promise_Not_To_Do_This = true; // signed: Marc Gravell
#pragma warning restore CS0618 // Type or member is obsolete
            _server = new Server
            {
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            object nonGeneric = new ApplyServices();
            _server.Services.AddCodeFirst(nonGeneric);
            _server.Services.AddCodeFirst(new AdhocService(), AdhocConfig.ClientFactory);
            _server.Services.AddCodeFirst(new InterceptedService(), interceptors: new[] { _interceptor });
            _server.Services.AddCodeFirst(new OwnedMemoryService(), AdhocConfig.ClientFactory); // since we configured this
            _server.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await _server.ShutdownAsync();
        }
    }
    public class TestInterceptor : Interceptor
    {
        private readonly GrpcServiceFixture _parent;
        public void Log(string message) => _parent?.Log(message);
        public TestInterceptor(GrpcServiceFixture parent) => _parent = parent;
        private static string Me([CallerMemberName] string? caller = null) => caller ?? "(unknown)";
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            Log($"> {Me()}");
            var result = await base.UnaryServerHandler(request, context, continuation);
            Log($"< {Me()}");
            return result;
        }
    }


    public class GrpcServiceTests : IClassFixture<GrpcServiceFixture>, IDisposable
    {
        private readonly GrpcServiceFixture _fixture;
        public GrpcServiceTests(GrpcServiceFixture fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            if (fixture != null) fixture.Output = log;
        }

        private int Port => _fixture.Port;

        private void Log(string message) => _fixture?.Log(message);

        public void Dispose()
        {
            if (_fixture != null) _fixture.Output = null;
            GC.SuppressFinalize(this);
        }

        private static readonly ProtoBufMarshallerFactory
            EnableContextualSerializer = (ProtoBufMarshallerFactory)ProtoBufMarshallerFactory.Create(userState: new object()),
            DisableContextualSerializer = (ProtoBufMarshallerFactory)ProtoBufMarshallerFactory.Create(options: ProtoBufMarshallerFactory.Options.DisableContextualSerializer, userState: new object());

        [Fact]
        public async Task CanUseWriteOnceMemory()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{Port}");
            var svc = http.CreateGrpcService<IOwnedMemoryService>(AdhocConfig.ClientFactory);

#if DEBUG
            WriteOnce.Reset();
#endif
            using (var payload = WriteOnce.Create(114))
            {
                Assert.Equal(114, payload.Length);
#if DEBUG
                Assert.True(WriteOnce.TotalBytesInUse >= 114);
#endif
                using (var clone = await svc.RoundTripClone(payload))
                {
                    Assert.Equal(114, clone.Length);
                }
            }
#if DEBUG
            Assert.Equal(0, WriteOnce.TotalBytesInUse);
#endif
            using (var payload = WriteOnce.Create(114))
            {
                Assert.Equal(114, payload.Length);
#if DEBUG
                Assert.True(WriteOnce.TotalBytesInUse >= 114);
#endif
                using (var clone = await svc.RoundTripZeroCopy(payload))
                {
                    Assert.Equal(114, clone.Length);
                }
            }
#if DEBUG
            Assert.Equal(0, WriteOnce.TotalBytesInUse);
#endif
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CanCallAllApplyServicesUnaryAsync(bool disableContextual)
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{Port}");

            var request = new Apply { X = 6, Y = 3 };
            var marshaller = disableContextual ? DisableContextualSerializer : EnableContextualSerializer;
            var client = new GrpcClient(http, nameof(ApplyServices), BinderConfiguration.Create(new[] { marshaller }));

            Assert.Equal(nameof(ApplyServices), client.ToString());

#if DEBUG
            var uplevelReadsBefore = marshaller.UplevelBufferReadCount;
            var uplevelWritesBefore = marshaller.UplevelBufferWriteCount;
            Log($"Buffer usage before: {uplevelReadsBefore}/{uplevelWritesBefore}");
#endif

            var response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);

#if DEBUG
            var uplevelReadsAfter = marshaller.UplevelBufferReadCount;
            var uplevelWritesAfter = marshaller.UplevelBufferWriteCount;
            Log($"Buffer usage after: {uplevelReadsAfter}/{uplevelWritesAfter}");

#if PROTOBUFNET_BUFFERS
            bool expectContextual = true;
#else
            bool expectContextual = false;
#endif
            if (disableContextual) expectContextual = false;

            if (expectContextual)
            {
                Assert.True(uplevelReadsBefore < uplevelReadsAfter);
                Assert.True(uplevelWritesBefore < uplevelWritesAfter);
            }
            else
            {
                Assert.Equal(uplevelReadsBefore, uplevelReadsAfter);
                Assert.Equal(uplevelWritesBefore, uplevelWritesAfter);
            }
#endif
        }

        [Fact]
        public async Task CanCallAllApplyServicesTypedUnaryAsync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = http.CreateGrpcService(typeof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Add)));
            Assert.Equal(9, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Mul)));
            Assert.Equal(18, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Sub)));
            Assert.Equal(3, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Div)));
            Assert.Equal(2, response.Result);

            static MethodInfo GetMethod(string name) => typeof(ApplyServices).GetMethod(name)!;
        }

        [Fact]
        public void CanCallAllApplyServicesUnarySync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = new GrpcClient(http, nameof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);
        }

        [Fact]
        public void CanCallAllApplyServicesTypedUnarySync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = new GrpcClient(http, typeof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Add)));
            Assert.Equal(9, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Mul)));
            Assert.Equal(18, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Sub)));
            Assert.Equal(3, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Div)));
            Assert.Equal(2, response.Result);

            static MethodInfo GetMethod(string name) => typeof(ApplyServices).GetMethod(name)!;
        }

        [Fact]
        public void CanCallAdhocService()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{Port}");

            var request = new AdhocRequest { X = 12, Y = 7 };
            var client = http.CreateGrpcService<IAdhocService>(AdhocConfig.ClientFactory);
            var response = client.AdhocMethod(request);
            Assert.Equal(19, response.Z);
        }

        [Fact]
        public async Task CanCallInterceptedService()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = http.CreateGrpcService<IInterceptedService>();
            _fixture?.Log("> Add");
            var result = await client.Add(new Apply { X = 42, Y = 8 });
            _fixture?.Log("< Add");
            Assert.Equal(50, result.Result);
        }
    }
}
#endif
