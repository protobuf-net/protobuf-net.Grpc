using Grpc.Core;
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal;

partial class Reshape
{
    /// <summary>
    /// Performs an operation that returns data from the server as a <see cref="Stream"/>.
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Stream> ServerByteStreamingAsync<TRequest>(
        in CallContext context,
        CallInvoker invoker, IMethod method, TRequest request, string? host = null)
        where TRequest : class
    {

        context.CallOptions.CancellationToken.ThrowIfCancellationRequested();
        return Chunkify(invoker.AsyncServerStreamingCall(Assert<TRequest, BytesValue>(method), host, context.CallOptions, request), context.Prepare(), context.CancellationToken);

        async static Task<Stream> Chunkify(AsyncServerStreamingCall<BytesValue> call, MetadataContext? metadata, CancellationToken cancellationToken)
        {
            try
            {
                // wait for headers, even if not available; that means we're in a state to start spoofing the stream
                if (metadata is not null)
                {
                    await metadata.SetHeadersAsync(call.ResponseHeadersAsync);
                }
                else
                {
                    // even if we aren't capturing headers, we want to wait for them to be available,
                    //
                    await call.ResponseHeadersAsync.ConfigureAwait(false);
                }

                // so if we got this far, we think the server is happy - start spinning up infrastructure to be the stream
                Pipe pipe = new();
                _ = Task.Run(() => PushAsync(call, pipe.Writer, metadata, cancellationToken), CancellationToken.None);
                return pipe.Reader.AsStream(leaveOpen: false);
            }
            catch (RpcException fault)
            {
                metadata?.SetTrailers(fault);
                call.Dispose(); // note not using; only in case of fault!
                throw;
            }
        }

        async static Task PushAsync(AsyncServerStreamingCall<BytesValue> call, PipeWriter destination, MetadataContext? metadata, CancellationToken cancellationToken)
        {
            Exception? fault = null;
            try
            {
                var source = call.ResponseStream;
                while (await source.MoveNext(CancellationToken.None).ConfigureAwait(false)) // note that the context's cancellation is already baked in
                {
                    var chunk = source.Current;
                    var result = await destination.WriteAsync(chunk.Memory, cancellationToken).ConfigureAwait(false);
                    if (result.IsCanceled)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FallbackThrowCanceled();
                    }
                    if (result.IsCompleted)
                    {
                        // reader has shut down; stop copying (we'll tell the server by disposing the call)
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                fault = ex;
                if (fault is RpcException rpcFault)
                {
                    metadata?.SetTrailers(rpcFault);
                }
                throw;
            }
            finally
            {
                try
                {
                    // signal that no more data will be written, or at least try!
                    await destination.CompleteAsync(fault).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                try
                {
                    call.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }
    }
    static void FallbackThrowCanceled() => throw new OperationCanceledException();

    static Method<TRequest, TResponse> Assert<TRequest, TResponse>(IMethod method) => (Method<TRequest, TResponse>)method;

    // garbage for now; make this saner later!
    // see https://github.com/protocolbuffers/protobuf/blob/main/src/google/protobuf/wrappers.proto
    [ProtoContract]
    internal class BytesValue(byte[] oversized, int length, bool pooled)
    {
        public const int MaxLength = 0x1FFFFF; // 21 bits of length prefix; 2,097,151 bytes
                                               // (note we will still *read* buffers larger than that, because of non-"us" endpoints, but we'll never send them)

        private bool _pooled = pooled;
        private byte[] _oversized = oversized;
        private int _length = length;

        private BytesValue() : this([], 0, false) { } // for deserialization 

        [ProtoMember(1)]
        public byte[] RightSized // for deserializer only
        {
            get
            {
                if (_oversized.Length != _length)
                {
                    Array.Resize(ref _oversized, _length);
                    _pooled = false;
                }
                return _oversized;
            }
            set
            {
                value ??= [];
                _length = value.Length;
                _oversized = value;
            }
        }

        public void Recycle()
        {
            bool pooled = _pooled;
            _pooled = false;
            var tmp = _oversized;
            _length = 0;
            _oversized = [];

            if (pooled)
            {
                ArrayPool<byte>.Shared.Return(tmp);
            }
        }

        public bool IsEmpty => _length == 0;
        public int Length => _length;

        public ArraySegment<byte> ArraySegment => new(_oversized, 0, _length);
        public ReadOnlySpan<byte> Span => new(_oversized, 0, _length);
        public ReadOnlyMemory<byte> Memory => new(_oversized, 0, _length);

        public static Marshaller<BytesValue> Marshaller { get; } = new(Serialize, Deserialize);

        private static BytesValue Deserialize(DeserializationContext context)
        {
            var payload = context.PayloadAsReadOnlySequence();
            var totalLen = payload.Length;
            BytesValue? result;

            if (payload.First.Length >= 4)
            {
                // enough bytes in the first segment
                result = TryFastParse(payload.First.Span, payload);
            }
            else
            {
                // copy up-to 4 bytes into a buffer, handling multi-segment concerns
                Span<byte> buffer = stackalloc byte[4];
                payload.Slice(0, (int)Math.Min(totalLen, 4)).CopyTo(buffer);
                result = TryFastParse(buffer, payload);
            }

            return result ?? SlowParse(payload);
        }

        static BytesValue SlowParse(in ReadOnlySequence<byte> payload)
        {
            IProtoInput<Stream> model = RuntimeTypeModel.Default;
            var len = payload.Length;
            // use protobuf-net v3 API if available
            if (model is IProtoInput<ReadOnlySequence<byte>> v3)
            {
                return v3.Deserialize<BytesValue>(payload);
            }

            // use protobuf-net v2 API
            MemoryStream ms;
            if (payload.IsSingleSegment && MemoryMarshal.TryGetArray(payload.First, out var segment))
            {
                ms = new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false, publiclyVisible: true);
            }
            else
            {
                ms = new MemoryStream();
                ms.SetLength(len);
                if (ms.TryGetBuffer(out var buffer) && buffer.Count >= len)
                {
                    payload.CopyTo(buffer.AsSpan());
                }
                else
                {
#if !(NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER)
                    byte[] leased = [];
#endif
                    foreach (var chunk in payload)
                    {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                            ms.Write(chunk.Span);
#else
                        if (MemoryMarshal.TryGetArray(chunk, out segment))
                        {
                            ms.Write(segment.Array!, segment.Offset, segment.Count);
                        }
                        else
                        {
                            if (leased.Length < segment.Count)
                            {
                                ArrayPool<byte>.Shared.Return(leased);
                                leased = ArrayPool<byte>.Shared.Rent(segment.Count);
                            }
                            segment.AsSpan().CopyTo(leased);
                            ms.Write(leased, 0, segment.Count);
                        }
#endif
                    }
#if !(NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER)
                    if (leased.Length != 0)
                    {
                        ArrayPool<byte>.Shared.Return(leased);
                    }
#endif
                    Debug.Assert(ms.Position == len, "should have written all bytes");
                    ms.Position = 0;
                }
            }
            Debug.Assert(ms.Position == 0 && ms.Length == len, "full payload should be ready to read");
            return model.Deserialize<BytesValue>(ms);
        }



        static BytesValue? TryFastParse(ReadOnlySpan<byte> start, in ReadOnlySequence<byte> payload)
        {
            Debug.Assert(start.Length >= 4, "optimized for at least 4 bytes available");

            ulong raw = BinaryPrimitives.ReadUInt32LittleEndian(start);
            int byteLen, headerLen;
            switch (raw & 0x808080FF)
            {
                // one-byte length, with anything after (0A00*, backwards)
                case 0x0000000A:
                case 0x8000000A:
                case 0x0080000A:
                case 0x8080000A:
                    headerLen = 2;
                    byteLen = start[1];
                    break;
                // two-byte length, with anything after (0A8000*, backwards)
                case 0x0000800A:
                case 0x8000800A:
                    headerLen = 3;
                    byteLen = (start[1] & 0x7F) | (start[2] << 7);
                    break;
                // three-byte length (0A808000, backwards)
                case 0x0A808000:
                case 0x0080800A:
                    headerLen = 4;
                    byteLen = (start[1] & 0x7F) | ((start[2] & 0x7F) << 7) | (start[3] << 14);
                    break;
                default:
                    return null; // not useful
            }
            if (headerLen + byteLen != payload.Length)
            {
                return null; // not the entire payload (other fields?)
            }

#if DEBUG
            // double-check our math using the less efficient library functions
            var arr = start.Slice(0, 4).ToArray();
            Debug.Assert(start[0] == 0x0A, "field 1, string");
            Debug.Assert(Serializer.TryReadLengthPrefix(arr, 1, 3, PrefixStyle.None, out int checkLen)
                && checkLen == byteLen, $"length mismatch; {byteLen} vs {checkLen}");
#endif

            var leased = ArrayPool<byte>.Shared.Rent(byteLen);
            payload.Slice(headerLen).CopyTo(leased);
            return new(leased, byteLen, pooled: true);
        }

        private static void Serialize(BytesValue value, global::Grpc.Core.SerializationContext context)
        {
            int byteLen = value.Length, headerLen;
            if (byteLen <= 0x7F) // 7 bit
            {
                headerLen = 2;
            }
            else if (byteLen <= 0x3FFF) // 14 bit
            {
                headerLen = 3;
            }
            else if (byteLen <= 0x1FFFFF) // 21 bit
            {
                headerLen = 4;
            }
            else
            {
                throw new NotSupportedException("We don't expect to write messages this large!");
            }
            int totalLength = headerLen + byteLen;
            context.SetPayloadLength(totalLength);
            var writer = context.GetBufferWriter();
            var buffer = writer.GetSpan(totalLength);
            // we'll assume that we get space for at least the header bytes, but we can *hope* for the entire thing

            buffer[0] = 0x0A; // field 1, string
            switch (headerLen)
            {
                case 2:
                    buffer[1] = (byte)byteLen;
                    break;
                case 3:
                    buffer[1] = (byte)(byteLen | 0x80);
                    buffer[2] = (byte)(byteLen >> 7);
                    break;
                case 4:
                    buffer[1] = (byte)(byteLen | 0x80);
                    buffer[2] = (byte)((byteLen >> 7) | 0x80);
                    buffer[3] = (byte)(byteLen >> 14);
                    break;
            }
            if (buffer.Length >= totalLength)
            {
                // write everything in one go
                value.Span.CopyTo(buffer.Slice(headerLen));
                writer.Advance(totalLength);
            }
            else
            {
                // commit the header, then write the body
                writer.Advance(headerLen);
                writer.Write(value.Span);
            }
        }
    }
}
