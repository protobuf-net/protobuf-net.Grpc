using Grpc.Core;
using ProtoBuf.Grpc.Client;
using Shared_CS;
using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PlayClient
{
    class Program
    {
        static async Task Main()
        {
            await ViaChannel();
#if HTTPCLIENT
            await ViaHttpClient();
#endif
        }
        static async Task ViaChannel()
        {
            var channel = new Channel("localhost", 10042, ChannelCredentials.Insecure);
            try
            {
                var calculator = channel.CreateGrpcService<ICalculator>();
                await Test(calculator);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        static async Task Test(ICalculator calculator, [CallerMemberName] string? caller = null)
        {
            Console.WriteLine($"testing ({caller})");
            var prod = await calculator.MultiplyAsync(new MultiplyRequest(4, 9));
            Console.WriteLine(prod.Result);
        }

#if HTTPCLIENT
        static async Task ViaHttpClient()
        {
            HttpClientExtensions.AllowUnencryptedHttp2 = true;
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:10042") };
            var calculator = http.CreateGrpcService<ICalculator>();
            await Test(calculator);
        }
#endif
    }
}