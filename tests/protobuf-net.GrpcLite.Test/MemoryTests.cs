using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace protobuf_net.GrpcLite.Test;

public class MemoryTests
{
    private sealed class BasicMemoryManager<T> : MemoryManager<T>
    {
        private readonly T[] _buffer;
        public BasicMemoryManager(int size = 1024) => _buffer = new T[size];

        protected override bool TryGetArray(out ArraySegment<T> segment)
        {
            segment = new ArraySegment<T>(_buffer);
            return true;
        }

        public override Span<T> GetSpan() => _buffer;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(0, 29)]
    [InlineData(32, 32)]
    [InlineData(32, 29)]
    public void SingleBufferTests(int offset, int length)
    {
        var mgr = new BasicMemoryManager<byte>();
        var slice = mgr.Memory.Slice(offset, length);
        Assert.Equal(length, slice.Length);

        var ros = Utilities.AsReadOnlySequence<byte>(slice);
        Assert.Equal(length, ros.Length);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(0, 29)]
    [InlineData(32, 32)]
    [InlineData(32, 29)]
    public void MyOriginalTest(int offset, int length)
    {
        var oversized = RefCountedMemoryPool<byte>.Shared.RentMemory(1024);
        var slice = oversized.Slice(offset, length);
        Assert.Equal(length, slice.Length);
        

        Assert.True(MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(slice, out var mMgr, out var mStart, out var mLength));
        Assert.Equal(length, mLength);

        var ros = Utilities.AsReadOnlySequence<byte>(slice);
        Assert.Equal(length, ros.Length);

        Assert.True(ros.IsSingleSegment);

        Assert.True(MemoryMarshal.TryGetArray(ros.First, out var segment));
        Assert.Equal(mStart, segment.Offset);
        Assert.Equal(mLength, segment.Count);

#if !NET472
        Assert.True(MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(ros.First, out var sMgr, out var sStart, out var sLength));
        Assert.Equal(length, sLength);
        Assert.Equal(mStart, sStart);
        Assert.Same(mMgr, sMgr);
#endif

        RefCountedMemoryPool<byte>.Shared.Return(oversized.Slice(start: length + offset));
    }
}