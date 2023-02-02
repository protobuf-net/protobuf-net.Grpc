using System.Threading;

namespace protobuf_net.Grpc.Test.Integration
{
    internal static class PortManager
    {
        // Grpc.Core is very reluctant to release ports, so
        // we'll be more vigilent about not trying to reuse them
        private static int s_Port;
        static PortManager()
        {
            // we have 1024 to 65535 to play with;
            // allow 1000 per TFM, half for down-level,
            // half for up-level
#if NET6_0
            s_Port = 10000;
#elif NET7_0_OR_GREATER
            s_Port = 11000;
#elif NET472
            s_Port = 12000;
#else
#error No port range defined for this TFM
#endif
#if PROTOBUFNET_BUFFERS
            s_Port += 500;
#endif
        }
        public static int GetNextPort() => Interlocked.Increment(ref s_Port);
    }
}
