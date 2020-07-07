using Grpc.Core;
using System.Threading.Tasks;
using Xunit;

namespace protobuf_net.Grpc.Test.Integration.Issues
{
    public class Issue109 // Server refuses to die!
    {
        [Fact(Skip = "definitely fails, but needs Grpc.Core input")]
        public async Task Issue_109()
        {
            var port = PortManager.GetNextPort();

            var server = new Server()
            {
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };
            // it seems that the port is already in use at this point

            // try all kinds of ways to release the port
            await server.ShutdownAsync();
            // await server.KillAsync(); // kill also doesn't release, but can't Shutdown *and* Kill
            await server.ShutdownTask;

            var server2 = new Server()
            {
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };

            // server2.Services.Add(...) // note: repros even without any services
            server2.Start(); // <--- crash, socket is still in use.
        }

        public interface IVoid
        {
            ValueTask Void();
        }
        public class FooService : IVoid {
            ValueTask IVoid.Void() => default;
        }
    }
}
