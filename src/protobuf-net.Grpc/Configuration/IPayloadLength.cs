namespace ProtoBuf.Grpc.Configuration;

/// <summary>
/// Allows capture of the payload length as part of the marshaller serialize/deserialize operation.
/// </summary>
public interface IPayloadLength
{
    /// <summary>
    /// Records the payload length.
    /// </summary>
    void SetLength(long length);
}
