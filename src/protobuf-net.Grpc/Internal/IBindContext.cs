namespace ProtoBuf.Grpc.Internal
{
    internal interface IBindContext
    {
        void LogWarning(string message, params object?[]? args);
        void LogError(string message, params object?[]? args);
    }
}
