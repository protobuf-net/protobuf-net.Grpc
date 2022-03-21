﻿using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite.Connections;


public abstract class RefCountedMemoryPool<T> : MemoryPool<T>
{
    public static new RefCountedMemoryPool<T> Shared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SharedWrapper.Instance;
    }
    private static class SharedWrapper // to allow simple lazy/deferred, without any locking etc
    {
        public static readonly RefCountedMemoryPool<T> Instance = new ArrayRefCountedMemoryPool<T>(ArrayPool<T>.Shared);
    }

    public static RefCountedMemoryPool<T> Create(ArrayPool<T>? pool = default)
    {
        if (pool is null || ReferenceEquals(pool, ArrayPool<T>.Shared))
            return ArrayRefCountedMemoryPool<T>.Shared;
        return new ArrayRefCountedMemoryPool<T>(pool);
    }

    public static RefCountedMemoryPool<T> Create(MemoryPool<T> memoryPool)
    {
        if (memoryPool is RefCountedMemoryPool<T> refCounted) return refCounted;
        if (memoryPool is null || ReferenceEquals(memoryPool, MemoryPool<T>.Shared))
            return ArrayRefCountedMemoryPool<T>.Shared;
        return new WrappedRefCountedMemoryPool<T>(memoryPool);
    }

    protected override void Dispose(bool disposing) { }

    public sealed override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        var manager = RentRefCounted(minBufferSize);
        Debug.Assert(MemoryMarshal.TryGetMemoryManager<T, RefCountedMemoryManager<T>>(manager.Memory, out var viaMemory) && ReferenceEquals(viaMemory, manager),
            "incorrect memory manager detected");
        return manager;
    }

    protected abstract RefCountedMemoryManager<T> RentRefCounted(int minBufferSize = -1);
}
public abstract class RefCountedMemoryManager<T> : MemoryManager<T>, IDisposable // re-implement
{

    public sealed override Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => base.Memory; // to prevent implementors from breaking the identity
    }

    private int _refCount, _pinCount;
    private MemoryHandle _pinHandle;
    protected RefCountedMemoryManager()
    {
        _refCount = 1;
    }
    protected sealed override void Dispose(bool disposing)
    {   // shouldn't get here since re-implemented, but!
        if (disposing) Dispose();
    }
    public void Dispose()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            Release();
            GC.SuppressFinalize(this);
        }
    }
    protected abstract void Release();

    public void Preserve() => Interlocked.Increment(ref _refCount);
    protected virtual MemoryHandle Pin() => throw new NotSupportedException(nameof(Pin));
    public sealed override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= Memory.Length) Throw();
        lock (this) // use lock when pinning to avoid races
        {
            if (_pinCount == 0)
            {
                _pinHandle = Pin();
                Preserve(); // pin acts as a ref
            }
            _pinCount = checked(_pinCount + 1); // note: no incr if Pin() not supported
            unsafe
            {   // we can hand this outside the "unsafe", because it is pinned, but:
                // we always use ourselves as the IPinnable - we need to react, etc
                var ptr = _pinHandle.Pointer;
                if (elementIndex != 0) ptr = Unsafe.Add<T>(ptr, elementIndex);
                return new MemoryHandle(ptr, default, this);
            }
        }
        static void Throw() => throw new ArgumentOutOfRangeException(nameof(elementIndex));
    }

    public sealed override void Unpin()
    {
        lock (this) // use lock when pinning to avoid races
        {
            if (_pinCount == 0) Throw();
            if (--_pinCount == 0)
            {
                var tmp = _pinHandle;
                _pinHandle = default;
                tmp.Dispose();
                Dispose(false); // we also took a regular ref
            }
        }
        static void Throw() => throw new InvalidOperationException();
    }
}

internal sealed class ArrayRefCountedMemoryPool<T> : RefCountedMemoryPool<T>
{
    private readonly ArrayPool<T> _pool;
    private readonly int _defaultBufferSize;

    // advertise BCL limits (oddly, ArrayMemoryPool just uses int.MaxValue here, but that's... wrong)
    public override int MaxBufferSize => Unsafe.SizeOf<T>() == 1 ? 0x7FFFFFC7 : 0X7FEFFFFF;
    public ArrayRefCountedMemoryPool(ArrayPool<T> pool, int defaultBufferSize = 8 * 1024)
    {
        if (pool is null) throw new ArgumentNullException(nameof(pool));
        if (defaultBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(defaultBufferSize));
        _defaultBufferSize = defaultBufferSize;
        _pool = pool;
    }
    protected override RefCountedMemoryManager<T> RentRefCounted(int minBufferSize = -1)
        => new ArrayRefCountedMemoryManager(_pool, minBufferSize <= 0 ? _defaultBufferSize : minBufferSize);

    sealed class ArrayRefCountedMemoryManager : RefCountedMemoryManager<T>
    {
        private readonly ArrayPool<T> _pool;
        private T[]? _array;
        private T[] Array
        {
            get
            {
                return _array ?? Throw();
                static T[] Throw() => throw new ObjectDisposedException(nameof(ArrayRefCountedMemoryManager));
            }
        }
        public ArrayRefCountedMemoryManager(ArrayPool<T> pool, int minimumLength)
        {
            _pool = pool;
            _array = pool.Rent(minimumLength);
        }

        public override Span<T> GetSpan() => Array;
        protected override bool TryGetArray(out ArraySegment<T> segment)
        {
            segment = Array;
            return true;
        }
        protected override MemoryHandle Pin()
        {
            var gc = GCHandle.Alloc(Array, GCHandleType.Pinned);
            unsafe
            {
                return new MemoryHandle(gc.AddrOfPinnedObject().ToPointer(), gc, null);
            }
        }

        protected override void Release()
        {
            // note: we're fine if operations after this cause NREs
            var arr = Interlocked.Exchange(ref _array, null);
            if (arr is not null) _pool.Return(arr, clearArray: false);
        }
    }
}

internal sealed class WrappedRefCountedMemoryPool<T> : RefCountedMemoryPool<T>
{
    private MemoryPool<T> _pool;

    public override int MaxBufferSize => _pool.MaxBufferSize;
    public WrappedRefCountedMemoryPool(MemoryPool<T> pool)
        => _pool = pool;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _pool.Dispose(); // we'll assume we have ownership
    }
    protected override RefCountedMemoryManager<T> RentRefCounted(int minBufferSize = -1)
        => new WrappedRefCountedMemoryManager(_pool.Rent(minBufferSize));

    sealed class WrappedRefCountedMemoryManager : RefCountedMemoryManager<T>
    {
        private IMemoryOwner<T>? _owner;
        private IMemoryOwner<T> Owner
        {
            get
            {
                return _owner ?? Throw();
                static IMemoryOwner<T> Throw() => throw new ObjectDisposedException(nameof(WrappedRefCountedMemoryManager));
            }
        }


        public WrappedRefCountedMemoryManager(IMemoryOwner<T> owner)
            => _owner = owner;

        protected override void Release()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Dispose();
        }

        public override Span<T> GetSpan() => Owner.Memory.Span;
        protected override MemoryHandle Pin()
        {
            if (!MemoryMarshal.TryGetMemoryManager<T, MemoryManager<T>>(Owner.Memory, out var manager))
            {
                return Throw();
            }
            return manager.Pin();
            static MemoryHandle Throw() => throw new NotSupportedException(nameof(Pin));
        }
    }


}

public readonly partial struct Frame
{
    public static Builder CreateBuilder(RefCountedMemoryPool<byte>? pool = default)
        => new Builder(pool ?? RefCountedMemoryPool<byte>.Shared);

    public struct Builder
    {
        private readonly RefCountedMemoryPool<byte> _pool;
        private Memory<byte> _oversizedCurrentFrame;
        private int _bytesIntoCurrentFrame;

        public bool InProgress
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _bytesIntoCurrentFrame != 0;
        }

        internal Builder(RefCountedMemoryPool<byte> pool)
        {
            _pool = pool;
            _bytesIntoCurrentFrame = 0;
            _oversizedCurrentFrame = default;
        }

        public Memory<byte> GetBuffer()
        {
            if (_bytesIntoCurrentFrame == 0) EnsureCapacityFor(FrameHeader.Size); // useful for first read
            DebugAssertCapacity(); // check we can read *either* a header or a header+frame
            var buffer = _oversizedCurrentFrame.Slice(_bytesIntoCurrentFrame);
            return buffer;
        }

        // if we haven't read a header yet: request the header bytes (minus whatever we've already read); otherwise, request the entire frame,
        // i.e. the header bytes plus the payload bytes (minus whatever we've already read)
        public int RequestBytes => (_bytesIntoCurrentFrame < FrameHeader.Size ? 0 : GetPayloadLength()) + FrameHeader.Size - _bytesIntoCurrentFrame;

        [Conditional("DEBUG")]
        private void DebugAssertCapacity()
        {
            Debug.Assert(_oversizedCurrentFrame.Length >= FrameHeader.Size, "insufficient buffer space for header");
            if (_bytesIntoCurrentFrame >= FrameHeader.Size)
            {
                Debug.Assert(_oversizedCurrentFrame.Length >= FrameHeader.Size + GetPayloadLength(), "insufficient buffer space for payload");
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetPayloadLength()
        {
            Debug.Assert(_bytesIntoCurrentFrame >= FrameHeader.Size, "can't get payload length; haven't read a complete header");
            var len = FrameHeader.GetPayloadLength(_oversizedCurrentFrame.Span);
            Frame.AssertValidLength(len);
            return len;
        }
        private void EnsureCapacityFor(int required)
        {
            if (_oversizedCurrentFrame.Length < required)
            {
                var newBuffer = RentNewBuffer();
                if (_bytesIntoCurrentFrame != 0)
                {   // copy over any bytes we've already read
                    _oversizedCurrentFrame.Slice(start: 0, length: _bytesIntoCurrentFrame)
                        .CopyTo(newBuffer);
                }
                Return(_oversizedCurrentFrame);
                _oversizedCurrentFrame = newBuffer;
            }
        }

        public void Release()
        {
            var buffer = _oversizedCurrentFrame;
            this = default;
            Return(buffer);
        }

        private static void Return(Memory<byte> memory)
        {
            if (MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(memory, out var manager))
                manager.Dispose();
        }

        private Memory<byte> RentNewBuffer()
        {
            var lease = _pool.Rent(FrameHeader.MaxPayloadLength + FrameHeader.Size);
            return lease.Memory;
        }

        public bool TryRead(ref int bytesRead, out Frame frame)
        {
            if (bytesRead <= 0)
            {
                frame = default;
                return false;
            }
            int take;
            if (_bytesIntoCurrentFrame < FrameHeader.Size)
            {
                // read some more of the header (note: we don't actually *do*
                // anything except book-keeping)
                take = Math.Min(bytesRead, FrameHeader.Size - _bytesIntoCurrentFrame);
                bytesRead -= take;
                _bytesIntoCurrentFrame += take;

                if (_bytesIntoCurrentFrame < FrameHeader.Size)
                {
                    // still not enough
                    frame = default;
                    return false;
                }
            }

            var totalLength = FrameHeader.Size + GetPayloadLength();
            take = Math.Min(bytesRead, totalLength - _bytesIntoCurrentFrame);
            bytesRead -= take;
            _bytesIntoCurrentFrame += take;

            if (_bytesIntoCurrentFrame == totalLength)
            {
                // we've got enough \o/
                frame = CreateFrame();
                return true;
            }

            // check we have capacity for the payload (this preserves data, note)
            EnsureCapacityFor(totalLength);
            frame = default;
            return false;
        }

        public bool TryRead(ref ReadOnlySequence<byte> buffer, out Frame frame)
        {
            var take = (int)Math.Min(buffer.Length, RequestBytes);
            if (take > 0)
            {
                EnsureCapacityFor(_bytesIntoCurrentFrame + take);
                buffer.Slice(start: 0, length: take).CopyTo(GetBuffer().Span);
                _bytesIntoCurrentFrame += take;
                buffer = buffer.Slice(start: take);
                if (RequestBytes == 0)
                {
                    frame = CreateFrame();
                    return true;
                }
            }
            frame = default;
            return false;
        }

        public Memory<byte> NewFrame(in FrameHeader headerTemplate, ushort sequenceId, ushort sizeHint)
        {
            if (InProgress) Throw();
            EnsureCapacityFor(FrameHeader.Size + sizeHint); // the hint only affects the buffer; we write it as zero, and await Advance()

            // write the header (note: we already validated we have enough capacity)
            new FrameHeader(in headerTemplate, sequenceId).UnsafeWrite(ref _oversizedCurrentFrame.Span[0]);
            _bytesIntoCurrentFrame = FrameHeader.Size;

            // provide the (oversized) buffer back to the caller
            return _oversizedCurrentFrame.Slice(start: FrameHeader.Size);

            static void Throw() => throw new InvalidOperationException("A new frame cannot be started while an existing frame is in progress");
        }

        public Frame CreateFrame(bool setFinal = false)
        {
            if (!InProgress) Throw();
            if (setFinal) FrameHeader.SetFinal(_oversizedCurrentFrame.Span);
            var frame = new Frame(_oversizedCurrentFrame.Slice(start: 0, length: _bytesIntoCurrentFrame));
            if (!MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(_oversizedCurrentFrame, out var refCounted))
                return Throw();
            refCounted.Preserve();

            // book-keeping etc
            _oversizedCurrentFrame = _oversizedCurrentFrame.Slice(start: _bytesIntoCurrentFrame);
            _bytesIntoCurrentFrame = 0;
            return frame;
            static Frame Throw() => throw new InvalidOperationException("Unable to obtain the ref-counted memory manager");
        }

        public void Advance(int count)
        {
            if (count < 0 || count > _oversizedCurrentFrame.Length - _bytesIntoCurrentFrame) Throw();
            FrameHeader.SetPayloadLength(_oversizedCurrentFrame.Span, checked((ushort)(GetPayloadLength() + count)));
            _bytesIntoCurrentFrame += count;

            static void Throw() => throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}
