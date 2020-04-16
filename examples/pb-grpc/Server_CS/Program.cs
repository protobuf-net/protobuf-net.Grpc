using Grpc.Core;
using ProtoBuf.Grpc.Server;
using Shared_CS;
using System;

namespace Server_CS
{
    class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server
            {
                Ports = { new ServerPort("localhost", 10042, ServerCredentials.Insecure) },
            };

            var calc = new MyCalculator();
            server.Services.AddCodeFirst<ICalculator>(calc);

            server.Start();
            Console.WriteLine("Server running... press any key");
            Console.ReadKey();
            server.ShutdownAsync().Wait();
        }
    }
}