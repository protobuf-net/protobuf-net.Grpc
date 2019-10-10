namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Marker interface for describing simple gRPC services.
    /// When a type implements this interface, the type name (without any prefix or other qualification)
    /// will be used as the name for resolving the gRPC service. All public methods defined by the type will be mapped as gRPC methods,
    /// using their declared name. 
    /// </summary>
    public interface IGrpcService {}
}