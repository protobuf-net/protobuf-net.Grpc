#if CLIENT_FACTORY
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace protobuf_net.Grpc.Test;
public class AssemblyLoadContextsClientFactoryTests
{
    [Fact]
    public void CanCreateProxiesInAssemblyLoadContexts()
    {
        const string TestProxyAssemblyFileName = "protobuf-net.Grpc.Tests.TestProxy.dll";

        string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        AssemblyLoadContext shared = CreatePluginAssemblyLoadContext("Shared", folder);
        AssemblyLoadContext plugin1 = CreatePluginAssemblyLoadContext("alc1", folder, shared);
        AssemblyLoadContext plugin2 = CreatePluginAssemblyLoadContext("alc2", folder, shared);

        string proxyPath = Path.Combine(folder, TestProxyAssemblyFileName);

        var proxyAssembly1 = plugin1.LoadFromAssemblyPath(proxyPath);
        var proxyAssembly2 = plugin2.LoadFromAssemblyPath(proxyPath);

        object? proxy1 = CreateAndAssertProxy(proxyAssembly1, plugin1);
        object? proxy2 = CreateAndAssertProxy(proxyAssembly2, plugin2);

        Assert.NotSame(proxy1!.GetType(), proxy2!.GetType());

        object? anotherProxy1 = CreateAndAssertProxy(proxyAssembly1, plugin1);
        Assert.NotSame(proxy1, anotherProxy1);
        Assert.Equal(proxy1.GetType(), anotherProxy1!.GetType());


        object? anotherProxy2 = CreateAndAssertProxy(proxyAssembly2, plugin2);
        Assert.NotSame(proxy2, anotherProxy2);
        Assert.Equal(proxy2.GetType(), anotherProxy2!.GetType());
    }

    private static object? CreateAndAssertProxy(Assembly proxyAssembly, AssemblyLoadContext expectedAssemblyLoadContext)
    {
        Assert.NotNull(proxyAssembly);
        object? proxy = CreateProxy(proxyAssembly);
        Assert.NotNull(proxy);
        Type pluginType = proxy!.GetType();
        Assert.Equal("IProxy", pluginType.GetInterfaces().First().Name);
        Assert.Equal(expectedAssemblyLoadContext, AssemblyLoadContext.GetLoadContext(pluginType.Assembly));
        return proxy;
    }

    private static AssemblyLoadContext CreatePluginAssemblyLoadContext(string name, string folder, AssemblyLoadContext? shared = null)
    {
        return new PluginLoadContext(name, folder, shared);
    }

    private static object? CreateProxy(Assembly assembly1)
    {
        var proxycreator = assembly1.DefinedTypes.FirstOrDefault(t => t.Name == "ProxyFactory");
        Assert.NotNull(proxycreator);
        var method = proxycreator!.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, new object[] { });
    }

    private class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyLoadContext? _sharedContext;
        private readonly string _folder;

        public PluginLoadContext(string name, string folder, AssemblyLoadContext? sharedContext = null)
            : base(name, isCollectible: true)
        {
            _sharedContext = sharedContext;
            _folder = folder;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (_sharedContext is not null)
            {
                return _sharedContext.LoadFromAssemblyName(assemblyName);
            }

            string path = Path.Combine(_folder, assemblyName.Name + ".dll");
            if (File.Exists(path))
            {
                return LoadFromAssemblyPath(path);
            }
            return null;
        }
    }
}
#endif
