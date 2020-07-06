using Grpc.Core;
using ProtoBuf.Grpc;
using Xunit;

namespace protobuf_net.Grpc.Test.Issues
{
    public class Issue111
    {
        [Fact]
        public void DirectOptions()
        {
            var options = new CallOptions(new Metadata
            {
                { "Authorization", $"Bearer abc" }
            });
            Assert.Equal("Bearer abc", options.Headers.GetString("Authorization"));
        }

        [Fact]
        public void TwoStepOptions()
        {
            var headers = new Metadata
            {
                { "Authorization", $"Bearer abc" }
            };
            var options = new CallOptions(headers);
            Assert.Equal("Bearer abc", options.Headers.GetString("Authorization"));
        }
    }
}
