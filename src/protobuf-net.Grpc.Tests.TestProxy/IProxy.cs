using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;

namespace protobuf_net.Grpc.Tests.TestProxy;

[Service]
public interface IProxy
{
    [Operation]
    ValueTask<Out> Operation(In request, CallContext callContext = default);
}

[ProtoContract]
public class In { }

[ProtoContract]
public class Out { }
