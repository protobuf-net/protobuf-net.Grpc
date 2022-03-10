using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static class Utilities
{
    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogDebug<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Debug, default(EventId), state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogInformation<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Information, default(EventId), state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Error, default(EventId), state, exception, formatter);
}
