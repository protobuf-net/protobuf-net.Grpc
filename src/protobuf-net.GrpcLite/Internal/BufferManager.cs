using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite.Internal;

internal class BufferManager
{
    private readonly int _slabSize;
    private Slab? _head;
    const int MIN_SLAB_SIZE = 1024; // at least 1KiB
    public BufferManager(int slabSize) => _slabSize = Math.Max(slabSize, MIN_SLAB_SIZE);
    public static BufferManager Shared { get; } = new BufferManager(64 * 1024);

    public int Slabs => Volatile.Read(ref _head)?.Count ?? 0;
    internal bool TryAttach(Slab? value)
    {
        if (value is not null)
        {
            Slab? tail = Volatile.Read(ref _head);
            do
            {
                value.Tail = tail;
                value.Count = 1 + (tail?.Count ?? 0);
            }
            while (ReferenceEquals(tail,
                tail = Interlocked.CompareExchange(ref _head, value, tail)));
            return true;
        }
        return false;
    }
    private Slab Detach()
    {
        Slab? head = Volatile.Read(ref _head);
        do
        {
            if (head is null) return new Slab(this, ArrayPool<byte>.Shared.Rent(_slabSize));
        }
        while (ReferenceEquals(head,
            head = Interlocked.CompareExchange(ref _head, head.Tail, head)));
        head!.Count = 0; // shouldn't ever be needed, but: just to be tidy
        return head;
    }

    public ScratchBuffer Rent(int hint)
    {
        const int MIN_REASONABLE_SIZE = 128; // unless hint is smaller, in which case we'll allow it
        while (true)
        {
            var slab = Detach();
            var buffer = slab.GetScratchBuffer(out int length);
            if (length >= Math.Min(hint, MIN_REASONABLE_SIZE))
            {   // don't change ref-count
                return buffer;
            }
            else
            {
                // otherwise, we'll drop that slab, and try again
                slab.RemoveReference();
            }
        }
    }
}

internal sealed class Slab : MemoryManager<byte>
{
    private readonly BufferManager _owner;
    public int Count { get; set; }
    public Slab? Tail { get; set; }

    public ScratchBuffer GetScratchBuffer(out int length)
    {
        lock (this)
        {
            length = _buffer.Length - _committed;
            return new ScratchBuffer(this, _scratchToken);
        }
    }

    private readonly byte[] _buffer;

    private int _committed, _scratchToken, _refCount = 1, _pinCount;
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
                    if (!_owner.TryAttach(this))
                    {
                        ArrayPool<byte>.Shared.Return(_buffer);
                    }
                    break;
            }
            _refCount--;
        }
        static void Throw() => throw new InvalidOperationException("Buffer released too many times!");
    }
    public int Committed => _committed;

    public byte[] Buffer => _buffer;

    public Slab(BufferManager owner, byte[] buffer)
    {
        _owner = owner;
        _buffer = buffer;
    }
    public BufferSegment Commit(int length, int token)
    {
        int offset;
        lock (this)
        {
            offset = _committed;
            if (length < 0 || length > _buffer.Length - offset) ThrowLength();
            if (_scratchToken != token) ThrowTokenMismatch();
            _scratchToken++;
            _committed = offset + length;
            AddReferenceLocked(); // for the buffer
            if (!_owner.TryAttach(this)) _refCount--;
        }
        return new BufferSegment(this, offset, length);

        static void ThrowLength() => throw new ArgumentOutOfRangeException(nameof(length));
    }
    static void ThrowTokenMismatch() => throw new InvalidOperationException("Slab token mismatch");

    internal Memory<byte> GetMemory(int token)
    {
        lock (this)
        {
            if (_scratchToken != token) ThrowTokenMismatch();
            var offset = _committed;
            return Memory.Slice(offset, _buffer.Length - offset);
        }
    }
    internal bool TryGetArray(int token, out ArraySegment<byte> segment)
    {
        lock (this)
        {
            if (_scratchToken != token) ThrowTokenMismatch();
            var offset = _committed;
            segment = new ArraySegment<byte>(_buffer, offset, _buffer.Length - offset);
            return true;
        }
    }

    internal ref byte this[int token, int index]
    {
        get
        {
            lock (this)
            {
                if (_scratchToken != token) ThrowTokenMismatch();
                var offset = _committed;
                if (index < 0 || index >= _buffer.Length - offset) Throw();
                return ref _buffer[offset + index];

                static void Throw() => throw new IndexOutOfRangeException();

            }
        }
    }

    protected override bool TryGetArray(out ArraySegment<byte> segment)
    {
        segment = new ArraySegment<byte>(_buffer, 0, _buffer.Length);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetArray(int offset, int length, out ArraySegment<byte> buffer)
    {
        buffer = new ArraySegment<byte>(_buffer, offset, length);
        return true;
    }

    public ref byte this[int offset] => ref _buffer[offset];

    protected override void Dispose(bool disposing) { } // use Release/RemoveReference

    public override Span<byte> GetSpan() => _buffer;
    public override Memory<byte> Memory => _buffer;

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
internal readonly struct ScratchBuffer
{
    internal ScratchBuffer(Slab slab, int token)
    {
        _slab = slab;
        _token = token;
    }
    private readonly Slab? _slab;
    private readonly int _token;
    public Memory<byte> Memory => _slab?.GetMemory(_token) ?? default;
    public bool TryGetArray(out ArraySegment<byte> segment)
        => _slab is not null ? _slab.TryGetArray(_token, out segment) : Utilities.TryGetEmptySegment(out segment);
    

    public ref byte this[int index]
    {
        get
        {
            if (_slab is not null) return ref _slab[_token, index];
            return ref Utilities.EmptyBuffer[index];
        }
    }

    public BufferSegment Commit(int length)
    {
        return _slab is not null ? _slab.Commit(length, _token) : BufferSegment.Empty;
    }
    
}
