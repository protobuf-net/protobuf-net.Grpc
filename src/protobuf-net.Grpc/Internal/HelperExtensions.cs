using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal
{
    internal static class HelperExtensions
    {
#if TASK_COMPLETED
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool RanToCompletion(this Task task) => task.IsCompletedSuccessfully;
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool RanToCompletion(this Task task) => task.Status == TaskStatus.RanToCompletion;
#endif
    }
}
