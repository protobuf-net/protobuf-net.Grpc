namespace ProtoBuf.Grpc.Lite;

[Flags]
internal enum FrameFlags : byte
{
    None = 0,
    RecycleBuffer = 1 << 0,
    HeaderReserved = 1 << 1,
}

[Flags]
internal enum PayloadFlags
{
    None = 0,
    EndItem = 1 << 0, // terminates a single streaming object (which could be split over multiple frames)
    FinalItem = 1 << 1, // terminates a sequence of streaming objects
    NoItem = 1 << 2, // signals that this object should be discarded; should only be sent as a stream terminator, i.e. EndItem | EndAllItems | NoPayload
}
[Flags]
internal enum GeneralFlags
{
    None = 0,
    IsResponse = 1 << 0,
}

public enum FrameKind : byte
{
    Handshake,
    NewStream,
    Payload,
    Cancel,
    Close,
    Ping,
    [Obsolete("remove this later; should be a structured response status")]
    MethodNotFound,
}