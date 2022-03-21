using Grpc.Core;
using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Text;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static class MetadataEncoder
{
    internal static void WriteHeader(IBufferWriter<byte> writer, bool isClient, string fullName, string? host, in CallOptions options)
    {
        if (string.IsNullOrEmpty(fullName)) ThrowMissingMethod();
        if (!string.IsNullOrEmpty(host)) ThrowNotSupported();

        // simple for now; just the method name; add full options/metadata later
        var bytes = Encoding.UTF8.GetByteCount(fullName);
        if (bytes > FrameHeader.MaxPayloadLength) ThrowMethodTooLarge(bytes);
        writer.Advance(Encoding.UTF8.GetBytes(fullName, writer.GetSpan(bytes)));

        static void ThrowMissingMethod() => throw new ArgumentOutOfRangeException(nameof(fullName), "No method name was specified");
        static void ThrowNotSupported() => throw new ArgumentOutOfRangeException(nameof(host), "Non-empty hosts are not currently supported");
        static void ThrowMethodTooLarge(int length) => throw new InvalidOperationException($"The method name is too large at {length} bytes");
    }
}
