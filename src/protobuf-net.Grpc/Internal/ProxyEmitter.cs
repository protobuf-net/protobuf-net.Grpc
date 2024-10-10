using Grpc.Core;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal
{
    internal static class ProxyEmitter
    {
        private static readonly string ProxyIdentity = typeof(ProxyEmitter).Namespace + ".Proxies";

        private static readonly ModuleBuilder s_module = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(ProxyIdentity), AssemblyBuilderAccess.RunAndCollect).DefineDynamicModule(ProxyIdentity);

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

        //private static void LoadDefault<T>(ILGenerator il) where T : struct
        //{
        //    var local = il.DeclareLocal(typeof(T));
        //    Ldloca(il, local);
        //    il.Emit(OpCodes.Initobj, typeof(T));
        //    Ldloc(il, local);
        //}

        //private static void Ldloc(ILGenerator il, LocalBuilder local)
        //{
        //    switch (local.LocalIndex)
        //    {
        //        case 0: il.Emit(OpCodes.Ldloc_0); break;
        //        case 1: il.Emit(OpCodes.Ldloc_1); break;
        //        case 2: il.Emit(OpCodes.Ldloc_2); break;
        //        case 3: il.Emit(OpCodes.Ldloc_3); break;
        //        case int i when (i >= 0 & i <= 255): il.Emit(OpCodes.Ldloc_S, (byte)i); break;
        //        default: il.Emit(OpCodes.Ldloc, local); break;
        //    }
        //}

        private static void Ldloca(ILGenerator il, LocalBuilder local)
        {
            switch (local.LocalIndex)
            {
                case int i when (i >= 0 & i <= 255): il.Emit(OpCodes.Ldloca_S, (byte)i); break;
                default: il.Emit(OpCodes.Ldloca, local); break;
            }
        }

        private static void Stloc(ILGenerator il, LocalBuilder local)
        {
            switch (local.LocalIndex)
            {
                case 0: il.Emit(OpCodes.Stloc_0); break;
                case 1: il.Emit(OpCodes.Stloc_1); break;
                case 2: il.Emit(OpCodes.Stloc_2); break;
                case 3: il.Emit(OpCodes.Stloc_3); break;
                case int i when (i >= 4 & i <= 255): il.Emit(OpCodes.Stloc_S, (byte)i); break;
                default: il.Emit(OpCodes.Stloc, local); break;
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

        static int _typeIndex;
        private static readonly MethodInfo s_marshallerCacheGenericMethodDef
            = typeof(MarshallerCache).GetMethod(nameof(MarshallerCache.GetMarshaller), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        internal static Func<CallInvoker, TService> CreateFactory<TService>(BinderConfiguration binderConfig)
           where TService : class
        {
            // front-load reflection discovery
            if (!typeof(TService).IsInterface)
                throw new InvalidOperationException("Type is not an interface: " + typeof(TService).FullName);

            if (binderConfig == BinderConfiguration.Default) // only use ProxyAttribute for default binder
            {
                var proxy = (typeof(TService).GetCustomAttribute(typeof(ProxyAttribute)) as ProxyAttribute)?.Type;
                if (proxy is object) return CreateViaActivator<TService>(proxy);
            }

            return EmitFactory<TService>(binderConfig);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Func<CallInvoker, TService> CreateViaActivator<TService>(Type type)
        {
            return channel => (TService)Activator.CreateInstance(
                        type,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        [channel],
                        null)!;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Func<CallInvoker, TService> EmitFactory<TService>(BinderConfiguration binderConfig)
        {
            Type baseType = GrpcClientFactory.ClientBaseType;

            var callInvoker = baseType.GetProperty("CallInvoker", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetGetMethod(true);
            if (callInvoker == null || callInvoker.ReturnType != typeof(CallInvoker) || callInvoker.GetParameters().Length != 0)
                throw new ArgumentException($"The base-type {baseType} for service-proxy {typeof(TService)} lacks a suitable CallInvoker API");

            lock (s_module)
            {
                // private sealed class IFooProxy...
                var type = s_module.DefineType(ProxyIdentity + "." + baseType.Name + "." + typeof(TService).Name + "_Proxy_" + _typeIndex++,
                    TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic | TypeAttributes.BeforeFieldInit);

                type.SetParent(baseType);

                // public IFooProxy(CallInvoker callInvoker) : base(callInvoker) { }
                var ctorCallInvoker = WritePassThruCtor<CallInvoker>(MethodAttributes.Public);

                // override ToString
                {
                    var baseToString = typeof(object).GetMethod(nameof(object.ToString))!;
                    var toString = type.DefineMethod(nameof(ToString), baseToString.Attributes, baseToString.CallingConvention,
                    typeof(string), Type.EmptyTypes);
                    var il = toString.GetILGenerator();
                    if (!binderConfig.Binder.IsServiceContract(typeof(TService), out var primaryServiceName)) primaryServiceName = typeof(TService).Name;
                    il.Emit(OpCodes.Ldstr, primaryServiceName + " / " + typeof(CallInvoker).Name);
                    il.Emit(OpCodes.Ret);
                    type.DefineMethodOverride(toString, baseToString);
                }

                const string InitMethodName = "Init";
                var cctor = type.DefineMethod(InitMethodName, MethodAttributes.Static | MethodAttributes.Public).GetILGenerator();

                var ops = ContractOperation.FindOperations(binderConfig, typeof(TService), null);

                int marshallerIndex = 0;
                Dictionary<Type, (FieldBuilder Field, string Name, object Instance)> marshallers = new Dictionary<Type, (FieldBuilder, string, object)>();
                FieldBuilder Marshaller(Type forType)
                {
                    if (marshallers.TryGetValue(forType, out var val)) return val.Field;

                    var instance = s_marshallerCacheGenericMethodDef.MakeGenericMethod(forType).Invoke(binderConfig.MarshallerCache, Array.Empty<object>())!;
                    var name = "_m" + marshallerIndex++;
                    var field = type.DefineField(name, typeof(Marshaller<>).MakeGenericType(forType), FieldAttributes.Static | FieldAttributes.Private); // **not** readonly, we need to set it afterwards!
                    marshallers.Add(forType, (field, name, instance));
                    return field;

                }

                int fieldIndex = 0;
                var contractExpandInterfaces = ContractOperation.ExpandInterfaces(typeof(TService))
                    .ToArray();
                foreach (var iType in contractExpandInterfaces)
                {
                    bool isService = binderConfig.Binder.IsServiceContract(iType, out var serviceName);

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
                        
                        // check whether the method belongs to [ServiceInherited] interface
                        var isMethodInherited =
                            iMethod.DeclaringType?.IsDefined(typeof(SubServiceAttribute)) ?? false;
                        var shallMethodBeImplemented = isService || isMethodInherited;
                        if (!(shallMethodBeImplemented && ContractOperation.TryIdentifySignature(iMethod, binderConfig, out var op, null)))
                        {
                            // it is frequent for some infrastructure code to always call Dispose() on IDisposable,
                            // for instance Asp.Net Core dependency injection, so we don't want to throw in this case
                            if (iType == typeof(IDisposable) && iMethod.Name == nameof(IDisposable.Dispose))
                            {
                                il.Emit(OpCodes.Ret);
                            }
                            else if (iType == typeof(IAsyncDisposable) && iMethod.Name == nameof(IAsyncDisposable.DisposeAsync))
                            {
                                // "return default;"
                                var loc = il.DeclareLocal(typeof(ValueTask));
                                il.Emit(OpCodes.Ldloca, loc);
                                il.Emit(OpCodes.Initobj, typeof(ValueTask));
                                il.Emit(OpCodes.Ldloc, loc);
                                il.Emit(OpCodes.Ret);
                            }
                            else
                                il.ThrowException(typeof(NotSupportedException));
                            continue;
                        }
                        
                        // in case method belongs to an sub-service interface, we have to find the service contract name inheriting it
                        if (isMethodInherited)
                        {
                            if (!binderConfig.Binder.TryFindInheritedService(iType, contractExpandInterfaces, out serviceName))
                            {
                                il.ThrowException(typeof(NotSupportedException));
                                continue;
                            }
                        }

                        Type[] fromTo = [op.From, op.To];
                        // private static Method<from, to> s_{i}
                        var field = type.DefineField("s_op_" + fieldIndex++, typeof(Method<,>).MakeGenericType(fromTo),
                            FieldAttributes.Static | FieldAttributes.Private);
                        // = new Method<from, to>(methodType, serviceName, opName, requestMarshaller, responseMarshaller);
                        Ldc_I4(cctor, (int)op.MethodType); // methodType
                        cctor.Emit(OpCodes.Ldstr, serviceName ?? ""); // serviceName
                        cctor.Emit(OpCodes.Ldstr, op.Name); // opName
                        cctor.Emit(OpCodes.Ldsfld, Marshaller(op.From)); // requestMarshaller
                        cctor.Emit(OpCodes.Ldsfld, Marshaller(op.To)); // responseMarshaller
                        cctor.Emit(OpCodes.Newobj, typeof(Method<,>).MakeGenericType(fromTo)
                            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single()); // new Method
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
                            case ContextKind.CancellationToken:
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
                                    switch (op.Context)
                                    {
                                        case ContextKind.CallContext:
                                            Ldarga(il, op.VoidRequest ? (ushort)1 : (ushort)2);
                                            break;
                                        case ContextKind.CancellationToken:
                                            var callContext = il.DeclareLocal(typeof(CallContext));
                                            Ldarg(il, op.VoidRequest ? (ushort)1 : (ushort)2);
                                            il.EmitCall(OpCodes.Call, s_CallContext_FromCancellationToken, null);
                                            Stloc(il, callContext);
                                            Ldloca(il, callContext);
                                            break;
                                        case ContextKind.NoContext:
                                            il.Emit(OpCodes.Ldsflda, s_CallContext_Default);
                                            break;
                                        default:
                                            // shouldn't get here - we checked above! this is in case of code maintenance errors
                                            throw new NotImplementedException($"Unhandled context kind: {op.Context}");
                                    }
                                    il.Emit(OpCodes.Ldarg_0); // this.
                                    il.EmitCall(OpCodes.Callvirt, callInvoker, null); // get_CallInvoker

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

#if NETSTANDARD2_0
                var finalType = type.CreateTypeInfo()!;
#else
                var finalType = type.CreateType()!;
#endif
                // assign the marshallers and invoke the init
                foreach((var field, var name, var instance) in marshallers.Values)
                {
                    finalType.GetField(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)!.SetValue(null, instance);
                }
                finalType.GetMethod(InitMethodName, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)!.Invoke(null, Array.Empty<object>());

                // return the factory
                var p = Expression.Parameter(typeof(CallInvoker), "channel");
                return Expression.Lambda<Func<CallInvoker, TService>>(
                    Expression.New(finalType.GetConstructor([typeof(CallInvoker)])!, p), p).Compile();

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
        internal static readonly FieldInfo
            s_CallContext_Default = typeof(CallContext).GetField(nameof(CallContext.Default))!,
#pragma warning disable CS0618 // Empty
            s_Empty_Instance = typeof(Empty).GetField(nameof(Empty.Instance))!,
            s_Empty_InstaneTask= typeof(Empty).GetField(nameof(Empty.InstanceTask))!;
#pragma warning restore CS0618

        internal static readonly MethodInfo s_CallContext_FromCancellationToken = typeof(CallContext).GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, null, [typeof(CancellationToken)], null)!;

        internal const string FactoryName = "Create";
    }
}
