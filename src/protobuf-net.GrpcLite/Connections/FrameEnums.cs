namespace ProtoBuf.Grpc.Lite.Connections;

[Flags]
internal enum FrameFlags : byte
{
    None = 0,
    RecycleBuffer = 1 << 0,
    HeaderReserved = 1 << 1,
}

public enum FrameKind : byte
{
    None = 0,
    Handshake = 1,
    Header = 2,
    Payload = 3,
    Trailer = 4,
    Cancel = 5,


    CloseConnection,
    Ping,
    [Obsolete("remove this later; should be a structured response status")]
    MethodNotFound,
    
}