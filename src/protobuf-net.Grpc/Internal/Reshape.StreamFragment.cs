using Grpc.Core;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Internal
{
    public static partial class Reshape
    {
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public sealed class StreamFragment : IDisposable
        {
            private readonly byte[] _buffer;
            private readonly int _length;
            private bool _disposed;
#if DEBUG
            static int s_nextId;
            private readonly int _id = System.Threading.Interlocked.Increment(ref s_nextId);
            public override string ToString() => $"{nameof(StreamFragment)}#{_id}, {_length} bytes ({(_disposed ? "dead" : "alive")})";
#endif
            internal static StreamFragment Empty { get; } = new StreamFragment(Array.Empty<byte>(), 0);
            internal static Marshaller<StreamFragment> Marshaller { get; } = new Marshaller<StreamFragment>(Write, Parse);
            internal int Length => _length;

            internal bool IsDisposed => _disposed;

            private static StreamFragment Parse(global::Grpc.Core.DeserializationContext context)
            {
                // TODO: protobuf header
                var ros = context.PayloadAsReadOnlySequence();
                var length = checked((int)ros.Length);
                if (length == 0) return Empty;

                var arr = ArrayPool<byte>.Shared.Rent(length);
                ros.CopyTo(arr);
                var obj = new StreamFragment(arr, length);
                Debug.WriteLine($"Deserialized {obj}");
                return obj;
            }
            private static void Write(StreamFragment value, global::Grpc.Core.SerializationContext context)
            {
                // TODO: protobuf header
                value.ThrowIfDisposed();
                context.SetPayloadLength(value._length);
                context.GetBufferWriter().Write(new ReadOnlySpan<byte>(value._buffer, 0, value._length));
                context.Complete();
                Debug.WriteLine($"Serialized {value}");
                value.Dispose();
            }

            internal static StreamFragment Create(byte[] buffer, int length)
            {
                Debug.Assert(buffer is not null, "null buffer");
                Debug.Assert(buffer.Length >= length, "invalid buffer length");
                if (length <= 0)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    return Empty;
                }
                var obj = new StreamFragment(buffer, length);
                Debug.WriteLine($"Created {obj}");
                return obj;
            }
            private StreamFragment(byte[] buffer, int length)
            {
                _buffer = buffer;
                _length = length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int Take(ref int offset, int count)
            {
                ThrowIfDisposed();
                Debug.Assert(count >= 0, "invalid count");
                Debug.Assert(offset >= 0 && offset <= count, "invalid offset");
                int take = Math.Min(count, _length - offset);
                Debug.WriteLine($"Taking {take} from {this}");
                return take;
            }
            internal ArraySegment<byte> TakeSegment(ref int offset, int count)
            {
                var take = Take(ref offset, count);
                var result = new ArraySegment<byte>(_buffer, offset, take);
                offset += take;
                return result;
            }
            internal ReadOnlySpan<byte> TakeSpan(ref int offset, int count)
            {
                var take = Take(ref offset, count);
                var result = new ReadOnlySpan<byte>(_buffer, offset, take);
                offset += take;
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ThrowIfDisposed()
            {
                if (_disposed && !ReferenceEquals(this, Empty)) Throw();
                static void Throw() => throw new ObjectDisposedException(nameof(StreamFragment));
            }

            public void Dispose()
            {
                if (_disposed || ReferenceEquals(this, Empty)) return;
                Debug.WriteLine($"Disposing {this}");
                _disposed = true;
                ArrayPool<byte>.Shared.Return(_buffer);
            }
        }
    }
}
