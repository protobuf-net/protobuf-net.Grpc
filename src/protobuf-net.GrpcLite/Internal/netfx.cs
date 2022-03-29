using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal
{
    static partial class Utilities
    {
#if NET472
        public static ValueTask SafeDisposeAsync(this Stream stream)
        {
            stream.SafeDispose();
            return default;
        }
        public static Task<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            static void Throw() => throw new NotSupportedException("Array-based buffer required");
            if (!MemoryMarshal.TryGetArray<byte>(buffer, out var segment)) Throw();
            return stream.ReadAsync(segment.Array, segment.Offset, segment.Count, cancellationToken);
        }
        public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            static void Throw() => throw new NotSupportedException("Array-based buffer required");
            if (!MemoryMarshal.TryGetArray<byte>(buffer, out var segment)) Throw();
            return new ValueTask(stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken));
        }

        public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty) return "";
            fixed (byte* ptr = value)
            {
                return encoding.GetString(ptr, value.Length);
            }
        }
        public static unsafe int GetCharCount(this Encoding encoding, ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty) return 0;
            fixed (byte* ptr = value)
            {
                return encoding.GetCharCount(ptr, value.Length);
            }
        }
        public static unsafe int GetByteCount(this Encoding encoding, ReadOnlySpan<char> value)
        {
            if (value.IsEmpty) return 0;
            fixed (char* ptr = value)
            {
                return encoding.GetByteCount(ptr, value.Length);
            }
        }
        public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            if (chars.IsEmpty) return 0;
            fixed (char* cPtr = chars)
            fixed (byte* bPtr = bytes)
            {
                return encoding.GetBytes(cPtr, chars.Length, bPtr, bytes.Length);
            }
        }
        public static unsafe int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, ReadOnlySpan<char> chars)
        {
            if (bytes.IsEmpty) return 0;
            fixed (char* cPtr = chars)
            fixed (byte* bPtr = bytes)
            {
                return encoding.GetChars(bPtr, bytes.Length, cPtr, chars.Length);
            }
        }
        public static unsafe void Convert(this Encoder encoder, ReadOnlySpan<char> chars, Span<byte> bytes, bool flush, out int charsUsed, out int bytesUsed, out bool completed)
        {
            fixed (char* cPtr = chars)
            fixed (byte* bPtr = bytes)
            {
                encoder.Convert(cPtr, chars.Length, bPtr, bytes.Length, flush, out charsUsed, out bytesUsed, out completed);
            }
        }

        public static bool TryPeek<T>(this Queue<T> queue, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? value)
        {
            var iter = queue.GetEnumerator();
            if (iter.MoveNext())
            {
                value = iter.Current!;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySequence<T> AsReadOnlySequence<T>(this ReadOnlyMemory<T> memory)
        {
            // netfx has a nasty bug if you use `new ROS(memory)` with a custom manager with non-zero start; the bug
            // doesn't apply to sequence segments, though - so if we have a custom manager that doesn't support arrays: use that
            if (memory.IsEmpty) return default;
            if (MemoryMarshal.TryGetArray(memory, out var segment))
                return new ReadOnlySequence<T>(segment.Array, segment.Offset, segment.Count);
            return ViaManager(memory);

            static ReadOnlySequence<T> ViaManager(ReadOnlyMemory<T> memory)
            {
                if (MemoryMarshal.TryGetMemoryManager<T, MemoryManager<T>>(memory, out var manager, out var start, out var length)
                    && start != 0)
                {
                    var seqSegment = manager is RefCountedMemoryManager<T> basic ? basic.SharedSegment : new IsolatedSequenceSegment<T>(manager.Memory);
                    return new ReadOnlySequence<T>(seqSegment, start, seqSegment, start + length);
                }
                return new ReadOnlySequence<T>(memory);
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySequence<T> AsReadOnlySequence<T>(this ReadOnlyMemory<T> memory) => new ReadOnlySequence<T>(memory);
#endif
    }
}
#if NET472
namespace ProtoBuf.Grpc.Lite.Internal
{
    internal sealed class IsolatedSequenceSegment<T> : ReadOnlySequenceSegment<T>
    {
        public IsolatedSequenceSegment(ReadOnlyMemory<T> memory)
        {
            Next = null;
            Memory = memory;
            RunningIndex = 0;
        }
    }
}
namespace ProtoBuf.Grpc.Lite.Connections
{
    partial class RefCountedMemoryManager<T>
    {
        private ReadOnlySequenceSegment<T>? _singleSegment;
        internal ReadOnlySequenceSegment<T> SharedSegment => _singleSegment ??= new IsolatedSequenceSegment<T>(Memory);
    }
}
#endif