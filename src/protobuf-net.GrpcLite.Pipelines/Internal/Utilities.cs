namespace ProtoBuf.Grpc.Lite.Internal;

internal static class Utilities
{
    internal static Task GetLazyCompletion(ref object? taskOrCompletion, bool markComplete)
    {   // lazily process _completion
        while (true)
        {
            switch (Volatile.Read(ref taskOrCompletion))
            {
                case null:
                    // try to swap in Task.CompletedTask
                    object newFieldValue;
                    Task result;
                    if (markComplete)
                    {
                        newFieldValue = result = Task.CompletedTask;
                    }
                    else
                    {
                        var newTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        newFieldValue = newTcs;
                        result = newTcs.Task;
                    }
                    if (Interlocked.CompareExchange(ref taskOrCompletion, newFieldValue, null) is null)
                    {
                        return result;
                    }
                    continue; // if we fail the swap: redo from start
                case Task task:
                    return task; // this will be Task.CompletedTask
                case TaskCompletionSource<bool> tcs:
                    if (markComplete) tcs.TrySetResult(true);
                    return tcs.Task;
                default:
                    throw new InvalidOperationException("unexpected completion object");
            }
        }
    }
}
