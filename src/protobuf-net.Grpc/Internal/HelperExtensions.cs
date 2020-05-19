using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal
{
    internal static class HelperExtensions
    {
#if NET461 || NETSTANDARD2_0
        internal static bool RanToCompletion(this Task task) => task.Status == TaskStatus.RanToCompletion;
#else
        internal static bool RanToCompletion(this Task task) => task.IsCompletedSuccessfully;
#endif
    }
}
