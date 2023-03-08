using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;
using System;
using System.Threading.Tasks;
using Xunit;
#if PROTOBUFNET_BUFFERS
using ProtoBuf.Serializers;
#endif

namespace protobuf_net.Grpc.Test.Integration.Issues
{
    public partial class Issue282 : IClassFixture<Issue282.ServerFixture>
    {
        private readonly ServerFixture _server;
        private int Port => _server.Port;

        public Issue282(ServerFixture server) => _server = server;

        [Fact]
        public async Task ExecuteAsync()
        {
            Channel channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            var svc = channel.CreateGrpcService<IMyService>();
            var payload = new setCommCellIdRequest
            {
                commCellId = 12,
                isDisposed = true,
                m_pEvAlertObj = new IntPtr(42),
            };

#if PROTOBUFNET_BUFFERS
            var doubled = await svc.DoubleAsync(payload);

            Assert.Equal(24, doubled.commCellId);
            Assert.True(doubled.isDisposed);
            Assert.Equal(84, (nint)doubled.m_pEvAlertObj);
#else
            // we expect this to fail on v2, as the custom serializer API does not exist
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.DoubleAsync(payload));
            Assert.Equal("No serializer defined for type: System.IntPtr", ex.Message);
#endif
        }

        [Service]
        public interface IMyService
        {
            ValueTask<setCommCellIdRequest> DoubleAsync(setCommCellIdRequest value);
        }

        [ProtoContract]
        public class setCommCellIdRequest
        {
            [ProtoMember(1)]
            public Int64 commCellId { get; set; }

            [ProtoMember(2)]
            public IntPtr m_pEvAlertObj { get; set; }

            [ProtoMember(3)]
            public Boolean isDisposed { get; set; }
        }

        static partial void ConfigureModel(RuntimeTypeModel model);

#if PROTOBUFNET_BUFFERS // needs protobuf-net v3
        static partial void ConfigureModel(RuntimeTypeModel model)
        {
            model.Add<nint>(false).SerializerType = typeof(IntPtrSerializer);
            model.Add<nuint>(false).SerializerType = typeof(IntPtrSerializer);
        }
        public sealed class IntPtrSerializer : ISerializer<nint>, ISerializer<nuint>
        {
            public SerializerFeatures Features
                => SerializerFeatures.CategoryScalar | SerializerFeatures.WireTypeVarint;
            public nint Read(ref ProtoReader.State state, IntPtr value)
                => new IntPtr(state.ReadInt64());
            public UIntPtr Read(ref ProtoReader.State state, UIntPtr value)
                => new UIntPtr(state.ReadUInt64());
            public void Write(ref ProtoWriter.State state, IntPtr value)
                => state.WriteInt64(value.ToInt64());
            public void Write(ref ProtoWriter.State state, UIntPtr value)
                => state.WriteUInt64(value.ToUInt64());
        }
#endif

        public class ServerFixture : IMyService
        {
            public int Port { get; } = PortManager.GetNextPort();

            public void Dispose()
            {
                _ = _server?.KillAsync();
                GC.SuppressFinalize(this);
            }

            ValueTask<setCommCellIdRequest> IMyService.DoubleAsync(setCommCellIdRequest value)
            {
                return new(new setCommCellIdRequest
                {
                    commCellId = 2 * value.commCellId,
                    isDisposed = value.isDisposed,
                    m_pEvAlertObj = 2 * (nint)value.m_pEvAlertObj,
                });
            }

            private readonly Server? _server;
            public ServerFixture()
            {
                GrpcClientFactory.AllowUnencryptedHttp2 = true;
                ConfigureModel(RuntimeTypeModel.Default);
                _server = new Server
                {
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                _server.Services.AddCodeFirst(this);
                _server.Start();
            }
        }
    }
}
