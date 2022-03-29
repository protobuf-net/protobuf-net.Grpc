using Grpc.Core;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static class MetadataEncoder
{
    internal static void WriteHeader(PayloadFrameSerializationContext writer, bool isClient, string fullName, string? host, in CallOptions options)
    {
        if (string.IsNullOrEmpty(fullName)) ThrowMissingMethod();

        WriteClob(writer, KnownField.Route, fullName.AsSpan());
        AssertSingleFrame(writer, KnownField.Route);

        if (!string.IsNullOrWhiteSpace(host)) WriteClob(writer, KnownField.Host, host.AsSpan());
        var headers = options.Headers;
        if (headers is not null && headers.Count != 0) WriteMetadata(writer, headers);

        static void ThrowMissingMethod() => throw new ArgumentOutOfRangeException(nameof(fullName), "No method name was specified");
    }

    static void AssertSingleFrame(PayloadFrameSerializationContext writer, KnownField field)
    {
        if (writer.PendingFrameCount > 0) ThrowTooLong(field);
        // we can remove this; we'd just need to tweak GetRouteBuffer and GetStatus
        static void ThrowTooLong(KnownField field) => throw new ArgumentOutOfRangeException(nameof(field), $"The {field} is too long; a single frame is expected");
    }

    internal static void WriteStatus(PayloadFrameSerializationContext writer, Status status)
        => WriteStatus(writer, status.StatusCode, status.Detail.AsSpan());

    internal static void WriteStatus(PayloadFrameSerializationContext writer, StatusCode statusCode, ReadOnlySpan<char> detail)
    {
        if (statusCode != StatusCode.OK) WriteInt32(writer, KnownField.StatusCode, (int)statusCode);
        if (!detail.IsEmpty)
        {
            WriteClob(writer, KnownField.StatusDetail, detail);
            AssertSingleFrame(writer, KnownField.StatusDetail);
        }
    }

    private static void WriteInt32(IBufferWriter<byte> writer, KnownField field, int value)
    {
        // note: values are always signed
        var span = writer.GetSpan(5);
        if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
        {
            span[0] = (byte)((int)field | (int)EncodingType.OneByte);
            span[1] = (byte)(sbyte)value;
            writer.Advance(2);
        }
        else if (value >= short.MinValue && value <= short.MaxValue)
        {
            span[0] = (byte)((int)field | (int)EncodingType.TwoBytes);
            BinaryPrimitives.WriteInt16LittleEndian(span.Slice(1), (short)value);
            writer.Advance(3);
        }
        else
        {
            span[0] = (byte)((int)field | (int)EncodingType.FourBytes);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), value);
            writer.Advance(5);
        }
    }

    private static void WriteBlob(IBufferWriter<byte> writer, KnownField field, ReadOnlySpan<byte> value)
    {
        var length = value.Length;
        var span = writer.GetSpan(Math.Min(length + 5, 256)); // enough for the header, in all cases
        int written;
        switch (length)
        {
            case 0:
                span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixOneByte);
                span[1] = 0;
                writer.Advance(2);
                return; // all done
            case 1:
                span[0] = (byte)((int)field | (int)EncodingType.OneByte);
                written = 1;
                break;
            case 2:
                span[0] = (byte)((int)field | (int)EncodingType.TwoBytes);
                written = 1;
                break;
            case 4:
                span[0] = (byte)((int)field | (int)EncodingType.FourBytes);
                written = 1;
                break;
            case 8:
                span[0] = (byte)((int)field | (int)EncodingType.EightBytes);
                written = 1;
                break;
            default: // note that length prefix is always unsigned
                if (length <= byte.MaxValue)
                {
                    span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixOneByte);
                    span[1] = (byte)length;
                    written = 2;
                }
                else if (length <= ushort.MaxValue)
                {
                    span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixTwoBytes);
                    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(1), (ushort)length);
                    written = 3;
                }
                else
                {
                    span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixFourBytes);
                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1), (uint)length);
                    written = 5;
                }
                break;
        }
        if (span.Length >= length + written)
        {
            value.CopyTo(span.Slice(written));
            writer.Advance(written + length);
        }
        else
        {
            writer.Advance(written);
            writer.Write(value);
        }
    }
    private static void WriteClob(IBufferWriter<byte> writer, KnownField field, ReadOnlySpan<char> value)
    {
        var encodedLength = Encoding.UTF8.GetByteCount(value);
        var span = writer.GetSpan(Math.Min(encodedLength + 5, 256)); // enough for the header, in all cases
        int written;
        switch (encodedLength)
        {
            case 0:
                span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixOneByte);
                span[1] = 0;
                writer.Advance(2);
                return; // all done
            case 1:
                span[0] = (byte)((int)field | (int)EncodingType.OneByte);
                written = 1;
                break;
            case 2:
                span[0] = (byte)((int)field | (int)EncodingType.TwoBytes);
                written = 1;
                break;
            case 4:
                span[0] = (byte)((int)field | (int)EncodingType.FourBytes);
                written = 1;
                break;
            case 8:
                span[0] = (byte)((int)field | (int)EncodingType.EightBytes);
                written = 1;
                break;
            default: // note that length prefix is always unsigned
                if (encodedLength <= byte.MaxValue)
                {
                    span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixOneByte);
                    span[1] = (byte)encodedLength;
                    written = 2;
                }
                else if (encodedLength <= ushort.MaxValue)
                {
                    span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixTwoBytes);
                    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(1), (ushort)encodedLength);
                    written = 3;
                }
                else
                {
                    span[0] = (byte)((int)field | (int)EncodingType.LengthPrefixFourBytes);
                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1), (uint)encodedLength);
                    written = 5;
                }
                break;
        }
        if (span.Length >= encodedLength + written)
        {
            var actualEncodedLength = Encoding.UTF8.GetBytes(value, span.Slice(written));
            Debug.Assert(actualEncodedLength == encodedLength, "encoding failure");
            writer.Advance(written + actualEncodedLength);
        }
        else
        {
            writer.Advance(written);
            WriteWithEncoder(writer, value, encodedLength);
        }
        static void WriteWithEncoder(IBufferWriter<byte> writer, ReadOnlySpan<char> value, int bytesRemaining)
        {
            var enc = Encoding.UTF8.GetEncoder();
            bool completed = true;
            while (!value.IsEmpty)
            {
                var span = writer.GetSpan(Math.Min(bytesRemaining, 1024));
                enc.Convert(value, span, false, out int charsUsed, out int bytesUsed, out completed);
                bytesRemaining -= bytesUsed;
                writer.Advance(bytesUsed);
                value = value.Slice(charsUsed);
            }
            if (!completed)
            {
                var span = writer.GetSpan(5);
                enc.Convert(default, span, true, out int charsUsed, out int bytesUsed, out completed);
                Debug.Assert(completed && charsUsed == 0, "final write looks odd");
                bytesRemaining -= bytesUsed;
                writer.Advance(bytesUsed);
            }
            Debug.Assert(bytesRemaining == 0, "not all bytes written");
        }
    }

    internal static void WriteMetadata(IBufferWriter<byte> writer, Metadata metadata)
    {
        foreach (var pair in metadata)
        {
            WriteClob(writer, KnownField.HeaderName, pair.Key.AsSpan());
            if (pair.IsBinary)
            {
                WriteBlob(writer, KnownField.HeaderBinaryValue, pair.ValueBytes);
            }
            else
            {
                WriteClob(writer, KnownField.HeaderTextValue, pair.Value.AsSpan());
            }
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, int length)
    {
        return length switch
        {
            0 => 0,
            1 => span[0],
            2 => BinaryPrimitives.ReadInt16LittleEndian(span),
            4 => BinaryPrimitives.ReadInt32LittleEndian(span),
            8 => checked((int)BinaryPrimitives.ReadInt64LittleEndian(span)),
            _ => Throw(length),
        };

        static int Throw(int length) => throw new ArgumentOutOfRangeException(nameof(length), $"Unexpected Int32 length: {length}");
    }

    internal static Metadata GetMetadata(ReadOnlySequence<byte> value, IConnection connection)
    {
        if (!value.IsSingleSegment) ThrowMultiSegment();

        Metadata? result = null;
        var span = value.First.Span;
        string? lastHeaderName = null;
        while (!span.IsEmpty)
        {
            var len = GetPayloadLength(ref span, out var field);
            switch (field)
            {
                case KnownField.HeaderName:
                    if (lastHeaderName is not null)
                    {   // check for a trailing name-only header
                        (result ??= new Metadata()).Add(lastHeaderName, "");
                    }
                    lastHeaderName = ReadString(span, len);
                    break;
                case KnownField.HeaderTextValue:
                    if (lastHeaderName is null) ThrowNoName();
                    string s;
                    if (lastHeaderName == "user-agent")
                    {
                        connection.LastKnownUserAgent = s = TestStringForLastKnownValue(span.Slice(0, len), connection.LastKnownUserAgent);
                    }
                    else
                    {
                        s = ReadString(span, len);
                    }
                    (result ??= new Metadata()).Add(lastHeaderName!, s);
                    lastHeaderName = null;
                    break;
                case KnownField.HeaderBinaryValue:
                    if (lastHeaderName is null) ThrowNoName();
                    (result ??= new Metadata()).Add(lastHeaderName!, ReadByteArray(span, len));
                    lastHeaderName = null;
                    break;
            }
            span = span.Slice(len);
        }
        if (lastHeaderName is not null)
        {   // check for a trailing name-only header
            (result ??= new Metadata()).Add(lastHeaderName, "");
        }

        return result ?? Metadata.Empty;


        static void ThrowNoName() => throw new NotSupportedException("Missing header name");
    }

    private static string TestStringForLastKnownValue(ReadOnlySpan<byte> value, string? lastKnown)
    {
        if (value.IsEmpty) return "";
        if (lastKnown is not null && Encoding.UTF8.GetByteCount(lastKnown) == value.Length)
        {
            if (value.Length < 128)
            {
                if (IsMatch(stackalloc byte[value.Length], value, lastKnown)) return lastKnown;
            }
            else
            {
                var arr = ArrayPool<byte>.Shared.Rent(value.Length);
                bool eq = IsMatch(new Span<byte>(arr, 0, value.Length), value, lastKnown);
                ArrayPool<byte>.Shared.Return(arr);
                if (eq) return lastKnown;
            }
        }
        return Encoding.UTF8.GetString(value);
        static bool IsMatch(Span<byte> buffer, ReadOnlySpan<byte> value, string? lastKnownValue)
        {
            Debug.Assert(value.Length == buffer.Length);
            int actual = Encoding.UTF8.GetBytes(lastKnownValue.AsSpan(), buffer);
            Debug.Assert(actual == buffer.Length);
            return buffer.SequenceEqual(value);
        }
    }

    static void ThrowMultiSegment() => throw new NotSupportedException("Single-frame headers/trailers expected");

    internal static Status GetStatus(ReadOnlySequence<byte> value)
    {
        if (!value.IsSingleSegment) ThrowMultiSegment();
        var span = value.First.Span;
        StatusCode statusCode = StatusCode.OK;
        string detail = "";
        Logging.DebugWriteLine($"Parsing status from: " + value.ToHex());
        while (!span.IsEmpty)
        {
            var len = GetPayloadLength(ref span, out var field);
            switch (field)
            {
                case KnownField.StatusCode:
                    statusCode = (StatusCode)ReadInt32(span, len);
                    break;
                case KnownField.StatusDetail:
                    detail = ReadString(span, len);
                    break;
            }
            span = span.Slice(len);
        }
        return new Status(statusCode, detail);
    }

    private static string ReadString(ReadOnlySpan<byte> span, int length)
    {
        Debug.Assert(span.Length >= length, "undersized span");
        if (length == 0) return "";
        if (length == 10 && span.SequenceEqual(UserAgent)) return "user-agent"; // well-known header
        return Encoding.UTF8.GetString(span.Slice(0, length));
    }

    private static ReadOnlySpan<byte> UserAgent => new byte[10] { 117, 115, 101, 114, 045, 097, 103, 101, 110, 116 }; // ASCII

    private static byte[] ReadByteArray(ReadOnlySpan<byte> span, int length)
        => length == 0 ? Utilities.EmptyBuffer : span.Slice(0, length).ToArray();

    internal static ArraySegment<char> GetRouteBuffer(ReadOnlyMemory<byte> value)
    {
        var span = value.Span;
        while (!span.IsEmpty)
        {
            var len = GetPayloadLength(ref span, out var field);
            if (field == KnownField.Route)
            {
                if (len == 0) return new ArraySegment<char>(Array.Empty<char>());
                span = span.Slice(0, len);

                var charCount = Encoding.UTF8.GetCharCount(span);
                var chars = ArrayPool<char>.Shared.Rent(charCount);
                Encoding.UTF8.GetChars(span, chars);
                return new ArraySegment<char>(chars, 0, charCount);
            }
            else
            {
                span = span.Slice(len);
            }
        }
        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static KnownField GetKnownField(byte value, out EncodingType type)
    {
        type = (EncodingType)value & EncodingType.Mask;
        return (KnownField)value & KnownField.Mask;
    }
    static int GetPayloadLength(ref ReadOnlySpan<byte> span, out KnownField field)
    {
        field = GetKnownField(span[0], out var type);
        span = span.Slice(1);
        switch (type)
        {
            case EncodingType.OneByte:
                return 1;
            case EncodingType.TwoBytes:
                return 2;
            case EncodingType.FourBytes:
                return 4;
            case EncodingType.EightBytes:
                return 8;
            case EncodingType.LengthPrefixOneByte:
                int len = span[0];
                span = span.Slice(1);
                return len;
            case EncodingType.LengthPrefixTwoBytes:
                len = BinaryPrimitives.ReadUInt16LittleEndian(span);
                span = span.Slice(2);
                return len;
            case EncodingType.LengthPrefixFourBytes:
                len = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(span));
                span = span.Slice(4);
                return len;
            case EncodingType.LengthPrefixEightBytes:
                len = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(span));
                span = span.Slice(8);
                return len;
            default:
                return Throw(type);
        }
        static int Throw(EncodingType type) => throw new ArgumentOutOfRangeException(nameof(type), $"Unexpected encoding type: {type}");
    }

    enum KnownField
    {
        Route = 0 << 3,
        Host = 1 << 3,
        StatusCode = 16 << 3,
        StatusDetail = 17 << 3,


        HeaderName = 29 << 3,
        HeaderTextValue = 30 << 3,
        HeaderBinaryValue = 31 << 3,
        Mask = 0b11111000,
    }
    enum EncodingType // low 3 bits
    {
        OneByte = 0,
        TwoBytes = 1,
        FourBytes = 2,
        EightBytes = 3,
        LengthPrefixOneByte = 4,
        LengthPrefixTwoBytes = 5,
        LengthPrefixFourBytes = 6,
        LengthPrefixEightBytes = 7,
        Mask = 0b00000111,
    }
}
