using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Server;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ProtoBuf.Grpc.Lite
{
    public abstract class StreamServer
    {
        public StreamServer(ILogger? logger)
            => Logger = logger;

        internal readonly ILogger? Logger;

        private int id = -1;
        internal int NextId() => Interlocked.Increment(ref id);
        internal StreamServerConnection AddConnection(Stream input, Stream output, CancellationToken cancellationToken)
            => new StreamServerConnection(this, input, output, cancellationToken);

        private StreamServiceBinder? _serviceBinder;
        public void ManualBind<T>(T? server = null) where T : class
        {
            var binder = typeof(T).GetCustomAttribute<BindServiceMethodAttribute>(true);
            if (binder is null) throw new InvalidOperationException("No " + nameof(BindServiceMethodAttribute) + " found");
            if (binder.BindType is null) throw new InvalidOperationException("No " + nameof(BindServiceMethodAttribute) + "." + nameof(BindServiceMethodAttribute.BindType) + " found");

            var method = binder.BindType.FindMembers(MemberTypes.Method, BindingFlags.Public | BindingFlags.Static,
                static (member, state) =>
                {
                    if (member is not MethodInfo method) return false;
                    if (method.Name != (string)state!) return false;

                    if (method.ReturnType != typeof(void)) return false;
                    var args = method.GetParameters();
                    if (args.Length != 2) return false;
                    if (args[0].ParameterType != typeof(ServiceBinderBase)) return false;
                    if (!args[1].ParameterType.IsAssignableFrom(typeof(T))) return false;
                    return true;

                }, binder.BindMethodName).OfType<MethodInfo>().SingleOrDefault();
            if (method is null) throw new InvalidOperationException("No suitable " + binder.BindType.Name + "." + binder.BindMethodName + " method found");

            server ??= Activator.CreateInstance<T>();
            _serviceBinder ??= new StreamServiceBinder(this);
            method.Invoke(null, new object[] { _serviceBinder, server });
        }

        public int MethodCount => _handlers.Count;

        readonly ConcurrentDictionary<string, Func<IHandler>> _handlers = new ConcurrentDictionary<string, Func<IHandler>>();
        internal void AddHandler(string fullName, Func<IHandler> handlerFactory)
        {
            if (!_handlers.TryAdd(fullName, handlerFactory)) ThrowDuplicate(fullName);
            static void ThrowDuplicate(string fullName) => throw new ArgumentException($"The method '{fullName}' already exists", nameof(fullName));
        }
        internal bool TryGetHandler(string fullName,
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
            [MaybeNullWhen(false)]
#endif
            out Func<IHandler> handlerFactory)
            => _handlers.TryGetValue(fullName, out handlerFactory);
    }
}
