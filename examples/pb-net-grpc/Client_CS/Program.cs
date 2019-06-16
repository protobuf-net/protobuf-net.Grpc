using ProtoBuf.Grpc.Client;
using Shared_CS;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Client_CS
{
    class Program
    {
        static async Task Main()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            using (var http = new HttpClient { BaseAddress = new Uri("http://localhost:10042") })
            {
                var client = ClientFactory.Create<ICalculator>(http);
                var result = await client.MultiplyAsync(new MultiplyRequest { X = 12, Y = 4 });
                Console.WriteLine(result.Result);
            }
        }
    }
}
