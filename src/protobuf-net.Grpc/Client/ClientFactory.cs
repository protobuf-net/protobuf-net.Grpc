using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;

#if GRPCSERVER
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
#endif

namespace ProtoBuf.Grpc.Client
{
    public static class ClientFactory
    {
        private const string Switch_AllowUnencryptedHttp2 = "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport";
        public static bool AllowUnencryptedHttp2
        {
            get => AppContext.TryGetSwitch(Switch_AllowUnencryptedHttp2, out var enabled) ? enabled : false;
            set => AppContext.SetSwitch(Switch_AllowUnencryptedHttp2, true);
        }
#if GRPCSERVER
        public static TService Create<TService>(HttpClient httpClient, ILoggerFactory? loggerFactory = null)
            where TService : class => ProxyCache<TService>.Create(httpClient, loggerFactory);
#endif
        public static TService Create<TService>(Channel channel)
            where TService : class => ProxyCache<TService>.Create(channel);
        public static TService Create<TService>(CallInvoker callInvoker)
            where TService : class => ProxyCache<TService>.Create(callInvoker);

        internal readonly struct ProxyCache<TService> where TService : class
        {
            private static readonly ProxyCache<TService> s_factory = ProxyEmitter.CreateFactory<TService>();
#if GRPCSERVER
            public static TService Create(HttpClient httpClient, ILoggerFactory? loggerFactory) => s_factory._httpClient(httpClient, loggerFactory);
#endif
            public static TService Create(CallInvoker callInvoker) => s_factory._callInvoker(callInvoker);
            public static TService Create(Channel channel) => s_factory._channel(channel);

#if GRPCSERVER
            private readonly Func<HttpClient, ILoggerFactory?, TService> _httpClient;
#endif
            private readonly Func<CallInvoker, TService> _callInvoker;
            private readonly Func<Channel, TService> _channel;
            // public readonly Func<ClientBaseConfiguration, TService> ClientBaseConfiguration;

            public ProxyCache(Type type)
            {
#if GRPCSERVER
                if (!FindFactory(type, out _httpClient!)) _httpClient = (a, b) => throw new NotSupportedException();
#endif
                if (!FindFactory(type, out _callInvoker!)) _callInvoker = a => throw new NotSupportedException();
                if (!FindFactory(type, out _channel!)) _channel = a => throw new NotSupportedException();
                // if (!FindFactory(type, out ClientBaseConfiguration!)) ClientBaseConfiguration = a => throw new NotSupportedException();
            }
            static bool FindFactory<T>(Type type, out T? field) where T : Delegate
            {
                field = default;
                if (type == null) return false;
                var invoke = typeof(T).GetMethod("Invoke");
                if (invoke == null) return false;
                var signature = Array.ConvertAll(invoke.GetParameters(), x => x.ParameterType);
                var factory = type.GetMethod(ProxyEmitter.FactoryName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, signature, null);
                if (factory == null) return false;
                field = (T)Delegate.CreateDelegate(typeof(T), factory);
                return true;
            }
        }

#pragma warning disable CS0618
        internal static readonly FieldInfo s_CallContext_Default = typeof(CallContext).GetField(nameof(CallContext.Default))!,
            s_Empty_Instance = typeof(Empty).GetField(nameof(Empty.Instance))!;
#pragma warning restore CS0618

        // this **abstract** inheritance is just to get access to ClientBaseConfiguration
        // (without that, this could be a static class)
        abstract class ProxyEmitter : ClientBase
        {
            private ProxyEmitter() { }

            private static readonly string ProxyIdentity = typeof(ClientFactory).Namespace + ".Proxies";

            private static readonly ModuleBuilder s_module = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName(ProxyIdentity), AssemblyBuilderAccess.Run).DefineDynamicModule(ProxyIdentity);

            private static readonly MethodInfo s_ClientBase_CallInvoker = typeof(ClientBase).GetProperty(nameof(CallInvoker),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetGetMethod(true)!,
                s_Object_ToString = typeof(object).GetMethod(nameof(object.ToString))!;

            private static void Ldc_I4(ILGenerator il, int value)
            {
                switch (value)
                {
                    case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    case int i when (i >= -128 & i < 127): il.Emit(OpCodes.Ldc_I4_S, (sbyte)i); break;
                    default: il.Emit(OpCodes.Ldc_I4, value); break;
                }
            }

            private static void LoadDefault<T>(ILGenerator il) where T : struct
            {
                var local = il.DeclareLocal(typeof(T));
                Ldloca(il, local);
                il.Emit(OpCodes.Initobj, typeof(T));
                Ldloc(il, local);
            }

            private static void Ldloc(ILGenerator il, LocalBuilder local)
            {
                switch (local.LocalIndex)
                {
                    case 0: il.Emit(OpCodes.Ldloc_0); break;
                    case 1: il.Emit(OpCodes.Ldloc_1); break;
                    case 2: il.Emit(OpCodes.Ldloc_2); break;
                    case 3: il.Emit(OpCodes.Ldloc_3); break;
                    case int i when (i >= 0 & i <= 255): il.Emit(OpCodes.Ldloc_S, (byte)i); break;
                    default: il.Emit(OpCodes.Ldloc, local); break;
                }
            }

            private static void Ldloca(ILGenerator il, LocalBuilder local)
            {
                switch (local.LocalIndex)
                {
                    case int i when (i >= 0 & i <= 255): il.Emit(OpCodes.Ldloca_S, (byte)i); break;
                    default: il.Emit(OpCodes.Ldloca, local); break;
                }
            }
            private static void Ldarga(ILGenerator il, ushort index)
            {
                if (index <= 255)
                {
                    il.Emit(OpCodes.Ldarga_S, (byte)index);
                }
                else
                {
                    il.Emit(OpCodes.Ldarga, index);
                }
            }
            private static void Ldarg(ILGenerator il, ushort index)
            {
                switch (index)
                {
                    case 0: il.Emit(OpCodes.Ldarg_0); break;
                    case 1: il.Emit(OpCodes.Ldarg_1); break;
                    case 2: il.Emit(OpCodes.Ldarg_2); break;
                    case 3: il.Emit(OpCodes.Ldarg_3); break;
                    case ushort x when x <= 255: il.Emit(OpCodes.Ldarg_S, (byte)x); break;
                    default: il.Emit(OpCodes.Ldarg, index); break;
                }
            }

            internal static ProxyCache<TService> CreateFactory<TService>()
               where TService : class
            {
                // front-load reflection discovery
                if (!typeof(TService).IsInterface)
                    throw new InvalidOperationException("Type is not an interface: " + typeof(TService).FullName);


                lock (s_module)
                {
                    // private sealed class IFooProxy...
                    var type = s_module.DefineType(ProxyIdentity + "." + typeof(TService).Name + "_Proxy",
                        TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic | TypeAttributes.BeforeFieldInit);

                    // : ClientBase
                    Type baseType = typeof(ClientBase);
                    type.SetParent(baseType);

                    // private IFooProxy() : base() { }
                    type.DefineDefaultConstructor(MethodAttributes.Private);

                    // public IFooProxy(CallInvoker callInvoker) : base(callInvoker) { }
                    var ctorCallInvoker = WritePassThruCtor<CallInvoker>(MethodAttributes.Public);

                    // public IFooProxy(Channel channel) : base(callIchannelnvoker) { }
                    var ctorChannel = WritePassThruCtor<Channel>(MethodAttributes.Public);

                    // private IFooProxy(ClientBaseConfiguration configuration) : base(configuration) { }
                    var ctorClientBaseConfig = WritePassThruCtor<ClientBaseConfiguration>(MethodAttributes.Family);

                    // override ToString
                    {
                        var toString = type.DefineMethod(nameof(ToString), s_Object_ToString.Attributes, s_Object_ToString.CallingConvention,
                        typeof(string), Type.EmptyTypes);
                        var il = toString.GetILGenerator();
                        ContractOperation.TryGetServiceName(typeof(TService), out var primaryServiceName);
                        il.Emit(OpCodes.Ldstr, primaryServiceName);
                        il.Emit(OpCodes.Ret);
                        type.DefineMethodOverride(toString, s_Object_ToString);
                    }

                    var cctor = type.DefineTypeInitializer().GetILGenerator();

                    var ops = ContractOperation.FindOperations(typeof(TService));

                    int fieldIndex = 0;
                    foreach (var iType in ContractOperation.ExpandInterfaces(typeof(TService)))
                    {
                        ContractOperation.TryGetServiceName(iType, out var serviceName);

                        // : TService
                        type.AddInterfaceImplementation(iType);

                        // add each method of the interface
                        foreach (var iMethod in iType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var pTypes = Array.ConvertAll(iMethod.GetParameters(), x => x.ParameterType);
                            var impl = type.DefineMethod(iType.Name + "." + iMethod.Name,
                                    MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Private | MethodAttributes.Virtual,
                                    iMethod.CallingConvention, iMethod.ReturnType, pTypes);
                            // mark it as the interface implementation
                            type.DefineMethodOverride(impl, iMethod);

                            var il = impl.GetILGenerator();
                            if (!ContractOperation.TryIdentifySignature(iMethod, out var op))
                            {
                                il.ThrowException(typeof(NotSupportedException));
                                continue;
                            }

                            Type[] fromTo = new Type[] { op.From, op.To };
                            // public static readonly Method<from, to> s_{i}
                            var field = type.DefineField("s_op_" + fieldIndex++, typeof(Method<,>).MakeGenericType(fromTo),
                                FieldAttributes.Static | FieldAttributes.Public | FieldAttributes.InitOnly);
                            // = new FullyNamedMethod<from, to>(opName, methodType, serviceName, method.Name);
                            cctor.Emit(OpCodes.Ldstr, op.Name); // opName
                            Ldc_I4(cctor, (int)op.MethodType); // methodType
                            cctor.Emit(OpCodes.Ldstr, serviceName); // serviceName
                            cctor.Emit(OpCodes.Ldnull); // methodName: leave null (uses opName)
                            cctor.Emit(OpCodes.Ldnull); // requestMarshaller: always null
                            cctor.Emit(OpCodes.Ldnull); // responseMarshaller: always null
                            cctor.Emit(OpCodes.Newobj, typeof(FullyNamedMethod<,>).MakeGenericType(fromTo)
                                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single()); // new FullyNamedMethod
                            cctor.Emit(OpCodes.Stsfld, field);

                            // implement the method
                            switch (op.Context)
                            {
                                case ContextKind.CallOptions:
                                    // we only support this for signatures that match the exat google pattern, but:
                                    // defer for now
                                    il.ThrowException(typeof(NotImplementedException));
                                    break;
                                case ContextKind.NoContext:
                                case ContextKind.CallContext:
                                    // typically looks something like (where this is an extension method on Reshape):
                                    // => context.{ReshapeMethod}(CallInvoker, {method}, request, [host: null]);
                                    var method = op.TryGetClientHelper();
                                    if (method == null)
                                    {
                                        // unexpected, but...
                                        il.ThrowException(typeof(NotSupportedException));
                                    }
                                    else
                                    {
                                        if (op.Context == ContextKind.CallContext)
                                        {
                                            Ldarga(il, op.VoidRequest ? (ushort)1 : (ushort)2);
                                        }
                                        else
                                        {
                                            il.Emit(OpCodes.Ldsflda, s_CallContext_Default);
                                        }
                                        il.Emit(OpCodes.Ldarg_0); // this.
                                        il.EmitCall(OpCodes.Callvirt, s_ClientBase_CallInvoker, null); // get_CallInvoker

                                        il.Emit(OpCodes.Ldsfld, field); // {method}
                                        if (op.VoidRequest)
                                        {
                                            il.Emit(OpCodes.Ldsfld, s_Empty_Instance); // Empty.Instance
                                        }
                                        else
                                        {
                                            il.Emit(OpCodes.Ldarg_1); // request
                                        }
                                        il.Emit(OpCodes.Ldnull); // host (always null)
                                        il.EmitCall(OpCodes.Call, method, null);
                                        il.Emit(OpCodes.Ret); // return
                                    }
                                    break;
                                case ContextKind.ServerCallContext: // server call? we're writing a client!
                                default: // who knows!
                                    il.ThrowException(typeof(NotSupportedException));
                                    break;
                            }
                        }
                    }

                    cctor.Emit(OpCodes.Ret); // end the type initializer (after creating all the field types)

                    // write a factory method
#if GRPCSERVER
                    WriteFactory(new[] { typeof(HttpClient), typeof(ILoggerFactory) }, typeof(HttpClientCallInvoker), ctorCallInvoker);
#endif
                    WriteFactory(new[] { typeof(CallInvoker) }, null, ctorCallInvoker);
                    WriteFactory(new[] { typeof(Channel) }, null, ctorChannel);
                    // WriteFactory(new[] { typeof(ClientBaseConfiguration) }, null, ctorClientBaseConfig);

                    // return the factory
#if NETSTANDARD2_0
                    return new ProxyCache<TService>(type.AsType());
#else
                    return new ProxyCache<TService>(type.CreateType());
#endif

                    void WriteFactory(Type[] signature, Type? via, ConstructorBuilder? ctor)
                    {
                        if (ctor == null) return;
                        ConstructorInfo? viaCtor = via?.GetConstructor(signature);
                        if (via != null && viaCtor == null) return; // nope!

                        var factory = type.DefineMethod(FactoryName, MethodAttributes.Public | MethodAttributes.Static, typeof(TService), signature);
                        var il = factory.GetILGenerator();
                        for (ushort i = 0; i < signature.Length; i++)
                            Ldarg(il, i);
                        if (viaCtor != null) il.Emit(OpCodes.Newobj, viaCtor);
                        il.Emit(OpCodes.Newobj, ctor);
                        il.Emit(OpCodes.Ret);
                    }

                    ConstructorBuilder? WritePassThruCtor<T>(MethodAttributes accessibility)
                    {
                        var signature = new[] { typeof(T) };
                        var baseCtor = baseType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, signature, null);
                        if (baseCtor == null) return null;

                        var ctor = type.DefineConstructor(accessibility, CallingConventions.HasThis, signature);
                        var il = ctor.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Call, baseCtor);
                        il.Emit(OpCodes.Ret);
                        return ctor;
                    }
                }
            }
            internal const string FactoryName = "Create";
        }
    }
}
