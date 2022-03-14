using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite.Internal;

internal class FrameBufferManager
{
    private readonly int _slabSize;
    private Slab? _head;
    public FrameBufferManager(int slabSize) => _slabSize = Math.Max(slabSize, ushort.MaxValue); // need to allow full frames
    public static FrameBufferManager Shared { get; } = new FrameBufferManager(256 * 1024);

    public int Slabs => Volatile.Read(ref _head)?.Count ?? 0;
    internal bool TryReturn(Slab? value)
    {
        const int MAX_BUFFERS = 128, MIN_BYTES_FOR_REUSE = 256;
        if (value is not null)
        {
            if (value.UnusedBytes < MIN_BYTES_FOR_REUSE) return false;
            Slab? tail = Volatile.Read(ref _head);
            do
            {
                var tailCount = (tail?.Count ?? 0);
                if (tailCount >= MAX_BUFFERS) return false;
                value.Tail = tail;
                value.Count = 1 + tailCount;
            }
            while (!ReferenceEquals(tail,
                tail = Interlocked.CompareExchange(ref _head, value, tail)));
            return true;
        }
        return false;
    }
    internal Slab Rent(int minPayloadSize)
    {
        var minimumSize = minPayloadSize + FrameHeader.Size;
        if (minimumSize > _slabSize) Throw(minimumSize, _slabSize);
        Slab? head = Volatile.Read(ref _head);
        do
        {
            if (head is null)
            {
                head = new Slab(this, ArrayPool<byte>.Shared.Rent(_slabSize));
                break; // prepare etc like normal
            }
        }
        while (!(ReferenceEquals(head,
            head = Interlocked.CompareExchange(ref _head, head.Tail, head)) && AssertCapacityOrRecycle(head, minimumSize)));
        head.Count = 0; // shouldn't ever be needed, but: just to be tidy
        head.Prepare();
        return head;

        static void Throw(int requested, int slabSize)
            => throw new ArgumentOutOfRangeException($"The requested size {requested} exceeds the slab size {slabSize}");

        static bool AssertCapacityOrRecycle([NotNullWhen(true)] Slab? slab, int capacity)
        {
            if (slab is null) return false;
            if (slab.UnusedBytes >= capacity) return true;
            slab.RemoveReference();
            return false;
        }
    }

    internal sealed class Slab : MemoryManager<byte>
    {
        private readonly FrameBufferManager _owner;
        public int Count { get; set; }
        public Slab? Tail { get; set; }


        private readonly byte[] _buffer;

        internal int CurrentHeaderOffset => _currentHeaderOffset;
        private int _currentHeaderOffset, _refCount = 1, _pinCount;
        private Memory<byte> _activePayloadBuffer;
        public Memory<byte> ActiveBuffer => _activePayloadBuffer;


        internal string DebugSummarize(int count) => DebugSummarize(ActiveBuffer.Slice(0, count));
        internal string DebugSummarize(ReadOnlyMemory<byte> buffer)
        {
#if DEBUG
            try
            {
                if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
                {
                    return $"{segment.Count} bytes, [{segment.Offset}, {segment.Offset + segment.Count})";
                }
                else
                {
                    return $"{buffer.Length} bytes";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
#else
            return "";
#endif
        }
        internal string DebugGetHex(int count)
        {
#if DEBUG
            try
            {
                var buffer = ActiveBuffer.Slice(0, count);
                if (!MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
                {
                    segment = buffer.ToArray();
                }
                return BitConverter.ToString(segment.Array!, segment.Offset, segment.Count);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
#else
            return "";
#endif
        }

        public void Advance(int count) => _activePayloadBuffer = _activePayloadBuffer.Slice(start: count);

        private GCHandle _pinHandle;
        public void AddReference()
        {
            lock (this)
            {
                AddReferenceLocked();
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddReferenceLocked()
        {
            if (_refCount == int.MaxValue) Throw();
            _refCount++;
            static void Throw() => throw new InvalidOperationException("Overflow adding buffer reference");
        }
        public void RemoveReference()
        {
            lock (this)
            {
                switch (_refCount)
                {
                    case 0:
                        Throw();
                        break;
                    case 1:
                        if (!_owner.TryReturn(this))
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                        }
                        break;
                }
                _refCount--;
            }
            static void Throw() => throw new InvalidOperationException("Buffer released too many times!");
        }

        internal Slab(FrameBufferManager owner, byte[] buffer)
        {
            _owner = owner;
            _buffer = buffer;
        }
        public void Return()
        {
            _activePayloadBuffer = default;
            lock (this)
            {
                if (!_owner.TryReturn(this)) _refCount--; // not accepted
            }
        }

        public Frame CreateFrameAndInvalidate(FrameHeader header, bool updateHeaderLength)
        {
            // compute the length, and overwrite the header (including the updated length)
            ref byte headerStart = ref _buffer[_currentHeaderOffset];
            var delta = Unsafe.ByteOffset(ref headerStart, ref ActiveBuffer.Span[0]).ToInt64();
            var headerAndPayloadLength = checked((ushort)delta);
            if (headerAndPayloadLength < FrameHeader.Size) ThrowTooSmallForHeader();

            header.UnsafeWrite(ref headerStart);
            var actualPayloadLength = (ushort)(headerAndPayloadLength - FrameHeader.Size);
            if (updateHeaderLength)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(_buffer, _currentHeaderOffset + FrameHeader.PayloadLengthOffset, 2), actualPayloadLength);
            }

            var frame = new Frame(Memory.Slice(_currentHeaderOffset, headerAndPayloadLength), trusted: !updateHeaderLength);
            _currentHeaderOffset += headerAndPayloadLength;
            _activePayloadBuffer = default;
            AddReference(); // for the new buffer
            return frame;
        }

        internal void Prepare()
        {
            var headerOffset = _currentHeaderOffset;
            var availableBytes = _buffer.Length - headerOffset;
            if (availableBytes < FrameHeader.Size) ThrowTooSmallForHeader();
            _activePayloadBuffer = Memory.Slice(headerOffset + FrameHeader.Size, Math.Min(availableBytes - FrameHeader.Size, FrameHeader.MaxPayloadSize));
        }

        static void ThrowTooSmallForHeader() => throw new InvalidOperationException("The available buffer is not large enough for a frame header");

        protected override bool TryGetArray(out ArraySegment<byte> segment)
        {
            segment = new ArraySegment<byte>(_buffer, 0, _buffer.Length);
            return true;
        }

        protected override void Dispose(bool disposing) { } // use Release/RemoveReference

        public override Span<byte> GetSpan() => _buffer;

        public int UnusedBytes => _buffer.Length - _currentHeaderOffset;

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            lock (this)
            {
                switch (_pinCount)
                {
                    case 0:
                        _pinHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
                        break;
                    case int.MaxValue:
                        return Throw();
                        static MemoryHandle Throw() => throw new InvalidOperationException("Overflow pinning buffer reference");
                }
                _pinCount++;
                unsafe
                {
                    return new MemoryHandle(Unsafe.AsPointer(ref _buffer[elementIndex]), default, this);
                }
            }
        }

        public override void Unpin()
        {
            lock (this)
            {
                switch (_pinCount)
                {
                    case 0:
                        Throw();
                        break;
                        static void Throw() => throw new InvalidOperationException("Underflow unpinning buffer reference");
                    case 1:
                        _pinHandle.Free();
                        _pinHandle = default;
                        break;
                }
                _pinCount--;
            }
        }
    }
}

