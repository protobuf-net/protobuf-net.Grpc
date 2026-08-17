using System.Reflection;
using System.Reflection.Emit;
#if NET
using System.Collections.Concurrent;
using System.Runtime.Loader;
using System.Threading;
#endif

namespace ProtoBuf.Grpc.Internal;
internal static class ProxyModuleHelper
{
    static ProxyModuleHelper() { }
    public static readonly string ProxyModuleIdentity = typeof(ProxyEmitter).Namespace + ".Proxies";

#if NET
    private static int s_moduleCounter = 0;
    private static string GetNextModuleIdentity()
    {
        return ProxyModuleIdentity + "-" + Interlocked.Increment(ref s_moduleCounter);
    }

    // strong keys, which is fine: a collectible ALC only unloads when Unload() is called, and the
    // Unloading handler below removes it here before collection - so this never pins one that would
    // otherwise have gone away
    private static readonly ConcurrentDictionary<AssemblyLoadContext, ModuleBuilder> _proxyModules = new();

    public static ModuleBuilder GetOrCreateProxyModule(AssemblyLoadContext assemblyLoadContext)
    {
        return _proxyModules.GetOrAdd(assemblyLoadContext, key =>
        {
            using var _ = key.EnterContextualReflection();
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
