using CodeFirst;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using PerfTest;
using ProtoBuf.Grpc.Client;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PerfClient
{
    static class Program
    {
        static async Task Main()
        {
            var unmanagedClientmanagedClientUnmanagedServer = new Channel("localhost", 10050, ChannelCredentials.Insecure);
            var unmanagedClientmanagedClientManagedServer = new Channel("localhost", 10051, ChannelCredentials.Insecure);
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            var managedClientUnmanagedServer = GrpcChannel.ForAddress("http://localhost:10050");
            var managedClientManagedServer = GrpcChannel.ForAddress("http://localhost:10051");

            Console.WriteLine("warming up...");
            await Test(20, false);
            const int LOOPS = 5, COUNT = 2000;
            for (int i = 1; i <= LOOPS; i++)
            {
                Console.WriteLine($"testing {i} of {LOOPS} (x{COUNT} ops)...");
                await Test(COUNT, true);
            }
            Console.WriteLine("all done!");

            async Task Test(int count, bool log)
            {
                await Test1("unmanaged", "unmanaged", new DefaultCallInvoker(unmanagedClientmanagedClientUnmanagedServer), count, log);
                await Test2("unmanaged", "unmanaged", new DefaultCallInvoker(unmanagedClientmanagedClientUnmanagedServer), count, log);
                await Test3("unmanaged", "unmanaged", unmanagedClientmanagedClientUnmanagedServer.CreateGrpcService<IVanillaGrpc>(), count, log);
                await Test4("unmanaged", "unmanaged", unmanagedClientmanagedClientUnmanagedServer.CreateGrpcService<IProtobufNetGrpc>(), count, log);

                await Test1("unmanaged", "managed", new DefaultCallInvoker(unmanagedClientmanagedClientManagedServer), count, log);
                await Test2("unmanaged", "managed", new DefaultCallInvoker(unmanagedClientmanagedClientManagedServer), count, log);
                await Test3("unmanaged", "managed", unmanagedClientmanagedClientManagedServer.CreateGrpcService<IVanillaGrpc>(), count, log);
                await Test4("unmanaged", "managed", unmanagedClientmanagedClientManagedServer.CreateGrpcService<IProtobufNetGrpc>(), count, log);

                await Test1("managed", "unmanaged", managedClientUnmanagedServer.CreateCallInvoker(), count, log);
                await Test2("managed", "unmanaged", managedClientUnmanagedServer.CreateCallInvoker(), count, log);
                await Test3("managed", "unmanaged", managedClientUnmanagedServer.CreateGrpcService<IVanillaGrpc>(), count, log);
                await Test4("managed", "unmanaged", managedClientUnmanagedServer.CreateGrpcService<IProtobufNetGrpc>(), count, log);

                await Test1("managed", "managed", managedClientManagedServer.CreateCallInvoker(), count, log);
                await Test2("managed", "managed", managedClientManagedServer.CreateCallInvoker(), count, log);
                await Test3("managed", "managed", managedClientManagedServer.CreateGrpcService<IVanillaGrpc>(), count, log);
                await Test4("managed", "managed", managedClientManagedServer.CreateGrpcService<IProtobufNetGrpc>(), count, log);

            }
        }
        static readonly Empty s_empty = new Empty();
        static async Task Test1(string clientNetwork, string serverNetwork, CallInvoker callInvoker, int count, bool log)
        {
            var client = new VanillaGrpc.VanillaGrpcClient(callInvoker);
            await client.ResetAsync(s_empty);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                await client.IncrementAsync(s_empty);
            }
            timer.Stop();
            var final = await client.ResetAsync(s_empty);

            if (log) Console.WriteLine($"{clientNetwork}(vanilla) => {serverNetwork}(vanilla), {count}/{final.Count}, {timer.ElapsedMilliseconds}ms");
        }
        static async Task Test2(string clientNetwork, string serverNetwork, CallInvoker callInvoker, int count, bool log)
        {
            var client = new ProtobufNetGrpc.ProtobufNetGrpcClient(callInvoker);
            await client.ResetAsync(s_empty);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                await client.IncrementAsync(s_empty);
            }
            timer.Stop();
            var final = await client.ResetAsync(s_empty);

            if (log) Console.WriteLine($"{clientNetwork}(vanilla) => {serverNetwork}(pb-net), {count}/{final.Count}, {timer.ElapsedMilliseconds}ms");
        }
        static async Task Test3(string clientNetwork, string serverNetwork, IVanillaGrpc client, int count, bool log)
        {
            await client.ResetAsync();
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                await client.IncrementAsync();
            }
            timer.Stop();
            var final = await client.ResetAsync();

            if (log) Console.WriteLine($"{clientNetwork}(pb-net) => {serverNetwork}(vanilla), {count}/{final.Count}, {timer.ElapsedMilliseconds}ms");
        }
        static async Task Test4(string clientNetwork, string serverNetwork, IProtobufNetGrpc client, int count, bool log)
        {
            await client.ResetAsync();
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                await client.IncrementAsync();
            }
            timer.Stop();
            var final = await client.ResetAsync();

            if (log) Console.WriteLine($"{clientNetwork}(pb-net) => {serverNetwork}(pb-net), {count}/{final.Count}, {timer.ElapsedMilliseconds}ms");
        }
    }
}
