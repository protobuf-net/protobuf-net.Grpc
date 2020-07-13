using Grpc.Core;
using ProtoBuf.Grpc.Client;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;
#nullable disable
namespace protobuf_net.Grpc.Test.Integration.Issues
{
    public class SO62732593
    {
        [Fact]
        public void CanCreateProxy()
        {
            Channel channel = new Channel("none", ChannelCredentials.Insecure);
            var svc = channel.CreateGrpcService<ICustomerService>();
            Assert.NotNull(svc);
        }

        [DataContract]
        public class CustomerResultSet
        {
            [DataMember(Order = 1)]
            public IEnumerable<Customer> Customers { get; set; }
        }
        [DataContract]
        public partial class Customer
        {
            [DataMember(Order = 1)]
            public int CustomerId { get; set; }
            [DataMember(Order = 2)]
            public string CustomerName { get; set; }
        }

        [ServiceContract(Name = "Services.Customer")]
        public interface ICustomerService
        {
            ValueTask<Customer> CreateCustomer(Customer customerDTO);

            ValueTask<CustomerResultSet> GetCustomers();
        }
    }
}
