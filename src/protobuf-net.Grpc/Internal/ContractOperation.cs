using System;
using System.Collections.Generic;
using System.Reflection;
using Grpc.Core;
using System.ServiceModel;
using System.Buffers;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace ProtoBuf.Grpc.Internal
{
    internal readonly struct ContractOperation
    {
        public string Name { get; }
        public Type From { get; }
        public Type To { get; }
        public MethodInfo Method { get; }
        public MethodType MethodType { get; }
        public ContextKind Context { get; }
        public ResultKind Result { get; }
        public VoidKind Void { get; }
        public bool VoidRequest => (Void & VoidKind.Request) != 0;
        public bool VoidResponse => (Void & VoidKind.Response) != 0;

        public override string ToString() => $"{Name}: {From.Name}=>{To.Name}, {MethodType}, {Result}, {Context}";

        public ContractOperation(string name, Type from, Type to, MethodInfo method,
            MethodType methodType, ContextKind contextKind, ResultKind resultKind, VoidKind @void)
        {
            Name = name;
            From = from;
            To = to;
            Method = method;
            MethodType = methodType;
            Context = contextKind;
            Result = resultKind;
            Void = @void;
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
       
        public static bool TryIdentifySignature(MethodInfo method, out ContractOperation operation)
            => TryGetPattern(method, false, out operation);

        enum TypeCategory
        {
            None,
            Void,
            UntypedTask,
            UntypedValueTask,
            TypedTask,
            TypedValueTask,
            IAsyncEnumerable,
            IAsyncStreamReader,
            IServerStreamWriter,
            CallOptions,
            ServerCallContext,
            CallContext,
            AsyncUnaryCall,
            AsyncClientStreamingCall,
            AsyncDuplexStreamingCall,
            AsyncServerStreamingCall,
            Data,
        }
        const int RET = 3, EMPTY = 4;
        private static readonly Dictionary<(TypeCategory Arg0, TypeCategory Arg1, TypeCategory Arg2, TypeCategory Ret), (ContextKind Context, MethodType Method, ResultKind Result, VoidKind Void, int From, int To)>
            s_signaturePatterns = new Dictionary<(TypeCategory, TypeCategory, TypeCategory, TypeCategory), (ContextKind, MethodType, ResultKind, VoidKind, int, int)>
        {
                // google server APIs
                { (TypeCategory.IAsyncStreamReader, TypeCategory.ServerCallContext, TypeCategory.None, TypeCategory.TypedTask), (ContextKind.ServerCallContext, MethodType.ClientStreaming, ResultKind.Task, VoidKind.None, 0, RET) },
                { (TypeCategory.IAsyncStreamReader, TypeCategory.IServerStreamWriter, TypeCategory.ServerCallContext, TypeCategory.UntypedTask), (ContextKind.ServerCallContext, MethodType.DuplexStreaming, ResultKind.Task, VoidKind.None, 0, 1) },
                { (TypeCategory.Data, TypeCategory.IServerStreamWriter, TypeCategory.ServerCallContext, TypeCategory.UntypedTask), (ContextKind.ServerCallContext, MethodType.ServerStreaming, ResultKind.Task, VoidKind.None, 0, 1) },
                { (TypeCategory.Data, TypeCategory.ServerCallContext, TypeCategory.None, TypeCategory.TypedTask), (ContextKind.ServerCallContext, MethodType.Unary, ResultKind.Task, VoidKind.None, 0, RET) },

                // google client APIs
                { (TypeCategory.Data, TypeCategory.CallOptions, TypeCategory.None, TypeCategory.AsyncUnaryCall), (ContextKind.CallOptions, MethodType.Unary, ResultKind.Grpc, VoidKind.None, 0, RET) },
                { (TypeCategory.CallOptions, TypeCategory.None, TypeCategory.None, TypeCategory.AsyncClientStreamingCall), (ContextKind.CallOptions, MethodType.ClientStreaming, ResultKind.Grpc, VoidKind.None, RET, RET) },
                { (TypeCategory.CallOptions, TypeCategory.None, TypeCategory.None, TypeCategory.AsyncDuplexStreamingCall), (ContextKind.CallOptions, MethodType.DuplexStreaming, ResultKind.Grpc, VoidKind.None, RET, RET) },
                { (TypeCategory.Data, TypeCategory.CallOptions, TypeCategory.None, TypeCategory.AsyncServerStreamingCall), (ContextKind.CallOptions, MethodType.ServerStreaming, ResultKind.Grpc, VoidKind.None, 0, RET) },
                { (TypeCategory.Data, TypeCategory.CallOptions, TypeCategory.None, TypeCategory.Data), (ContextKind.CallOptions, MethodType.Unary, ResultKind.Sync, VoidKind.None, 0, RET) },
        };

        static TypeCategory GetCategory(Type type)
        {
            if (type == null) return TypeCategory.None;
            if (type == typeof(void)) return TypeCategory.Void;
            if (type == typeof(Task)) return TypeCategory.UntypedTask;
            if (type == typeof(ValueTask)) return TypeCategory.UntypedValueTask;
            if (type == typeof(ServerCallContext)) return TypeCategory.ServerCallContext;
            if (type == typeof(CallOptions)) return TypeCategory.CallOptions;
            if (type == typeof(CallContext)) return TypeCategory.CallContext;

            if (type.IsGenericType)
            {
                var genType = type.GetGenericTypeDefinition();
                if (genType == typeof(Task<>)) return TypeCategory.TypedTask;
                if (genType == typeof(ValueTask<>)) return TypeCategory.TypedValueTask;
                if (genType == typeof(IAsyncEnumerable<>)) return TypeCategory.IAsyncEnumerable;
                if (genType == typeof(IAsyncStreamReader<>)) return TypeCategory.IAsyncStreamReader;
                if (genType == typeof(IServerStreamWriter<>)) return TypeCategory.IServerStreamWriter;
                if (genType == typeof(AsyncUnaryCall<>)) return TypeCategory.AsyncUnaryCall;
                if (genType == typeof(AsyncClientStreamingCall<,>)) return TypeCategory.AsyncClientStreamingCall;
                if (genType == typeof(AsyncDuplexStreamingCall<,>)) return TypeCategory.AsyncDuplexStreamingCall;
                if (genType == typeof(AsyncServerStreamingCall<>)) return TypeCategory.AsyncServerStreamingCall;
            }
            // otherwise, assume data
            return TypeCategory.Data;
        }

        private static bool TryGetPattern(MethodInfo method, bool demandAttribute, out ContractOperation operation)
        {
            operation = default;

            if (method.IsGenericMethodDefinition) return false; // can't work with <T> methods

            var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
            if (demandAttribute && oca == null) return false;

            string? opName = oca?.Name;
            if (string.IsNullOrWhiteSpace(opName))
            {
                opName = method.Name;
                if (opName.EndsWith("Async"))
                    opName = opName.Substring(0, opName.Length - 5);
            }
            if (string.IsNullOrWhiteSpace(opName)) return false;

            var args = method.GetParameters();
            if (args.Length > 3) return false; // too many parameters

            (TypeCategory Arg0, TypeCategory Arg1, TypeCategory Arg2, TypeCategory Ret) signature = default;
            if (args.Length >= 1) signature.Arg0 = GetCategory(args[0].ParameterType);
            if (args.Length >= 2) signature.Arg1 = GetCategory(args[1].ParameterType);
            if (args.Length >= 3) signature.Arg2 = GetCategory(args[2].ParameterType);
            signature.Ret = GetCategory(method.ReturnType);

            if (!s_signaturePatterns.TryGetValue(signature, out var config)) return false;

            (Type type, TypeCategory category) GetTypeByIndex(int index)
            {
                switch (index)
                {
                    case 0: return (args[0].ParameterType, signature.Arg0);
                    case 1: return (args[1].ParameterType, signature.Arg1);
                    case 2: return (args[2].ParameterType, signature.Arg2);
                    case RET: return (method.ReturnType, signature.Ret);
#pragma warning disable CS0618
                    case EMPTY: return (typeof(Empty), TypeCategory.None);
#pragma warning restore CS0618
                    default: throw new IndexOutOfRangeException(nameof(index));
                }
            }
            Type GetEffectiveType((Type type, TypeCategory category) key, bool req)
            {
                var type = key.type;
                switch (key.category)
                {
                    case TypeCategory.None:
                    case TypeCategory.Data:
                    case TypeCategory.UntypedTask:
                    case TypeCategory.UntypedValueTask:
                        return type;
                    case TypeCategory.TypedTask:
                    case TypeCategory.TypedValueTask:
                    case TypeCategory.IAsyncEnumerable:
                    case TypeCategory.IAsyncStreamReader:
                    case TypeCategory.IServerStreamWriter:
                    case TypeCategory.AsyncUnaryCall:
                    case TypeCategory.AsyncServerStreamingCall:
                        return type.GetGenericArguments()[0];
                    case TypeCategory.AsyncClientStreamingCall:
                    case TypeCategory.AsyncDuplexStreamingCall:
                        return type.GetGenericArguments()[req ? 0 : 1];
                    default:
                        throw new ArgumentOutOfRangeException(key.category.ToString());
                }
            }

            var from = GetEffectiveType(GetTypeByIndex(config.From), true);
            var to = GetEffectiveType(GetTypeByIndex(config.To), false);

            operation = new ContractOperation(opName, from, to, method, config.Method, config.Context, config.Result, config.Void);
            return true;
            /*


                        else if (IsMatch(ret, args, types, typeof(void)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(CallContext), typeof(void)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(Task)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Task, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(CallContext), typeof(Task)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Task, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(ValueTask)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(CallContext), typeof(ValueTask)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(IAsyncEnumerable<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.DuplexStreaming, ResultKind.AsyncEnumerable, types[0], types[2], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(IAsyncEnumerable<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.DuplexStreaming, ResultKind.AsyncEnumerable, types[0], types[1], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(IAsyncEnumerable<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, types[0], types[2], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(IAsyncEnumerable<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, types[0], types[1], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(Task<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.Task, types[0], types[2], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(Task<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.Task, types[0], types[1], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(Task)))
                        {
                            Configure(ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.Task, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(Task)))
                        {
                            Configure(ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.Task, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(ValueTask<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.ValueTask, types[0], types[2], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(CallContext), typeof(ValueTask)))
                        {
                            Configure(ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.ValueTask, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(ValueTask<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.ValueTask, types[0], types[1], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, typeof(IAsyncEnumerable<>), typeof(ValueTask)))
                        {
                            Configure(ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.ValueTask, types[0], typeof(void), VoidKind.Response);
                        }

                        else if (IsMatch(ret, args, types, typeof(CallContext), typeof(Task<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Task, typeof(void), types[1], VoidKind.Request);
                        }
                        else if (IsMatch(ret, args, types, typeof(Task<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Task, typeof(void), types[0], VoidKind.Request);
                        }
                        else if (IsMatch(ret, args, types, typeof(CallContext), typeof(Task)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Task, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(Task)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Task, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(CallContext), typeof(ValueTask<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, typeof(void), types[1], VoidKind.Request);
                        }
                        else if (IsMatch(ret, args, types, typeof(ValueTask<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, typeof(void), types[0], VoidKind.Request);
                        }
                        else if (IsMatch(ret, args, types, typeof(CallContext), typeof(ValueTask)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, typeof(void), typeof(void), VoidKind.Both);
                        }
                        else if (IsMatch(ret, args, types, typeof(ValueTask)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, typeof(void), typeof(void), VoidKind.Both);
                        }

                        else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(Task<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Task, types[0], types[2], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(Task<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Task, types[0], types[1], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(Task)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Task, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(Task)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Task, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(ValueTask<>)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, types[0], types[2], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(ValueTask<>)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, types[0], types[1], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(ValueTask)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(ValueTask)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(CallContext), typeof(void)))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(CallContext), null))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, types[0], types[2], VoidKind.None);
                        }
                        else if (IsMatch(ret, args, types, null, typeof(void)))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, types[0], typeof(void), VoidKind.Response);
                        }
                        else if (IsMatch(ret, args, types, null))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, typeof(void), types[0], VoidKind.Request);
                        }
                        else if (IsMatch(ret, args, types, typeof(CallContext), null))
                        {
                            Configure(ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, typeof(void), types[1], VoidKind.Request);
                        }
                        else if (IsMatch(ret, args, types, null, null))
                        {
                            Configure(ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, types[0], types[1], VoidKind.None);
                        }
                        if (resultKind != ResultKind.Unknown && from != null && to != null)
                        {
                            operation = new ContractOperation(opName, from, to, method, methodType, contextKind, resultKind, @void);
                            return true;
                        }
                        return false;
                        */
        }
        public static List<ContractOperation> FindOperations(Type contractType, bool demandAttribute = false)
        {
            var all = contractType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var ops = new List<ContractOperation>(all.Length);
            foreach (var method in all)
            {
                if (TryGetPattern(method, demandAttribute, out var op))
                    ops.Add(op);
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

        static readonly Dictionary<(MethodType, ResultKind, VoidKind), string> _clientResponseMap = new Dictionary<(MethodType, ResultKind, VoidKind), string>
        {
            {(MethodType.DuplexStreaming, ResultKind.AsyncEnumerable, VoidKind.None), nameof(Reshape.DuplexAsync) },
            {(MethodType.ServerStreaming, ResultKind.AsyncEnumerable, VoidKind.None), nameof(Reshape.ServerStreamingAsync) },
            {(MethodType.ClientStreaming, ResultKind.Task, VoidKind.None), nameof(Reshape.ClientStreamingTaskAsync) },
            {(MethodType.ClientStreaming, ResultKind.Task, VoidKind.Response), nameof(Reshape.ClientStreamingTaskAsync) }, // Task<T> works as Task
            {(MethodType.ClientStreaming, ResultKind.ValueTask, VoidKind.None), nameof(Reshape.ClientStreamingValueTaskAsync) },
            {(MethodType.ClientStreaming, ResultKind.ValueTask, VoidKind.Response), nameof(Reshape.ClientStreamingValueTaskAsyncVoid) },
            {(MethodType.Unary, ResultKind.Task, VoidKind.None), nameof(Reshape.UnaryTaskAsync) },
            {(MethodType.Unary, ResultKind.Task, VoidKind.Response), nameof(Reshape.UnaryTaskAsync) }, // Task<T> works as Task
            {(MethodType.Unary, ResultKind.ValueTask, VoidKind.None), nameof(Reshape.UnaryValueTaskAsync) },
            {(MethodType.Unary, ResultKind.ValueTask, VoidKind.Response), nameof(Reshape.UnaryValueTaskAsyncVoid) },
            {(MethodType.Unary, ResultKind.Sync, VoidKind.None), nameof(Reshape.UnarySync) },
            {(MethodType.Unary, ResultKind.Sync, VoidKind.Response), nameof(Reshape.UnarySyncVoid) },
        };
#pragma warning restore CS0618
        private string? GetClientHelperName()
        {
            switch (Context)
            {
                case ContextKind.CallContext:
                case ContextKind.NoContext:
                    return _clientResponseMap.TryGetValue((MethodType, Result, Void & VoidKind.Response), out var helper) ? helper : null;
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

        internal static ISet<Type> ExpandInterfaces(Type type)
        {
            var set = type.GetInterfaces().ToHashSet();
            if (type.IsInterface) set.Add(type);
            return set;
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

    [Flags]
    internal enum VoidKind
    {
        None = 0,
        Request = 1,
        Response = 2,
        Both = Request | Response
    }
}
