using System;

namespace ProtoBuf.Grpc.Lite.Connections;

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
    ConnectionHandshake = 1,
    /// <summary>
    /// Represents the start of a request/response stream.
    /// </summary>
    StreamHeader = 2,
    /// <summary>
    /// Represents a data element in a request/response stream.
    /// </summary>
    StreamPayload = 3,
    /// <summary>
    /// Represents the end of a request/response stream.
    /// </summary>
    StreamTrailer = 4,
    /// <summary>
    /// Used to terminate a request.
    /// </summary>
    StreamCancel = 5,
    /// <summary>
    /// Signals the end of a connection.
    /// </summary>
    ConnectionClose,
    /// <summary>
    /// Tests a connection for activity.
    /// </summary>
    ConnectionPing,
    /// <summary>
    /// Indicates that the specified stream is exceeding quota, and should suspend transmit; failure to do so may cause the stream or connection to be terminated.
    /// </summary>
    StreamSuspend,
    /// <summary>
    /// Indicates that the specified suspended stream may resume transmit.
    /// </summary>
    StreamResume,
}

/// <summary>
/// Flags that apply to individual frames
/// </summary>
public enum FrameWriteFlags : byte
{
    /// <summary>
    /// No flags
    /// </summary>
    None = 0,

    /// <summary>
    /// Hint that the write may be buffered and need not go out on the wire immediately.
    /// gRPC is free to buffer the message until the next non-buffered
    /// write, or until write stream completion, but it need not buffer completely or at all.
    /// </summary>
    BufferHint = 1 << 0
}