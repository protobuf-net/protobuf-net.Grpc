using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using ProtoBuf.Grpc.Lite.Internal.Server;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ProtoBuf.Grpc.Lite
{
    public sealed class LiteServer : IDisposable, IAsyncDisposable
    {
        public LiteServer(ILogger? logger = null)
            => Logger = logger;

        internal readonly ILogger? Logger;

        private int id = -1;
        internal int NextStreamId() => Interlocked.Increment(ref id);

        internal CancellationToken ServerShutdown => _serverShutdown.Token;

        CancellationTokenSource _serverShutdown = new CancellationTokenSource();
        public void Stop() => _serverShutdown.Cancel();

        void IDisposable.Dispose() => Stop();
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            Stop();
            return default;
        }

        public LiteChannel CreateLocalClient(string? name = null)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "(local)";
            NullConnection.CreateLinkedPair(out var x, out var y);
            var server = new LiteConnection(this, x, Logger);
            var client = new LiteChannel(y, name, Logger);
            server.StartWorker();
            return client;
        }

        public Task ListenAsync(Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> listener)
            => Task.Run(() => ListenAsyncCore(listener));

        public Task ListenAsync(Func<CancellationToken, ValueTask<ConnectionState<Stream>>> listener)
            => ListenAsync(listener.AsFrames());

        private async Task ListenAsyncCore(Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> listener)
        {
            Logger.SetSource(LogKind.Server, "listener");
            Logger.Debug("starting listener (accepts incoming connections)");
            try
            {
                while (!_serverShutdown.IsCancellationRequested)
                {
                    await Task.Yield(); // let's not hog a core if we have lots of connections...
                    Logger.Information("listening for new connection...");
                    try
                    {
                        var connection = await listener(ServerShutdown);
                        if (connection is null)
                        {
                            continue;
                        }

                        Logger.Information(connection, static (state, _) => $"established connection {state.Name}");
                        var server = new LiteConnection(this, connection.Value, connection.Logger);
                        server.StartWorker();
                    }
                    catch(Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == _serverShutdown.Token)
            { } // that's success
        }

        private LiteServiceBinder? _serviceBinder;
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
            _serviceBinder ??= new LiteServiceBinder(this);
            var countBefore = MethodCount;
            method.Invoke(null, new object[] { _serviceBinder, server });
            var methodsAdded = MethodCount - countBefore;
            Logger.Information((type: typeof(T), methodsAdded), static (state, ex) => $"bound {state.type.FullName}, {state.methodsAdded} methods added");
        }

        public int MethodCount => _handlers.Count;

        private readonly ConcurrentDictionary<string, Func<IServerStream>> _handlers = new ConcurrentDictionary<string, Func<IServerStream>>();
        internal void AddHandler(string fullName, Func<IServerStream> handlerFactory)
        {
            if (!_handlers.TryAdd(fullName, handlerFactory)) ThrowDuplicate(fullName);
            static void ThrowDuplicate(string fullName) => throw new ArgumentException($"The method '{fullName}' already exists", nameof(fullName));
        }
        internal bool TryGetHandler(string fullName, [MaybeNullWhen(false)] out IServerStream handler)
        {
            handler = _handlers.TryGetValue(fullName, out var factory) ? factory?.Invoke() : null;
            return handler is not null;
        }
    }
}
