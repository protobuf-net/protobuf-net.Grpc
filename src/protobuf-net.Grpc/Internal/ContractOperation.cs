using System;
using System.Collections.Generic;
using System.Reflection;
using Grpc.Core;
using System.ServiceModel;
using System.Buffers;
using System.Threading.Tasks;
using System.Linq;

namespace ProtoBuf.Grpc.Internal
{
    internal readonly struct ContractOperation
    {
        public string Name { get; }
        public Type From { get; }
        public Type To { get; }
        public MethodInfo Method { get; }
        public Type[] ParameterTypes { get; }
        public MethodType MethodType { get; }
        public ContextKind Context { get; }
        public ResultKind Result { get; }

        public override string ToString() => $"{Name}: {From.Name}=>{To.Name}, {MethodType}, {Result}, {Context}";

        public ContractOperation(string name, Type from, Type to, MethodInfo method,
            MethodType methodType, ContextKind contextKind, ResultKind resultKind,
            Type[] parameterTypes)
        {
            Name = name;
            From = from;
            To = to;
            Method = method;
            MethodType = methodType;
            Context = contextKind;
            Result = resultKind;
            ParameterTypes = parameterTypes;
        }

        public static bool TryGetServiceName(Type contractType, out string? serviceName, bool demandAttribute = false)
        {
            var sca = (ServiceContractAttribute?)Attribute.GetCustomAttribute(contractType, typeof(ServiceContractAttribute), inherit: true);
            if (demandAttribute && sca == null)
            {
                serviceName = null;
                return false;
            }
            serviceName = sca?.Name;
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                serviceName = contractType.Name;
                if (contractType.IsInterface && serviceName.StartsWith('I')) serviceName = serviceName.Substring(1); // IFoo => Foo
                serviceName = contractType.Namespace + serviceName; // Whatever.Foo
                serviceName = serviceName.Replace('+', '.'); // nested types
            }
            return !string.IsNullOrWhiteSpace(serviceName);
        }

        // do **not** replace these with a `params` etc version; the point here is to be as cheap
        // as possible for misses
        internal static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? tRet)
            => parameters.Length == 0
            && IsMatch(tRet, returnType, out types[0]);
        internal static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? t0, Type? tRet)
            => parameters.Length == 1
            && IsMatch(t0, parameters[0].ParameterType, out types[0])
            && IsMatch(tRet, returnType, out types[1]);
        internal static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? t0, Type? t1, Type? tRet)
            => parameters.Length == 2
            && IsMatch(t0, parameters[0].ParameterType, out types[0])
            && IsMatch(t1, parameters[1].ParameterType, out types[1])
            && IsMatch(tRet, returnType, out types[2]);
        internal static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? t0, Type? t1, Type? t2, Type? tRet)
            => parameters.Length == 3
            && IsMatch(t0, parameters[0].ParameterType, out types[0])
            && IsMatch(t1, parameters[1].ParameterType, out types[1])
            && IsMatch(t2, parameters[2].ParameterType, out types[2])
            && IsMatch(tRet, returnType, out types[3]);

        private static bool IsMatch(in Type? template, in Type actual, out Type result)
        {
            if (template == null || template == actual)
            {
                result = actual;
                return true;
            } // fine
            if (actual.IsGenericType && template.IsGenericTypeDefinition
                && actual.GetGenericTypeDefinition() == template)
            {
                // expected Foo<>, got Foo<T>: report T
                result = actual.GetGenericArguments()[0];
                return true;
            }
            result = typeof(void);
            return false;
        }

        public static List<ContractOperation> FindOperations(Type contractType, bool demandAttribute = false)
        {
            var ops = new List<ContractOperation>();
            var types = ArrayPool<Type>.Shared.Rent(10);
            try
            {
                foreach (var method in contractType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (method.IsGenericMethodDefinition) continue; // can't work with <T> methods

                    var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
                    if (demandAttribute && oca == null) continue;
                    string? opName = oca?.Name;
                    if (string.IsNullOrWhiteSpace(opName))
                    {
                        opName = method.Name;
                        if (opName.EndsWith("Async"))
                            opName = opName.Substring(0, opName.Length - 5);
                    }
                    if (string.IsNullOrWhiteSpace(opName)) continue;

                    var args = method.GetParameters();
                    if (args.Length == 0) continue; // no way of inferring anything!

                    var ret = method.ReturnType;

                    ContextKind contextKind = default;
                    MethodType methodType = default;
                    ResultKind resultKind = ResultKind.Unknown;
                    Type? from = null, to = null;

                    void Configure(ContextKind ck, MethodType mt, ResultKind rt, Type f, Type t)
                    {
                        contextKind = ck;
                        methodType = mt;
                        resultKind = rt;
                        from = f;
                        to = t;
                    }

                    // google server APIs
                    if (IsMatch(ret, args, types, typeof(IAsyncStreamReader<>), typeof(ServerCallContext), typeof(Task<>)))
                    {
                        Configure(ContextKind.ServerCallContext, MethodType.ClientStreaming, ResultKind.Task, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, typeof(IAsyncStreamReader<>), typeof(IAsyncStreamWriter<>), typeof(ServerCallContext), typeof(Task)))
                    {
                        Configure(ContextKind.ServerCallContext, MethodType.DuplexStreaming, ResultKind.Task, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(IServerStreamWriter<>), typeof(ServerCallContext), typeof(Task)))
                    {
                        Configure(ContextKind.ServerCallContext, MethodType.ServerStreaming, ResultKind.Task, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(ServerCallContext), typeof(Task<>)))
                    {
                        Configure(ContextKind.ServerCallContext, MethodType.Unary, ResultKind.Task, types[0], types[2]);
                    }

                    // google client APIs
                    else if (IsMatch(ret, args, types, null, typeof(CallOptions), typeof(AsyncUnaryCall<>)))
                    {
                        Configure(ContextKind.CallOptions, MethodType.Unary, ResultKind.Grpc, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, typeof(CallOptions), typeof(AsyncClientStreamingCall<,>)))
                    {
                        Configure(ContextKind.CallOptions, MethodType.ClientStreaming, ResultKind.Grpc, types[1], ret.GetGenericArguments()[1]);
                    }
                    else if (IsMatch(ret, args, types, typeof(CallOptions), typeof(AsyncDuplexStreamingCall<,>)))
                    {
                        Configure(ContextKind.CallOptions, MethodType.DuplexStreaming, ResultKind.Grpc, types[1], ret.GetGenericArguments()[1]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(CallOptions), typeof(AsyncServerStreamingCall<>)))
                    {
                        Configure(ContextKind.CallOptions, MethodType.ServerStreaming, ResultKind.Grpc, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(CallOptions), null))
                    {
                        Configure(ContextKind.CallOptions, MethodType.Unary, ResultKind.Sync, types[0], types[2]);
                    }


                    else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(IAsyncEnumerable<>)))
                    {
                        Configure(ContextKind.CallContext, MethodType.DuplexStreaming, ResultKind.AsyncEnumerable, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(IAsyncEnumerable<>)))
                    {
                        Configure(ContextKind.NoContext, MethodType.DuplexStreaming, ResultKind.AsyncEnumerable, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(IAsyncEnumerable<>)))
                    {
                        Configure(ContextKind.CallContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(IAsyncEnumerable<>)))
                    {
                        Configure(ContextKind.NoContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(Task<>)))
                    {
                        Configure(ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.Task, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(Task<>)))
                    {
                        Configure(ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.Task, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(ValueTask<>)))
                    {
                        Configure(ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.ValueTask, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(ValueTask<>)))
                    {
                        Configure(ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.ValueTask, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(Task<>)))
                    {
                        Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Task, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(Task<>)))
                    {
                        Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Task, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(ValueTask<>)))
                    {
                        Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(ValueTask<>)))
                    {
                        Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, types[0], types[1]);
                    }
                    else if (IsMatch(ret, args, types, null, typeof(CallContext), null))
                    {
                        Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, types[0], types[2]);
                    }
                    else if (IsMatch(ret, args, types, null, null))
                    {
                        Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, types[0], types[1]);
                    }

                    Type[] argTypes = Array.ConvertAll(args, x => x.ParameterType);
                    if (resultKind != ResultKind.Unknown && from != null && to != null)
                    {
                        ops.Add(new ContractOperation(opName, from, to, method, methodType, contextKind, resultKind, argTypes));
                    }
                }
            }
            finally
            {
                ArrayPool<Type>.Shared.Return(types);
            }
            return ops;
        }

        
        internal MethodInfo? TryGetClientHelper()
        {
            var name = GetClientHelperName();
            if (name == null || !s_reshaper.TryGetValue(name, out var method)) return null;
            return method.MakeGenericMethod(From, To);
        }
#pragma warning disable CS0618
        static readonly Dictionary<string, MethodInfo> s_reshaper =

            (from method in typeof(Reshape).GetMethods(BindingFlags.Public | BindingFlags.Static)
             where method.IsGenericMethodDefinition
             let parameters = method.GetParameters()
             where parameters[1].ParameterType == typeof(CallInvoker)
             && parameters[0].ParameterType == typeof(CallContext).MakeByRefType()
             select method).ToDictionary(x => x.Name);

        static readonly Dictionary<(MethodType, ResultKind), string> _clientReshapeMap = new Dictionary<(MethodType, ResultKind), string>
        {
            {(MethodType.DuplexStreaming, ResultKind.AsyncEnumerable), nameof(Reshape.DuplexAsync) },
            {(MethodType.ServerStreaming, ResultKind.AsyncEnumerable), nameof(Reshape.ServerStreamingAsync) },
            {(MethodType.ClientStreaming, ResultKind.Task), nameof(Reshape.ClientStreamingTaskAsync) },
            {(MethodType.ClientStreaming, ResultKind.ValueTask), nameof(Reshape.ClientStreamingValueTaskAsync) },
            {(MethodType.Unary, ResultKind.Task), nameof(Reshape.UnaryTaskAsync) },
            {(MethodType.Unary, ResultKind.ValueTask), nameof(Reshape.UnaryValueTaskAsync) },
            {(MethodType.Unary, ResultKind.Sync), nameof(Reshape.UnarySync) },
        };
#pragma warning restore CS0618
        private string? GetClientHelperName()
        {
            switch (Context)
            {
                case ContextKind.CallContext:
                case ContextKind.NoContext:
                    return _clientReshapeMap.TryGetValue((MethodType, Result), out var helper) ? helper : null;
                default:
                    return null;
            }            
        }


        internal bool IsSyncT()
        {
            return Method.ReturnType == To;
        }
        internal bool IsTaskT()
        {
            var ret = Method.ReturnType;
            return ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(Task<>)
                && ret.GetGenericArguments()[0] == To;
        }
        internal bool IsValueTaskT()
        {
            var ret = Method.ReturnType;
            return ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(ValueTask<>)
                && ret.GetGenericArguments()[0] == To;
        }
    }

    internal enum ContextKind
    {
        NoContext, // no context
        CallContext, // pb-net shared context kind
        CallOptions, // GRPC core client context kind
        ServerCallContext, // GRPC core server context kind
    }

    internal enum ResultKind
    {
        Unknown,
        Sync,
        Task,
        ValueTask,
        AsyncEnumerable,
        Grpc,
    }
}
