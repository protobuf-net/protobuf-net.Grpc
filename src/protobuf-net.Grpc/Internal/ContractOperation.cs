using System;
using System.Collections.Generic;
using System.Reflection;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
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
        public MethodType MethodType { get; }
        public ContextKind Context { get; }
        public ResultKind Result { get; }
        public VoidKind Void { get; }
        public bool VoidRequest => (Void & VoidKind.Request) != 0;
        public bool VoidResponse => (Void & VoidKind.Response) != 0;

        public override string ToString() => $"{Name}: {From.Name}=>{To.Name}, {MethodType}, {Result}, {Context}, {Void}";

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

        public static bool TryIdentifySignature(MethodInfo method, ServiceBinder binder, out ContractOperation operation)
            => TryGetPattern(method, binder, out operation);

        internal enum TypeCategory
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

        const int RET = -1, VOID = -2;
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

                // unary parameterless, with or without a return value
                { (TypeCategory.None,TypeCategory.None, TypeCategory.None, TypeCategory.Void), (ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, VoidKind.Both, VOID, VOID)},
                { (TypeCategory.None,TypeCategory.None, TypeCategory.None, TypeCategory.Data), (ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, VoidKind.Request, VOID, RET)},
                { (TypeCategory.None,TypeCategory.None, TypeCategory.None, TypeCategory.UntypedTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.Task, VoidKind.Both, VOID, VOID) },
                { (TypeCategory.None,TypeCategory.None, TypeCategory.None, TypeCategory.UntypedValueTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.Both, VOID, VOID) },
                { (TypeCategory.None,TypeCategory.None, TypeCategory.None, TypeCategory.TypedTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.Task, VoidKind.Request, VOID, RET) },
                { (TypeCategory.None,TypeCategory.None, TypeCategory.None, TypeCategory.TypedValueTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.Request, VOID, RET) },

                { (TypeCategory.CallContext,TypeCategory.None, TypeCategory.None, TypeCategory.Void), (ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, VoidKind.Both,VOID, VOID)},
                { (TypeCategory.CallContext,TypeCategory.None, TypeCategory.None, TypeCategory.Data), (ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, VoidKind.Request, VOID, RET)},
                { (TypeCategory.CallContext,TypeCategory.None, TypeCategory.None, TypeCategory.UntypedTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.Task, VoidKind.Both, VOID, VOID) },
                { (TypeCategory.CallContext,TypeCategory.None, TypeCategory.None, TypeCategory.UntypedValueTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.Both,VOID, VOID) },
                { (TypeCategory.CallContext,TypeCategory.None, TypeCategory.None, TypeCategory.TypedTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.Task, VoidKind.Request, VOID, RET) },
                { (TypeCategory.CallContext,TypeCategory.None, TypeCategory.None, TypeCategory.TypedValueTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.Request, VOID, RET) },

                // unary with parameter, with or without a return value
                { (TypeCategory.Data, TypeCategory.None,TypeCategory.None, TypeCategory.Void), (ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, VoidKind.Response, 0, VOID)},
                { (TypeCategory.Data, TypeCategory.None,TypeCategory.None, TypeCategory.Data), (ContextKind.NoContext, MethodType.Unary, ResultKind.Sync, VoidKind.None, 0, RET)},
                { (TypeCategory.Data, TypeCategory.None,TypeCategory.None, TypeCategory.UntypedTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.Task, VoidKind.Response, 0, VOID) },
                { (TypeCategory.Data, TypeCategory.None,TypeCategory.None, TypeCategory.UntypedValueTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.Response, 0, VOID) },
                { (TypeCategory.Data, TypeCategory.None,TypeCategory.None, TypeCategory.TypedTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.Task, VoidKind.None, 0, RET) },
                { (TypeCategory.Data, TypeCategory.None,TypeCategory.None, TypeCategory.TypedValueTask), (ContextKind.NoContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.None, 0, RET) },

                { (TypeCategory.Data, TypeCategory.CallContext,TypeCategory.None, TypeCategory.Void), (ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, VoidKind.Response,0, VOID)},
                { (TypeCategory.Data, TypeCategory.CallContext,TypeCategory.None, TypeCategory.Data), (ContextKind.CallContext, MethodType.Unary, ResultKind.Sync, VoidKind.None, 0, RET)},
                { (TypeCategory.Data, TypeCategory.CallContext,TypeCategory.None, TypeCategory.UntypedTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.Task, VoidKind.Response, 0, VOID) },
                { (TypeCategory.Data, TypeCategory.CallContext,TypeCategory.None, TypeCategory.UntypedValueTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.Response,0, VOID) },
                { (TypeCategory.Data, TypeCategory.CallContext,TypeCategory.None, TypeCategory.TypedTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.Task, VoidKind.None, 0, RET) },
                { (TypeCategory.Data, TypeCategory.CallContext,TypeCategory.None, TypeCategory.TypedValueTask), (ContextKind.CallContext, MethodType.Unary, ResultKind.ValueTask, VoidKind.None, 0, RET) },

                // client streaming
                { (TypeCategory.IAsyncEnumerable, TypeCategory.None, TypeCategory.None, TypeCategory.TypedValueTask), (ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.ValueTask, VoidKind.None, 0, RET) },
                { (TypeCategory.IAsyncEnumerable, TypeCategory.None, TypeCategory.None, TypeCategory.UntypedValueTask), (ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.ValueTask, VoidKind.Response, 0, VOID) },
                { (TypeCategory.IAsyncEnumerable, TypeCategory.None, TypeCategory.None, TypeCategory.TypedTask), (ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.Task, VoidKind.None, 0, RET) },
                { (TypeCategory.IAsyncEnumerable, TypeCategory.None, TypeCategory.None, TypeCategory.UntypedTask), (ContextKind.NoContext, MethodType.ClientStreaming, ResultKind.Task, VoidKind.Response, 0, VOID) },

                { (TypeCategory.IAsyncEnumerable, TypeCategory.CallContext, TypeCategory.None, TypeCategory.TypedValueTask), (ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.ValueTask, VoidKind.None, 0, RET) },
                { (TypeCategory.IAsyncEnumerable, TypeCategory.CallContext, TypeCategory.None, TypeCategory.UntypedValueTask), (ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.ValueTask, VoidKind.Response, 0, VOID) },
                { (TypeCategory.IAsyncEnumerable, TypeCategory.CallContext, TypeCategory.None, TypeCategory.TypedTask), (ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.Task, VoidKind.None, 0, RET) },
                { (TypeCategory.IAsyncEnumerable, TypeCategory.CallContext, TypeCategory.None, TypeCategory.UntypedTask), (ContextKind.CallContext, MethodType.ClientStreaming, ResultKind.Task, VoidKind.Response, 0, VOID) },

                // server streaming
                { (TypeCategory.None, TypeCategory.None, TypeCategory.None, TypeCategory.IAsyncEnumerable), (ContextKind.NoContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, VoidKind.Request, VOID, RET) },
                { (TypeCategory.CallContext, TypeCategory.None, TypeCategory.None, TypeCategory.IAsyncEnumerable), (ContextKind.CallContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, VoidKind.Request, VOID, RET) },
                { (TypeCategory.Data, TypeCategory.None, TypeCategory.None, TypeCategory.IAsyncEnumerable), (ContextKind.NoContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, VoidKind.None, 0, RET) },
                { (TypeCategory.Data, TypeCategory.CallContext, TypeCategory.None, TypeCategory.IAsyncEnumerable), (ContextKind.CallContext, MethodType.ServerStreaming, ResultKind.AsyncEnumerable, VoidKind.None, 0, RET) },

                // duplex
                { (TypeCategory.IAsyncEnumerable, TypeCategory.None, TypeCategory.None, TypeCategory.IAsyncEnumerable), (ContextKind.NoContext, MethodType.DuplexStreaming, ResultKind.AsyncEnumerable, VoidKind.None, 0, RET) },
                { (TypeCategory.IAsyncEnumerable, TypeCategory.CallContext, TypeCategory.None, TypeCategory.IAsyncEnumerable), (ContextKind.CallContext, MethodType.DuplexStreaming, ResultKind.AsyncEnumerable, VoidKind.None, 0, RET) },
        };
        internal static int SignatureCount => s_signaturePatterns.Count;

        internal static int GeneralPurposeSignatureCount() => s_signaturePatterns.Values.Count(x => x.Context == ContextKind.CallContext || x.Context == ContextKind.NoContext);

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

            if (typeof(Delegate).IsAssignableFrom(type)) return TypeCategory.None; // yeah, that's not going to happen
            // otherwise, assume data
            return TypeCategory.Data;
        }

        internal static (TypeCategory Arg0, TypeCategory Arg1, TypeCategory Arg2, TypeCategory Ret) GetSignature(MethodInfo method)
            => GetSignature(method.GetParameters(), method.ReturnType);

        private static (TypeCategory Arg0, TypeCategory Arg1, TypeCategory Arg2, TypeCategory Ret) GetSignature(ParameterInfo[] args, Type returnType)
        {
            (TypeCategory Arg0, TypeCategory Arg1, TypeCategory Arg2, TypeCategory Ret) signature = default;
            if (args.Length >= 1) signature.Arg0 = GetCategory(args[0].ParameterType);
            if (args.Length >= 2) signature.Arg1 = GetCategory(args[1].ParameterType);
            if (args.Length >= 3) signature.Arg2 = GetCategory(args[2].ParameterType);
            signature.Ret = GetCategory(returnType);
            return signature;
        }
        private static bool TryGetPattern(MethodInfo method, ServiceBinder binder, out ContractOperation operation)
        {
            operation = default;

            if (method.IsGenericMethodDefinition) return false; // can't work with <T> methods

            if ((method.Attributes & (MethodAttributes.SpecialName)) != 0) return false; // some kind of accessor etc

            if (!binder.IsOperationContract(method, out var opName)) return false;

            var args = method.GetParameters();
            if (args.Length > 3) return false; // too many parameters

            var signature = GetSignature(args, method.ReturnType);

            if (!s_signaturePatterns.TryGetValue(signature, out var config)) return false;

            (Type type, TypeCategory category) GetTypeByIndex(int index)
            {
                switch (index)
                {
                    case 0: return (args[0].ParameterType, signature.Arg0);
                    case 1: return (args[1].ParameterType, signature.Arg1);
                    case 2: return (args[2].ParameterType, signature.Arg2);
                    case RET: return (method.ReturnType, signature.Ret);
                    case VOID: return (typeof(void), TypeCategory.Void);
                    default: throw new IndexOutOfRangeException(nameof(index));
                }
            }

            static Type GetDataType((Type type, TypeCategory category) key, bool req)
            {
                var type = key.type;
                switch (key.category)
                {
                    case TypeCategory.Data:
                        return type;
                    case TypeCategory.Void:
                    case TypeCategory.UntypedTask:
                    case TypeCategory.UntypedValueTask:
#pragma warning disable CS0618 // Empty
                        return typeof(Empty);
#pragma warning restore CS0618
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

            var from = GetDataType(GetTypeByIndex(config.From), true);
            var to = GetDataType(GetTypeByIndex(config.To), false);

            operation = new ContractOperation(opName, from, to, method, config.Method, config.Context, config.Result, config.Void);
            return true;
        }
        public static List<ContractOperation> FindOperations(ServiceBinder binder, Type contractType)
        {
            var all = contractType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var ops = new List<ContractOperation>(all.Length);
            foreach (var method in all)
            {
                if (TryGetPattern(method, binder, out var op))
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
#pragma warning disable CS0618 // Reshape
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
            var set = new HashSet<Type>(type.GetInterfaces());
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
