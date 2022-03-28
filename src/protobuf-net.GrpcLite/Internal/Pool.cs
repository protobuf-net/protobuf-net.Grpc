using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ProtoBuf.Grpc.Lite.Internal;

internal interface IPooled
{
    void Recycle(); // this is used to enforce the calling pattern; it isn't used by the pool itself
}
internal static class Pool<T> where T : class, IPooled, new()
{
    static T? s_global;
    [ThreadStatic]
    static T? s_Thread;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get()
    {
        var value = s_Thread;
        if (value is not null)
        {
            s_Thread = null;
            return value;
        }
        return Interlocked.Exchange(ref s_global, null) ?? new T();
    }

    public static void Put(T value) // this should be called by Recycle implementations *after* resetting
    {
        Debug.Assert(value is not null, "trying to pool a null " + typeof(T).Name);
        if (s_Thread is null)
        {
            s_Thread = value;
        }
        else
        {
            // keep the newest; avoids branches, and
            // may help avoid artificially longer lifetimes
            Interlocked.Exchange(ref s_global, value);
        }
    }
}
