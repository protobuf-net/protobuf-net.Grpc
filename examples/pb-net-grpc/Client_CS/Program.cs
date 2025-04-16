#define ADVANCED

using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using Shared_CS;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client_CS
{
    class Program
    {
        static async Task Main()
        {
            AdvancedBuffer.Register();
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress("http://localhost:10042");

            var svc = http.CreateGrpcService<IBufferScenarios>();
            int[] lengths = [8, 64, 1024, 16 * 1024];
            const int COUNT = 10000;

#if SIMPLE
            foreach (var len in lengths)
            {
                int total = 0;
                await foreach (var item in svc.Simple(GenerateSimple(len, COUNT)))
                {
                    total += item.Data.Length;
                }
                Console.WriteLine($"Simple: {len}x{COUNT}: {total}");
            }
#endif

#if ADVANCED

            foreach (var len in lengths)
            {
                int total = 0;
                await foreach (var item in svc.Advanced(GenerateAdvanced(len, COUNT)))
                {
                    total += item.Length;
                    item.Dispose(); // inbound lifetime management (outbound is handled by marshaller)
                }
                Console.WriteLine($"Advanced: {len}x{COUNT}: {total}");
            }
#endif
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<SimpleBuffer> GenerateSimple(int len, int count)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            for (int i = 0; i < count; i++)
            {
                var data = new byte[len];
                Random.Shared.NextBytes(data);
                yield return new() { Data = data };
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<AdvancedBuffer> GenerateAdvanced(int len, int count)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            for (int i = 0; i < count; i++)
            {
                AdvancedBuffer data = new(len);
                Random.Shared.NextBytes(data.Span);
                yield return data;
            }
        }
    }
}
