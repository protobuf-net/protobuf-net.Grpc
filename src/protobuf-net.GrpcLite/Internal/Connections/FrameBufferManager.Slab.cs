﻿//using ProtoBuf.Grpc.Lite.Connections;
//using System.Buffers;
//using System.Buffers.Binary;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;

//namespace ProtoBuf.Grpc.Lite.Internal.Connections;

//internal partial class FrameBufferManager
//{
//    internal sealed class Slab : MemoryManager<byte>
//    {
//        private readonly FrameBufferManager _owner;
//        public int Count { get; set; }
//        public Slab? Tail { get; set; }


//        private readonly byte[] _buffer;

//        internal int CurrentHeaderOffset => _currentHeaderOffset;
//        private int _currentHeaderOffset, _refCount = 1, _pinCount;
//        private Memory<byte> _activePayloadBuffer;
//        public Memory<byte> ActiveBuffer => _activePayloadBuffer;

//        public Memory<byte> FrameBuffer(int start = 0)
//        {
//            if (start >= FrameHeader.Size) return _activePayloadBuffer;
//            return new Memory<byte>(_buffer, _currentHeaderOffset + start,
//                _buffer.Length - (_currentHeaderOffset + start));
//        }


//        internal string DebugSummarize(int count) => DebugSummarize(ActiveBuffer.Slice(0, count));
//        internal string DebugSummarize(ReadOnlyMemory<byte> buffer)
//        {
//#if DEBUG
//            try
//            {
//                if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
//                {
//                    return $"{segment.Count} bytes, [{segment.Offset}, {segment.Offset + segment.Count})";
//                }
//                else
//                {
//                    return $"{buffer.Length} bytes";
//                }
//            }
//            catch (Exception ex)
//            {
//                return ex.Message;
//            }
//#else
//            return "";
//#endif
//        }

//        internal ushort DeclaredPayloadLength() 
//        {
//            // only to be used when parsing buffers, i.e. something has populated this
//            return BinaryPrimitives.ReadUInt16LittleEndian(
//                new ReadOnlySpan<byte>(_buffer, _currentHeaderOffset + FrameHeader.PayloadLengthOffset, sizeof(ushort)));
//        }

//        internal string DebugGetHex(int count)
//        {
//#if DEBUG
//            try
//            {
//                var buffer = ActiveBuffer.Slice(0, count);
//                if (!MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
//                {
//                    segment = buffer.ToArray();
//                }
//                return BitConverter.ToString(segment.Array!, segment.Offset, segment.Count);
//            }
//            catch (Exception ex)
//            {
//                return ex.Message;
//            }
//#else
//            return "";
//#endif
//        }

//        public void Advance(int count) => _activePayloadBuffer = _activePayloadBuffer.Slice(start: count);

//        private GCHandle _pinHandle;
//        public void AddReference()
//        {
//            lock (this)
//            {
//                AddReferenceLocked();
//            }

//        }
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private void AddReferenceLocked()
//        {
//            if (_refCount == int.MaxValue) Throw();
//            _refCount++;
//            static void Throw() => throw new InvalidOperationException("Overflow adding buffer reference");
//        }
//        public void RemoveReference()
//        {
//            lock (this)
//            {
//                switch (_refCount)
//                {
//                    case 0:
//                        Throw();
//                        break;
//                    case 1:
//                        if (!_owner.TryReturn(this))
//                        {
//                            ArrayPool<byte>.Shared.Return(_buffer);
//                        }
//                        break;
//                }
//                _refCount--;
//            }
//            static void Throw() => throw new InvalidOperationException("Buffer released too many times!");
//        }

//        internal Slab(FrameBufferManager owner, byte[] buffer)
//        {
//            _owner = owner;
//            _buffer = buffer;
//        }
//        public void Return()
//        {
//            _activePayloadBuffer = default;
//            lock (this)
//            {
//                if (!_owner.TryReturn(this)) _refCount--; // not accepted
//            }
//        }

//        public Frame CreateFrameAndInvalidate(FrameHeader header, bool updateHeaderLength)
//        {
//            // compute the length, and overwrite the header (including the updated length)
//            ref byte headerStart = ref _buffer[_currentHeaderOffset];
//            var delta = Unsafe.ByteOffset(ref headerStart, ref ActiveBuffer.Span[0]).ToInt64();
//            var headerAndPayloadLength = checked((ushort)delta);
//            if (headerAndPayloadLength < FrameHeader.Size) ThrowTooSmallForHeader();

//            header.UnsafeWrite(ref headerStart);
//            var actualPayloadLength = (ushort)(headerAndPayloadLength - FrameHeader.Size);
//            if (updateHeaderLength)
//            {
//                BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(_buffer, _currentHeaderOffset + FrameHeader.PayloadLengthOffset, 2), actualPayloadLength);
//            }

//            var frame = new Frame(Memory.Slice(_currentHeaderOffset, headerAndPayloadLength), trusted: !updateHeaderLength);
//            _currentHeaderOffset += headerAndPayloadLength;
//            _activePayloadBuffer = default;
//            AddReference(); // for the new buffer
//            return frame;
//        }

//        internal void Prepare()
//        {
//            var headerOffset = _currentHeaderOffset;
//            var availableBytes = _buffer.Length - headerOffset;
//            if (availableBytes < FrameHeader.Size) ThrowTooSmallForHeader();
//            _activePayloadBuffer = Memory.Slice(headerOffset + FrameHeader.Size, Math.Min(availableBytes - FrameHeader.Size, FrameHeader.MaxPayloadLength));
//        }

//        static void ThrowTooSmallForHeader() => throw new InvalidOperationException("The available buffer is not large enough for a frame header");

//        protected override bool TryGetArray(out ArraySegment<byte> segment)
//        {
//            segment = new ArraySegment<byte>(_buffer, 0, _buffer.Length);
//            return true;
//        }

//        protected override void Dispose(bool disposing) { } // use Release/RemoveReference

//        public override Span<byte> GetSpan() => _buffer;

//        public int UnusedBytes => _buffer.Length - _currentHeaderOffset;

//        public override MemoryHandle Pin(int elementIndex = 0)
//        {
//            lock (this)
//            {
//                switch (_pinCount)
//                {
//                    case 0:
//                        _pinHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
//                        break;
//                    case int.MaxValue:
//                        return Throw();
//                        static MemoryHandle Throw() => throw new InvalidOperationException("Overflow pinning buffer reference");
//                }
//                _pinCount++;
//                unsafe
//                {
//                    return new MemoryHandle(Unsafe.AsPointer(ref _buffer[elementIndex]), default, this);
//                }
//            }
//        }

//        public override void Unpin()
//        {
//            lock (this)
//            {
//                switch (_pinCount)
//                {
//                    case 0:
//                        Throw();
//                        break;
//                        static void Throw() => throw new InvalidOperationException("Underflow unpinning buffer reference");
//                    case 1:
//                        _pinHandle.Free();
//                        _pinHandle = default;
//                        break;
//                }
//                _pinCount--;
//            }
//        }
//    }
//}

