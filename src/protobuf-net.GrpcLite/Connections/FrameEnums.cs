namespace ProtoBuf.Grpc.Lite.Connections;

[Flags]
internal enum FrameFlags : byte
{
    None = 0,
    RecycleBuffer = 1 << 0,
    HeaderReserved = 1 << 1,
}

/// <summary>
/// The types of <see cref="Frame"/> being communicated.
/// </summary>
public enum FrameKind : byte
{
    /// <summary>
    /// Invalid; should never be used.
    /// </summary>
    None = 0,
    /// <summary>
    /// Used at the connection level to perform initial negotiation.
    /// </summary>
    Handshake = 1,
    /// <summary>
    /// Represents the start of a request/response stream.
    /// </summary>
    Header = 2,
    /// <summary>
    /// Represents a data element in a request/response stream.
    /// </summary>
    Payload = 3,
    /// <summary>
    /// Represents the end of a request/response stream.
    /// </summary>
    Trailer = 4,
    /// <summary>
    /// Used to terminate a request.
    /// </summary>
    Cancel = 5,

    /// <summary>
    /// Signals the end of a connection.
    /// </summary>
    CloseConnection,
    /// <summary>
    /// Tests a connection for activity.
    /// </summary>
    Ping,
    /// <summary>
    /// Invalid; do not use.
    /// </summary>
    [Obsolete("remove this later; should be a structured response status")]
    MethodNotFound,
    
}