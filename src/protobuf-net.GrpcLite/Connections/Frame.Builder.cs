using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProtoBuf.Grpc.Lite.Connections;

/// <summary>
/// A <see cref="MemoryPool{T}"/> implementation that incorporates reference-counted tracking.
/// </summary>
public abstract class RefCountedMemoryPool<T> : MemoryPool<T>
{
    /// <summary>
    /// Gets a <see cref="RefCountedMemoryPool{T}"/> that uses <see cref="ArrayPool{T}"/>.
    /// </summary>
    public static new RefCountedMemoryPool<T> Shared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SharedWrapper.Instance;
    }
    private static class SharedWrapper // to allow simple lazy/deferred, without any locking etc
    {
        public static readonly RefCountedMemoryPool<T> Instance = new ArrayRefCountedMemoryPool<T>(ArrayPool<T>.Shared);
    }

    /// <summary>
    /// Create a <see cref="RefCountedMemoryPool{T}"/> instance.
    /// </summary>
    public static RefCountedMemoryPool<T> Create(ArrayPool<T>? pool = default)
    {
        if (pool is null || ReferenceEquals(pool, ArrayPool<T>.Shared))
            return ArrayRefCountedMemoryPool<T>.Shared;
        return new ArrayRefCountedMemoryPool<T>(pool);
    }

    /// <summary>
    /// Create a <see cref="RefCountedMemoryPool{T}"/> instance.
    /// </summary>
    public static RefCountedMemoryPool<T> Create(MemoryPool<T> memoryPool)
    {
        if (memoryPool is RefCountedMemoryPool<T> refCounted) return refCounted;
        if (memoryPool is null || ReferenceEquals(memoryPool, MemoryPool<T>.Shared))
            return ArrayRefCountedMemoryPool<T>.Shared;
        return new WrappedRefCountedMemoryPool<T>(memoryPool);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) { }

    /// <summary>
    /// Returns a memory block capable of holding at least minBufferSize elements of <typeparamref name="T"/>.
    /// </summary>
    public Memory<T> RentMemory(int minBufferSize = -1)
    {
        if (minBufferSize <= 0) minBufferSize = 512; // modest default

        for (int slot = GetCacheSlot(minBufferSize); slot < CACHE_SLOTS; slot++)
        {
            var cache = TryGet(slot);
            if (cache is not null && cache.TryDequeue(out var existing))
            {
                Debug.Assert(existing.IsAlive(), "renting dead memory from cache");
                return existing; // note: no change of counter - just transfer of ownership
            }
        }

        return RentNew(minBufferSize).Memory;
    }

    /// <inheritdoc/>
    [Obsolete(nameof(RentMemory) + " should be used instead")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member; this is very intentional - want to push people away from this API, since it can't use fragments
    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        if (minBufferSize <= 0) minBufferSize = 512; // modest default
        return RentNew(minBufferSize);
    }

    private IMemoryOwner<T> RentNew(int minBufferSize)
    {
        var manager = RentRefCounted(minBufferSize);
        Debug.Assert(MemoryMarshal.TryGetMemoryManager<T, RefCountedMemoryManager<T>>(manager.Memory, out var viaMemory) && ReferenceEquals(viaMemory, manager),
            "incorrect memory manager detected");
        Debug.Assert(manager.IsAlive, "new memory is dead!");
        return manager;
    }

    /// <summary>
    /// Identical to <see cref="MemoryPool{T}.Rent(int)"/>, but with support for reference-counting.
    /// </summary>
    protected abstract RefCountedMemoryManager<T> RentRefCounted(int minBufferSize);

    const int MIN_USEFUL_LENGTH = 32, CACHE_SLOTS = 13;
    private static int GetCacheSlot(int length)
    {
        if (length <= MIN_USEFUL_LENGTH) return 0;
        return (26 - Utilities.LeadingZeroCount((uint)length)) / 2;
    }

    internal void Return(Memory<T> unused)
    {
        const int MIN_USEFUL_SIZE = 32, MAX_STORED_PER_KEY = 8;
        if (!MemoryMarshal.TryGetMemoryManager<T, RefCountedMemoryManager<T>>(unused, out var manager, out var start, out var length))
            return; // not ref-counted; just ignore

        Debug.Assert(manager.IsAlive, "returning dead memory");
        Debug.Assert(start + unused.Length == manager.Memory.Length, "expect to be the tail end of the buffer");

        if (unused.Length >= MIN_USEFUL_SIZE && (start + unused.Length != manager.Memory.Length))
        {   // only recycle if we have a sensible amount of space left, and we're returning the entire tail of the buffer
            var slot = GetCacheSlot(unused.Length);
            while (slot >= 0)
            {
                var store = Get(slot);
                if (store.Count < MAX_STORED_PER_KEY)
                {
                    store.Enqueue(unused);
                    return; // note: we don't decrement the counter in this case - just transfer ownership
                }
                slot--; // try again, but storing an oversized buffer in a smaller pot
            }
        }

        // we can't store everything
        manager.Dispose();
    }

    private ConcurrentQueue<Memory<T>>? TryGet(int scale)
        => _fragments[scale];
    private ConcurrentQueue<Memory<T>> Get(int scale)
        => _fragments[scale] ?? GetSlow(scale);

    private ConcurrentQueue<Memory<T>> GetSlow(int scale)
    {
        var value = Volatile.Read(ref _fragments[scale]);
        if (value is null)
        {
            value = new ConcurrentQueue<Memory<T>>();
            value = Interlocked.CompareExchange(ref _fragments[scale], value, null) ?? value;
        }
        return value;
    }

    private ConcurrentQueue<Memory<T>>?[] _fragments = new ConcurrentQueue<Memory<T>>?[CACHE_SLOTS];
}

/// <summary>
/// A <see cref="MemoryManager{T}"/> implementation that incorporates reference-counted tracking.
/// </summary>
public abstract partial class RefCountedMemoryManager<T> : MemoryManager<T>, IDisposable // re-implement
{
    /// <inheritdoc/>
    public sealed override Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => base.Memory; // to prevent implementors from breaking the identity
    }

    private int _refCount, _pinCount;
    private MemoryHandle _pinHandle;
    /// <summary>
    /// Create a new instance.
    /// </summary>
    protected RefCountedMemoryManager()
    {
        _refCount = 1;
    }

    internal bool IsAlive => Volatile.Read(ref _refCount) > 0;

    /// <inheritdoc/>
    protected sealed override void Dispose(bool disposing)
    {   // shouldn't get here since re-implemented, but!
        if (disposing) Dispose();
    }

    /// <summary>
    /// Decrement the reference count associated with this instance; calls <see cref="Release"/> when the count becomes zero.
    /// </summary>
    public void Dispose()
    {
        switch(Interlocked.Decrement(ref _refCount))
        {
            case 0: // all done
                Release();
                GC.SuppressFinalize(this);
                break;
            case -1:
                Throw();
                break;
        }
        static void Throw() => throw new InvalidOperationException("Ref-counted memory was disposed too many times; all bets are off");
    }

    /// <summary>
    /// Recycle the data held by this instance.
    /// </summary>
    protected abstract void Release();

    /// <summary>
    /// Increment the reference count associated with this instance.
    /// </summary>
    public void Preserve()
    {
        if (Interlocked.Increment(ref _refCount) < 0) Throw();
        static void Throw() => throw new InvalidOperationException("Ref-counted memory was preserved too many times; all bets are off");
    }

    /// <summary>
    /// Pin this data so that it does not move during garbage collection.
    /// </summary>
    protected virtual MemoryHandle Pin() => throw new NotSupportedException(nameof(Pin));

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    // advertise BCL limits (oddly, ArrayMemoryPool just uses int.MaxValue here, but that's... wrong)
    public override int MaxBufferSize => Unsafe.SizeOf<T>() == 1 ? 0x7FFFFFC7 : 0X7FEFFFFF;
    public ArrayRefCountedMemoryPool(ArrayPool<T> pool)
    {
        if (pool is null) throw new ArgumentNullException(nameof(pool));
        _pool = pool;
    }
    protected override RefCountedMemoryManager<T> RentRefCounted(int minBufferSize)
        => new ArrayRefCountedMemoryManager(_pool, minBufferSize);

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
            segment = new ArraySegment<T>(Array);
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
    protected override RefCountedMemoryManager<T> RentRefCounted(int minBufferSize)
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
    /// <summary>
    /// Create a new <see cref="Builder"/> for constructing <see cref="Frame"/> values.
    /// </summary>
    public static Builder CreateBuilder(RefCountedMemoryPool<byte>? pool = default, ILogger? logger = null)
        => new Builder(pool ?? RefCountedMemoryPool<byte>.Shared, logger);

    /// <summary>
    /// Assists with constructing <see cref="Frame"/> values, either from a source stream (etc), or from a writer.
    /// </summary>
    public struct Builder
    {
        private readonly RefCountedMemoryPool<byte> _pool;
        private Memory<byte> _oversizedCurrentFrame;
        private int _bytesIntoCurrentFrame;
        private readonly ILogger? _logger;

        /// <summary>
        /// Insicates that a frame is actively being constructed (i.e. unconsumed data is held in the <see cref="Builder"/> instance).
        /// </summary>
        public bool InProgress
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _bytesIntoCurrentFrame != 0;
        }

        internal Builder(RefCountedMemoryPool<byte> pool, ILogger? logger)
        {
            _pool = pool;
            _logger = logger;
            _bytesIntoCurrentFrame = 0;
            _oversizedCurrentFrame = default;
        }

        /// <summary>
        /// Gets a buffer that can be used to perform writes; this buffer will automatically be big enough to be useful in the current state.
        /// </summary>
        public Memory<byte> GetBuffer()
        {
            if (_bytesIntoCurrentFrame == 0) EnsureCapacityFor(FrameHeader.Size, 0); // useful for first read
            DebugAssertCapacity(); // check we can read *either* a header or a header+frame
            var buffer = _oversizedCurrentFrame.Slice(_bytesIntoCurrentFrame);
            Debug.Assert(!buffer.IsEmpty, "providing empty buffer!");
            return buffer;
        }

        // if we haven't read a header yet: request the header bytes (minus whatever we've already read); otherwise, request the entire frame,
        // i.e. the header bytes plus the payload bytes (minus whatever we've already read)

        /// <summary>
        /// Indicates the necessary number of bytes required to make definite progress in constructing a <see cref="Frame"/>.
        /// </summary>
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
        private void EnsureCapacityFor(int required, int bytesRead)
        {
            if (_oversizedCurrentFrame.Length < required)
            {
                var newBuffer = RentNewBuffer();
                var usedBytes = _bytesIntoCurrentFrame + bytesRead;
                if (usedBytes != 0)
                {   // copy over any bytes we've already read
                    _oversizedCurrentFrame.Slice(start: 0, length: usedBytes)
                        .CopyTo(newBuffer);
                }
                _pool.Return(_oversizedCurrentFrame);
                _oversizedCurrentFrame = newBuffer;
            }
        }

        /// <summary>
        /// Releases all resources associated with this builder.
        /// </summary>
        /// <remarks><see cref="IDisposable.Dispose"/> is not used because this is a mutable value-type, and would not work well with "using" etc.</remarks>
        public void Release()
        {
            _pool?.Return(_oversizedCurrentFrame);
            this = default;
        }

        private Memory<byte> RentNewBuffer()
            => _pool.RentMemory(FrameHeader.MaxPayloadLength + FrameHeader.Size);

        /// <summary>
        /// Try to construct a <see cref="Frame"/> from data already written to the buffer; a buffer could contain multiple un-processed frames.
        /// </summary>
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
            _logger.Debug((totalLength, buffer: _oversizedCurrentFrame), static (state, _) => $"parsing header '{state.buffer.Slice(0, FrameHeader.Size).ToHex()}'; total length {state.totalLength}");
            take = Math.Min(bytesRead, totalLength - _bytesIntoCurrentFrame);
            bytesRead -= take;
            _bytesIntoCurrentFrame += take;

            if (_bytesIntoCurrentFrame == totalLength)
            {
                // we've got enough \o/
                frame = CreateFrame(bytesRead);
                _logger.Debug(frame, static (state, _) => $"parsed {state}: {state.GetPayload().ToHex()}");
                Debug.Assert(frame.HasValue, "invalid frame");
                return true;
            }

            // check we have capacity for the payload (this preserves data, note)
            Debug.Assert(bytesRead == 0, "we should have used everything already?");
            EnsureCapacityFor(totalLength, bytesRead);
            frame = default;
            return false;
        }

        /// <summary>
        /// Attempts to construct a <see cref="Frame"/> from data written to the buffer.
        /// </summary>
        public bool TryRead(ref ReadOnlySequence<byte> buffer, out Frame frame)
        {
            var take = (int)Math.Min(buffer.Length, RequestBytes);
            if (take <= 0)
            {
                frame = default;
                return false;
            }

            EnsureCapacityFor(_bytesIntoCurrentFrame + take, 0);
            buffer.Slice(start: 0, length: take).CopyTo(GetBuffer().Span);
            _bytesIntoCurrentFrame += take;
            buffer = buffer.Slice(start: take);

            if (_bytesIntoCurrentFrame >= FrameHeader.Size)
            {
                var totalLength = GetPayloadLength() + FrameHeader.Size;
                take = (int)Math.Min(totalLength - _bytesIntoCurrentFrame, buffer.Length);
                if (take > 0)
                {
                    EnsureCapacityFor(totalLength, 0);
                    buffer.Slice(start: 0, length: take).CopyTo(GetBuffer().Span);
                    _bytesIntoCurrentFrame += take;
                    buffer = buffer.Slice(start: take);
                }

                if (totalLength == _bytesIntoCurrentFrame)
                {
                    frame = CreateFrame(0);
                    _logger.Debug(frame, static (state, _) => $"parsed {state}: {state.Memory.ToHex()}");
                    Debug.Assert(frame.HasValue, "invalid frame");
                    return true;
                }
            }
            frame = default;
            return false;
        }

        /// <summary>
        /// Starts constructing a new frame.
        /// </summary>
        public Memory<byte> NewFrame(in FrameHeader headerTemplate, ushort sequenceId, ushort sizeHint)
        {
            if (InProgress) Throw();
            EnsureCapacityFor(FrameHeader.Size + sizeHint, 0); // the hint only affects the buffer; we write it as zero, and await Advance()

            // write the header (note: we already validated we have enough capacity)
            new FrameHeader(in headerTemplate, sequenceId).UnsafeWrite(ref _oversizedCurrentFrame.Span[0]);
            _bytesIntoCurrentFrame = FrameHeader.Size;

            // provide the (oversized) buffer back to the caller
            return _oversizedCurrentFrame.Slice(start: FrameHeader.Size);

            static void Throw() => throw new InvalidOperationException("A new frame cannot be started while an existing frame is in progress");
        }

        /// <summary>
        /// Creates a frame from the data already written.
        /// </summary>
        public Frame CreateFrame(bool setFinal)
        {
            AssertInProgress();
            if (setFinal) FrameHeader.SetFinal(_oversizedCurrentFrame.Span);
            return CreateFrameCore(0);
        }

        private Frame CreateFrame(int bytesRead)
        {
            AssertInProgress();
            return CreateFrameCore(bytesRead);
        }
        void AssertInProgress()
        {
            if (!InProgress) Throw();
            static void Throw() => throw new InvalidOperationException("No frame is in progress");
        }
        private Frame CreateFrameCore(int bytesRead)
        {
            var frame = new Frame(_oversizedCurrentFrame.Slice(start: 0, length: _bytesIntoCurrentFrame)); // performs a range of validations
            if (!MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(_oversizedCurrentFrame, out var refCounted))
                return Throw();
            refCounted.Preserve();

            // book-keeping etc
            _oversizedCurrentFrame = _oversizedCurrentFrame.Slice(start: _bytesIntoCurrentFrame);
            _bytesIntoCurrentFrame = 0;
            EnsureCapacityFor(FrameHeader.Size, bytesRead); // make sure we have capacity for the next header

            return frame;
            static Frame Throw() => throw new InvalidOperationException("Unable to obtain the ref-counted memory manager");
        }

        /// <summary>
        /// Indicates that a numer of bytes have been written to an existing buffer.
        /// </summary>
        public void Advance(int count)
        {
            if (count < 0 || count > _oversizedCurrentFrame.Length - _bytesIntoCurrentFrame) Throw();
            FrameHeader.SetPayloadLength(_oversizedCurrentFrame.Span, checked((ushort)(GetPayloadLength() + count)));
            _bytesIntoCurrentFrame += count;

            static void Throw() => throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}
