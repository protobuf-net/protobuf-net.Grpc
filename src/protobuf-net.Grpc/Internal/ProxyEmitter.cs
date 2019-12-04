using Grpc.Core;
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
                        new object[] { channel },
                        null)!;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Func<CallInvoker, TService> EmitFactory<TService>(BinderConfiguration binderConfig)
        {
            Type baseType = typeof(ClientBase);

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
                var marshallerCache = new MarshallerCache(new[] { new ValueTypeWrapperMarshallerFactory(binderConfig.MarshallerCache) });
                FieldBuilder Marshaller(Type forType)
                {
                    if (marshallers.TryGetValue(forType, out var val)) return val.Field;

                    var instance = s_marshallerCacheGenericMethodDef.MakeGenericMethod(forType).Invoke(marshallerCache, Array.Empty<object>())!;
                    var name = "_m" + marshallerIndex++;
                    var field = type.DefineField(name, typeof(Marshaller<>).MakeGenericType(forType), FieldAttributes.Static | FieldAttributes.Private); // **not** readonly, we need to set it afterwards!
                    marshallers.Add(forType, (field, name, instance));
                    return field;
                }

                int valueTypeWrapperTaskContinuationIndex = 0;
                Dictionary<Type, MethodBuilder> valueTypeWrapperTaskContinuations = new Dictionary<Type, MethodBuilder>();
                MethodBuilder ValueTypeWrapperTaskContinuation(Type forType)
                {
                    if (valueTypeWrapperTaskContinuations.TryGetValue(forType, out var val)) return val;

                    var continuationName = "Cont" + valueTypeWrapperTaskContinuationIndex++;
                    var valueTypeWrapperType = typeof(ValueTypeWrapper<>).MakeGenericType(forType);
                    var inputType = typeof(Task<>).MakeGenericType(valueTypeWrapperType);
                    var continuation = type.DefineMethod(continuationName,
                        MethodAttributes.HideBySig | MethodAttributes.Private | MethodAttributes.Static,
                        forType, new[] { inputType });
                    var il = continuation.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0); // original task
                    var resultProperty = inputType.GetProperty("Result")!.GetGetMethod()!;
                    il.EmitCall(OpCodes.Callvirt, resultProperty, null);
                    var valueField = valueTypeWrapperType.GetField("Value")!;
                    il.Emit(OpCodes.Ldfld, valueField);
                    il.Emit(OpCodes.Ret);

                    valueTypeWrapperTaskContinuations.Add(forType, continuation);
                    return continuation;
                }

                int valueTypeWrapperTaskAdapterIndex = 0;
                Dictionary<Type, MethodBuilder> valueTypeWrapperTaskAdapters = new Dictionary<Type, MethodBuilder>();
                MethodBuilder ValueTypeWrapperTaskAdapter(Type forType)
                {
                    if (valueTypeWrapperTaskAdapters.TryGetValue(forType, out var val)) return val;

                    var adapterName = "Vtwta" + valueTypeWrapperTaskAdapterIndex++;
                    var taskGenericType = forType.GetGenericTypeDefinition();
                    var isValueTask = taskGenericType == typeof(ValueTask<>);
                    var resultType = forType.GetGenericArguments()[0];
                    var valueTypeWrapperType = typeof(ValueTypeWrapper<>).MakeGenericType(resultType);
                    var inputType = taskGenericType.MakeGenericType(valueTypeWrapperType);
                    var adapter = type.DefineMethod(adapterName,
                        MethodAttributes.HideBySig | MethodAttributes.Private | MethodAttributes.Static,
                        forType, new[] { inputType });
                    var il = adapter.GetILGenerator();
                    if (isValueTask)
                    {
                        Ldarga(il, 0); // address of original ValueTask
                        var asTask = inputType.GetMethod(nameof(ValueTask.AsTask))!;
                        il.EmitCall(OpCodes.Call, asTask, null); // .AsTask()
                        // the stack now has a ref to a Task<ValueTypeWrapper<T>> on top
                        inputType = typeof(Task<>).MakeGenericType(typeof(ValueTypeWrapper<>).MakeGenericType(resultType));
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldarg_0); // original Task
                    }

                    var continuation = ValueTypeWrapperTaskContinuation(resultType);

                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, continuation);
                    var funcType = typeof(Func<,>).MakeGenericType(inputType, resultType);
                    var funcConstructor = funcType.GetConstructors().Single();
                    il.Emit(OpCodes.Newobj, funcConstructor);

                    var continueWith = ReflectionHelper.GetContinueWithForTask(valueTypeWrapperType, resultType);
                    il.EmitCall(OpCodes.Callvirt, continueWith, null);

                    if (isValueTask)
                    {
                        // change back to a ValueTask
                        var valueTaskConstructor = forType.GetConstructor(new[] { typeof(Task<>).MakeGenericType(resultType) })!;
                        il.Emit(OpCodes.Newobj, valueTaskConstructor);
                    }

                    il.Emit(OpCodes.Ret);

                    valueTypeWrapperTaskAdapters.Add(forType, adapter);
                    return adapter;
                }

                int fieldIndex = 0;
                foreach (var iType in ContractOperation.ExpandInterfaces(typeof(TService)))
                {
                    bool isService = binderConfig.Binder.IsServiceContract(iType, out var serviceName);

                    // : TService
                    type.AddInterfaceImplementation(iType);

                    // add each method of the interface
                    foreach (var iMethod in iType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var pTypes = Array.ConvertAll(iMethod.GetParameters(), x => x.ParameterType);
                        var retType = iMethod.ReturnType;
                        var impl = type.DefineMethod(iType.Name + "." + iMethod.Name,
                                MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Private | MethodAttributes.Virtual,
                                iMethod.CallingConvention, iMethod.ReturnType, pTypes);
                        // mark it as the interface implementation
                        type.DefineMethodOverride(impl, iMethod);

                        var il = impl.GetILGenerator();
                        if (!(isService && ContractOperation.TryIdentifySignature(iMethod, binderConfig, out var op, null)))
                        {
                            il.ThrowException(typeof(NotSupportedException));
                            continue;
                        }

                        Type[] fromTo = new Type[] { op.From, op.To };
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
                                // we only support this for signatures that match the exact google pattern, but:
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
                                            // last argument, plus "this" is pTypes.Length
                                            Ldarga(il, (ushort)pTypes.Length);
                                            // from now on only keep "data" arguments
                                            pTypes = pTypes.Take(pTypes.Length - 1).ToArray();
                                            break;
                                        case ContextKind.CancellationToken:
                                            var callContext = il.DeclareLocal(typeof(CallContext));
                                            // last argument, plus "this" is pTypes.Length
                                            Ldarg(il, (ushort)pTypes.Length);
                                            il.EmitCall(OpCodes.Call, s_CallContext_FromCancellationToken, null);
                                            Stloc(il, callContext);
                                            Ldloca(il, callContext);
                                            // from now on only keep "data" arguments
                                            pTypes = pTypes.Take(pTypes.Length - 1).ToArray();
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
                                        if (op.MethodType == MethodType.Unary || op.MethodType == MethodType.ServerStreaming)
                                        {
                                            if (pTypes.Length > 1)
                                            {
                                                for (int i = 0; i < pTypes.Length; i++)
                                                    Ldarg(il, (ushort)(i + 1));

                                                EmitTupleConstructor(il, pTypes); // request as a new Tuple<>
                                            }
                                            else if (pTypes.Length == 1 && pTypes[0].IsValueType)
                                            {
                                                il.Emit(OpCodes.Ldarg_1); // unwrapped request
                                                var constructor = typeof(ValueTypeWrapper<>).MakeGenericType(pTypes[0]).GetConstructors().Single();
                                                il.Emit(OpCodes.Newobj, constructor); // request wrapped in a ValueTypeWrapper
                                            }
                                            else
                                            {
                                                il.Emit(OpCodes.Ldarg_1); // request
                                            }
                                        }
                                        else
                                        {
                                            il.Emit(OpCodes.Ldarg_1); // request
                                        }
                                    }
                                    il.Emit(OpCodes.Ldnull); // host (always null)
                                    il.EmitCall(OpCodes.Call, method, null);
                                    // similar logic than in ContractOperation.GetSignature : look at the implemented method return type
                                    if (op.Result == ResultKind.Sync && !op.VoidResponse && retType.IsValueType)
                                    {
                                        var retValueTypeWrapperType = typeof(ValueTypeWrapper<>).MakeGenericType(retType);
                                        var valueField = retValueTypeWrapperType.GetField("Value")!;
                                        il.Emit(OpCodes.Ldfld, valueField);
                                    }
                                    else if ((op.Result == ResultKind.Task || op.Result == ResultKind.ValueTask) &&
                                             !op.VoidResponse && retType.GetGenericArguments()[0].IsValueType)
                                    {
                                        var adapter = ValueTypeWrapperTaskAdapter(retType);
                                        il.EmitCall(OpCodes.Call, adapter, null);
                                    }

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
                    Expression.New(finalType.GetConstructor(new[] { typeof(CallInvoker) })!, p), p).Compile();

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

        private static Type EmitTupleConstructor(ILGenerator il, Type[] types)
        {
            Type tupleType;
            if (types.Length > 7)
            {
                var rest = EmitTupleConstructor(il, types.Skip(7).ToArray());
                tupleType = typeof(Tuple<,,,,,,,>).MakeGenericType(types.Take(7).Concat(new [] { rest }).ToArray());
            }
            else
            {
                tupleType = types.Length switch
                {
                    1 => typeof(Tuple<>).MakeGenericType(types),
                    2 => typeof(Tuple<,>).MakeGenericType(types),
                    3 => typeof(Tuple<,,>).MakeGenericType(types),
                    4 => typeof(Tuple<,,,>).MakeGenericType(types),
                    5 => typeof(Tuple<,,,,>).MakeGenericType(types),
                    6 => typeof(Tuple<,,,,,>).MakeGenericType(types),
                    7 => typeof(Tuple<,,,,,,>).MakeGenericType(types),
                    _ => throw new ArgumentException() // not actually possible
                };
            }
            var constructor = tupleType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single();
            il.Emit(OpCodes.Newobj, constructor);
            return tupleType;
        }

        internal static readonly FieldInfo
            s_CallContext_Default = typeof(CallContext).GetField(nameof(CallContext.Default))!,
#pragma warning disable CS0618 // Empty
            s_Empty_Instance = typeof(Empty).GetField(nameof(Empty.Instance))!,
            s_Empty_InstaneTask= typeof(Empty).GetField(nameof(Empty.InstanceTask))!;
#pragma warning restore CS0618

        internal static readonly MethodInfo s_CallContext_FromCancellationToken = typeof(CallContext).GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(CancellationToken) }, null)!;

        internal const string FactoryName = "Create";
    }
}
