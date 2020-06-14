using ProtoBuf.Grpc;
using System;
using Xunit;

namespace protobuf_net.Grpc.Test
{
    public class PushEnumerableTests
    {
        [Fact]
        public void BasicUsage()
        {
            
            var push = PushAsyncEnumerable.Create<int>();
            Assert.False(push.IsCompleted, "push.IsCompleted");

            // write is not complete initially
            var write = push.PushAsync(42);
            Assert.False(write.IsCompleted, "write.IsCompleted #1");

            // read *is* complete initially since data is available
            var iter = push.GetAsyncEnumerator();
            Assert.Equal(0, iter.Current);
            var read = iter.MoveNextAsync();
            Assert.True(read.IsCompletedSuccessfully, "read.IsCompletedSuccessfully");

            // accessing the result of MoveNextAsync is what signals write handover
            Assert.False(write.IsCompleted);
            Assert.True(read.Result, "read.Result");
            Assert.True(write.IsCompleted);
            Assert.Equal(42, iter.Current);

            // our write was successful
            var writeAwaiter = write.GetAwaiter();
            Assert.True(writeAwaiter.IsCompleted);
            writeAwaiter.GetResult();

            // now read *without* data - should be incomplete
            read = iter.MoveNextAsync();
            Assert.False(read.IsCompleted, "read.IsCompletedSuccessfully");

            // write more data - iter is still 42 until we MoveNextAsync
            write = push.PushAsync(27);
            Assert.Equal(42, iter.Current);
            Assert.False(write.IsCompleted, "write.IsCompleted #2");
            Assert.True(read.IsCompletedSuccessfully, "read.IsCompletedSuccessfully");

            // accessing the result of MoveNextAsync is what signals write handover
            Assert.False(write.IsCompleted);
            Assert.True(read.Result, "read.Result");
            Assert.True(write.IsCompleted);
            Assert.Equal(27, iter.Current);

            // now read without ever getting data - should be incomplete
            read = iter.MoveNextAsync();
            Assert.False(read.IsCompleted, "read.IsCompletedSuccessfully");
            Assert.False(push.IsCompleted, "push.IsCompleted");
            push.Complete();
            Assert.True(push.IsCompleted, "push.IsCompleted");

            // iterator should detect end
            Assert.True(read.IsCompleted, "read.IsCompletedSuccessfully");
            Assert.False(read.Result, "read.Result");
            Assert.Equal(0, iter.Current);

            // should not be able to write any more
            write = push.PushAsync(14);
            Assert.True(write.IsFaulted);
            var ex = Assert.Throws<InvalidOperationException>(() => write.GetAwaiter().GetResult());
            Assert.Equal("Cannot push to a sequence that has been completed", ex.Message);

            // can dispose awaiter
            iter.DisposeAsync().GetAwaiter().GetResult();

            // disposing awaited doesn't muddy the water re pushing
            write = push.PushAsync(14);
            Assert.True(write.IsFaulted);
            ex = Assert.Throws<InvalidOperationException>(() => write.GetAwaiter().GetResult());
            Assert.Equal("Cannot push to a sequence that has been completed", ex.Message);

        }
    }
}
