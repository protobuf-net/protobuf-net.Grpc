using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Lite.Internal;

internal partial class FrameBufferManager
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
}

