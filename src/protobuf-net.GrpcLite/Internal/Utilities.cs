using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static class Utilities
{
    public static readonly byte[] EmptyBuffer = Array.Empty<byte>(); // static readonly field to make the JIT's life easy

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogDebug<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Debug, default, state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogInformation<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Information, default, state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Error, default, state, exception, formatter);
}
