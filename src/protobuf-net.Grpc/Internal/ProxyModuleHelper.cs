using System.Reflection;
using System.Reflection.Emit;
#if NET6_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Runtime.Loader;
using System.Threading;
#endif

namespace ProtoBuf.Grpc.Internal;
internal static class ProxyModuleHelper
{
    static ProxyModuleHelper() { }
    public static readonly string ProxyModuleIdentity = typeof(ProxyEmitter).Namespace + ".Proxies";

#if NET6_0_OR_GREATER
    private static int s_moduleCounter = 0;
    private static string GetNextModuleIdentity()
    {
        return ProxyModuleIdentity + "-" + Interlocked.Increment(ref s_moduleCounter);
    }

    private static readonly ConcurrentDictionary<AssemblyLoadContext, ModuleBuilder> _proxyModules = new();

    public static ModuleBuilder GetOrCreateProxyModule(AssemblyLoadContext assemblyLoadContext)
    {
        return _proxyModules.GetOrAdd(assemblyLoadContext, key =>
        {
            var alc = CreateProxyModule(GetNextModuleIdentity());
            key.Unloading += _ => RemoveAssemblyLoadContext(key);
            return alc;
        });
    }
    private static bool RemoveAssemblyLoadContext(AssemblyLoadContext alc)
    {
        return _proxyModules.TryRemove(alc, out _);
    }
#else

    public static readonly ModuleBuilder MainProxyModule = CreateProxyModule(ProxyModuleIdentity);

#endif

    private static ModuleBuilder CreateProxyModule(string moduleIdentity)
    {
        var name = new AssemblyName(moduleIdentity);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
        return assembly.DefineDynamicModule(moduleIdentity);
    }
}
