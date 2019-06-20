using ProtoBuf.Grpc;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
namespace Shared_CS
{
    [ServiceContract(Name = "Hyper.Calculator")]
    public interface ICalculator
    {
        ValueTask<MultiplyResult> MultiplyAsync(MultiplyRequest request);
    }

    [DataContract]
    public class MultiplyRequest
    {
        public MultiplyRequest() { }
        public MultiplyRequest(int x, int y) => (X, Y) = (x, y);

        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }
    }

    [DataContract]
    public class MultiplyResult
    {
        public MultiplyResult() { }
        public MultiplyResult(int result) => Result = result;

        [DataMember(Order = 1)]
        public int Result { get; set; }
    }

    [ServiceContract]
    public interface IDuplex
    {
        IAsyncEnumerable<MultiplyResult> FullDuplexAsync(IAsyncEnumerable<MultiplyRequest> bar, CallContext context = default);
    }
}
