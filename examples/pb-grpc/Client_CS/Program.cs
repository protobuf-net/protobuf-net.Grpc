using Grpc.Core;
using ProtoBuf.Grpc.Client;
using Shared_CS;
using System;
using System.Threading.Tasks;

namespace Client_CS
{
    class Program
    {
        static async Task Main()
        {
            var channel = new Channel("localhost", 10042, ChannelCredentials.Insecure);
            try
            {
                var calculator = channel.CreateGrpcService<ICalculator>();
                var response = await calculator.MultiplyAsync(new MultiplyRequest() { X = 2, Y = 4 });
                if (response.Result != 8)
                {
                    throw new InvalidOperationException();
                }
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }
    }
}