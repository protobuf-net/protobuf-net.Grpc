using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Net.Client;
using GrpcToolTest;

namespace protobuf_net.Grpc.Tool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new GrpcServices.GrpcServicesClient(GrpcChannel.ForAddress("https://example.org"));
            AllTypes response = await client.GetAllTypesAsync(new AllTypes());
        }
    }
}