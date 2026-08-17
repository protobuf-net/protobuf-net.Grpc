using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace protobuf_net.Grpc.Test.Issues
{
    // https://github.com/protobuf-net/protobuf-net.Grpc/issues/330
    // operations declared on a [SubService] interface are bound against the top-level
    // [Service] contract; the implementing method must still be located (so that attributes
    // on the implementation are surfaced as metadata), *without* losing the attributes of
    // the top-level contract type.
    public class Issue330
    {
        [Theory]
        [InlineData("/i330/TopLevel", "ContractType,ContractMethod,ImplType,TopLevelImpl")]
        [InlineData("/i330/ImplicitSub", "ContractType,SubContractMethod,ImplType,ImplicitSubImpl")]
        [InlineData("/i330/ExplicitSub", "ContractType,SubContractExplicitMethod,ImplType,ExplicitSubImpl")]
        public void MetadataFromSubServiceOperations(string fullName, string expected)
        {
            var binder = new MetadataServerBinder();
            Assert.Equal(3, binder.Bind<Issue330Service>(null!));

            var actual = string.Join(",", binder.Metadata[fullName].OfType<SomethingAttribute>().Select(x => x.Value));
            Assert.Equal(expected, actual);
        }

        [SubService]
        [Something("SubContractType")]
        public interface IIssue330Sub
        {
            [Something("SubContractMethod")]
            void ImplicitSub();

            [Something("SubContractExplicitMethod")]
            void ExplicitSub();
        }

        [Service("i330")]
        [Something("ContractType")]
        public interface IIssue330Service : IIssue330Sub
        {
            [Something("ContractMethod")]
            void TopLevel();
        }

        [Something("ImplType")]
        public class Issue330Service : IIssue330Service
        {
            [Something("TopLevelImpl")]
            public void TopLevel() { }

            [Something("ImplicitSubImpl")]
            public void ImplicitSub() { }

            [Something("ExplicitSubImpl")]
            void IIssue330Sub.ExplicitSub() { }
        }

        // mirrors what ServicesExtensions does for real: metadata per bound method
        private sealed class MetadataServerBinder : ServerBinder
        {
            public Dictionary<string, IList<object>> Metadata { get; } = [];

            protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext,
                Method<TRequest, TResponse> method, MethodStub<TService> stub)
            {
                Metadata.Add(method.FullName, bindContext.GetMetadata(stub.Method));
                return true;
            }
        }
    }
}
