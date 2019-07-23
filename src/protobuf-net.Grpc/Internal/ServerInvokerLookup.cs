using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal
{
    internal static class ServerInvokerLookup
    {
        internal static int GeneralPurposeSignatureCount() => _invokers.Keys.Count(x => x.Context == ContextKind.CallContext || x.Context == ContextKind.NoContext);

        static Expression ToTaskT(Expression expression)
        {
            var type = expression.Type;
            if (type == typeof(void))
            {
                // no result from the call; add in Empty.Instance instead
                var field = Expression.Field(null, ProxyEmitter.s_Empty_InstaneTask);
                return Expression.Block(expression, field);
            }
#pragma warning disable CS0618 // Reshape
            if (type == typeof(ValueTask))
                return Expression.Call(typeof(Reshape), nameof(Reshape.EmptyValueTask), null, expression);
            if (type == typeof(Task))
                return Expression.Call(typeof(Reshape), nameof(Reshape.EmptyTask), null, expression);
#pragma warning restore CS0618

            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(Task<>))
                    return expression;
                if (type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                    return Expression.Call(expression, nameof(ValueTask<int>.AsTask), null);
            }
            return Expression.Call(typeof(Task), nameof(Task.FromResult), new Type[] { expression.Type }, expression);
        }

        internal static readonly ConstructorInfo s_CallContext_FromServerContext = typeof(CallContext).GetConstructor(new[] { typeof(object), typeof(ServerCallContext) })!;
        static Expression ToCallContext(Expression server, Expression context) => Expression.New(s_CallContext_FromServerContext, server, context);
#pragma warning disable CS0618 // Reshape
        static Expression AsAsyncEnumerable(Expression value, Expression context)
            => Expression.Call(typeof(Reshape), nameof(Reshape.AsAsyncEnumerable),
                typeArguments: value.Type.GetGenericArguments(),
                arguments: new Expression[] { value, Expression.Property(context, nameof(ServerCallContext.CancellationToken)) });

        static Expression WriteTo(Expression value, Expression writer, Expression context)
            => Expression.Call(typeof(Reshape), nameof(Reshape.WriteTo),
                typeArguments: value.Type.GetGenericArguments(),
                arguments: new Expression[] { value, writer, Expression.Property(context, nameof(ServerCallContext.CancellationToken)) });

        internal static bool TryGetValue(MethodType MethodType, ContextKind Context, ResultKind Result, VoidKind Void, out Func<MethodInfo, Expression[], Expression>? invoker)
            => _invokers.TryGetValue((MethodType, Context, Result, Void), out invoker);

#pragma warning restore CS0618

        private static readonly Dictionary<(MethodType Method, ContextKind Context, ResultKind Result, VoidKind Void), Func<MethodInfo, Expression[], Expression>?> _invokers
            = new Dictionary<(MethodType, ContextKind, ResultKind, VoidKind), Func<MethodInfo, Expression[], Expression>?>
        {
                // GRPC-style server methods are direct match; no mapping required
                // => service.{method}(args)
                { (MethodType.Unary, ContextKind.ServerCallContext, ResultKind.Task, VoidKind.None), null },
                { (MethodType.ServerStreaming, ContextKind.ServerCallContext, ResultKind.Task, VoidKind.None), null },
                { (MethodType.ClientStreaming, ContextKind.ServerCallContext, ResultKind.Task, VoidKind.None), null },
                { (MethodType.DuplexStreaming, ContextKind.ServerCallContext, ResultKind.Task, VoidKind.None), null },

                // Unary: Task<TResponse> Foo(TService service, TRequest request, ServerCallContext serverCallContext);
                // => service.{method}(request, [new CallContext(serverCallContext)])
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Task, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.ValueTask, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Sync, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Task, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.ValueTask, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Sync, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[0], args[2]))) },

                
                // Unary: Task<TResponse> Foo(TService service, TRequest request, ServerCallContext serverCallContext);
                // => service.{method}(request, [new CallContext(serverCallContext)]) return Empty.Instance;
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Task, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.ValueTask, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Sync, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Task, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.ValueTask, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Sync, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[0], args[2]))) },

                // Unary: Task<TResponse> Foo(TService service, TRequest request, ServerCallContext serverCallContext);
                // => service.{method}([new CallContext(serverCallContext)])
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Task, VoidKind.Request), (method, args) => ToTaskT(Expression.Call(args[0], method)) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.ValueTask, VoidKind.Request), (method, args) => ToTaskT(Expression.Call(args[0], method)) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Sync, VoidKind.Request), (method, args) => ToTaskT(Expression.Call(args[0], method)) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Task, VoidKind.Request), (method, args) => ToTaskT(Expression.Call(args[0], method, ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.ValueTask, VoidKind.Request), (method, args) => ToTaskT(Expression.Call(args[0], method, ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Sync, VoidKind.Request), (method, args) => ToTaskT(Expression.Call(args[0], method, ToCallContext(args[0], args[2]))) },

                
                // Unary: Task<TResponse> Foo(TService service, TRequest request, ServerCallContext serverCallContext);
                // => service.{method}([new CallContext(serverCallContext)]) return Empty.Instance;
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Task, VoidKind.Both), (method, args) => ToTaskT(Expression.Call(args[0], method)) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.ValueTask, VoidKind.Both), (method, args) => ToTaskT(Expression.Call(args[0], method)) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Sync, VoidKind.Both), (method, args) => ToTaskT(Expression.Call(args[0], method)) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Task, VoidKind.Both), (method, args) => ToTaskT(Expression.Call(args[0], method, ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.ValueTask, VoidKind.Both), (method, args) => ToTaskT(Expression.Call(args[0], method, ToCallContext(args[0], args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Sync, VoidKind.Both), (method, args) => ToTaskT(Expression.Call(args[0], method, ToCallContext(args[0], args[2]))) },

                // Client Streaming: Task<TResponse> Foo(TService service, IAsyncStreamReader<TRequest> stream, ServerCallContext serverCallContext);
                // => service.{method}(reader.AsAsyncEnumerable(serverCallContext.CancellationToken), [new CallContext(serverCallContext)])
                {(MethodType.ClientStreaming, ContextKind.NoContext, ResultKind.Task, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.NoContext, ResultKind.ValueTask, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]))) },

                {(MethodType.ClientStreaming, ContextKind.CallContext, ResultKind.Task, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]), ToCallContext(args[0], args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.CallContext, ResultKind.ValueTask, VoidKind.None), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]), ToCallContext(args[0], args[2]))) },

                {(MethodType.ClientStreaming, ContextKind.NoContext, ResultKind.Task, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.NoContext, ResultKind.ValueTask, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]))) },

                {(MethodType.ClientStreaming, ContextKind.CallContext, ResultKind.Task, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]), ToCallContext(args[0], args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.CallContext, ResultKind.ValueTask, VoidKind.Response), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]), ToCallContext(args[0], args[2]))) },

                // Server Streaming: Task Foo(TService service, TRequest request, IServerStreamWriter<TResponse> stream, ServerCallContext serverCallContext);
                // => service.{method}(request, [new CallContext(serverCallContext)]).WriteTo(stream, serverCallContext.CancellationToken)
                {(MethodType.ServerStreaming, ContextKind.NoContext, ResultKind.AsyncEnumerable, VoidKind.None), (method, args) => WriteTo(Expression.Call(args[0], method, args[1]), args[2], args[3])},
                {(MethodType.ServerStreaming, ContextKind.CallContext, ResultKind.AsyncEnumerable, VoidKind.None), (method, args) => WriteTo(Expression.Call(args[0], method, args[1], ToCallContext(args[0], args[3])), args[2], args[3])},

                {(MethodType.ServerStreaming, ContextKind.NoContext, ResultKind.AsyncEnumerable, VoidKind.Request), (method, args) => WriteTo(Expression.Call(args[0], method), args[2], args[3])},
                {(MethodType.ServerStreaming, ContextKind.CallContext, ResultKind.AsyncEnumerable, VoidKind.Request), (method, args) => WriteTo(Expression.Call(args[0], method, ToCallContext(args[0], args[3])), args[2], args[3])},

                // Duplex: Task Foo(TService service, IAsyncStreamReader<TRequest> input, IServerStreamWriter<TResponse> output, ServerCallContext serverCallContext);
                // => service.{method}(input.AsAsyncEnumerable(serverCallContext.CancellationToken), [new CallContext(serverCallContext)]).WriteTo(output, serverCallContext.CancellationToken)
                {(MethodType.DuplexStreaming, ContextKind.NoContext, ResultKind.AsyncEnumerable, VoidKind.None), (method, args) => WriteTo(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[3])), args[2], args[3]) },
                {(MethodType.DuplexStreaming, ContextKind.CallContext, ResultKind.AsyncEnumerable, VoidKind.None), (method, args) => WriteTo(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[3]), ToCallContext(args[0], args[3])), args[2], args[3]) },
        };
    }
}
