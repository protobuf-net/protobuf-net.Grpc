using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static class Logging
{
    [Conditional("DEBUG")]
    public static void SetSource(object source) => SetSource(source?.ToString());

    [Conditional("DEBUG")]
    public static void SetSource(string? source)
    {
#if DEBUG
        s_source.Value = string.IsNullOrWhiteSpace(source) ? "" : source.Trim();
#endif
    }
#if DEBUG
    private static AsyncLocal<string> s_source = new();
    public static string? Source => s_source.Value;

    public const string ClientPrefix = "C:", ServerPrefix = "S:";
#else
    public static string Source => "";
#endif

    private static void LogWithPrefix<TState>(this ILogger logger, LogLevel level, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
#if DEBUG
            if (!string.IsNullOrEmpty(Source))
            {
                logger.Log(level, default, (state, formatter), exception, static (state, ex) => "[" + Source + "] " + state.formatter(state.state, ex));
                return;
            }
#endif
            logger.Log<TState>(level, default, state, exception, formatter);
        }
        catch { } // let's just never throw while logging, eh?
    }

    [Conditional("DEBUG")]
    public static void DebugWriteLine(string message)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(message, Source);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogWithPrefix(this ILogger logger, LogLevel level, string message)
        => logger.LogWithPrefix<string>(level, message, null, static (state, _) => state);

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.LogWithPrefix(LogLevel.Debug, state, exception, formatter);

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug(this ILogger? logger, string message)
        => logger?.LogWithPrefix(LogLevel.Debug, message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Information<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.LogWithPrefix<TState>(LogLevel.Information, state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining), Obsolete("needs implementation", error: false)]
    public static void ThrowNotImplemented(this ILogger? logger, [CallerMemberName] string caller = "")
    {
        logger.Critical(caller, static (state, _) => $"{state} is not implemented");
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Information(this ILogger? logger, string message)
        => logger?.LogWithPrefix(LogLevel.Information, message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.LogWithPrefix<TState>(LogLevel.Error, state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(this ILogger? logger, Exception exception, [CallerMemberName] string caller = "")
        => logger?.LogWithPrefix<string>(LogLevel.Error, caller, exception, static (state, ex) => $"[{state}]: {ex!.Message}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Critical(this ILogger? logger, Exception exception, [CallerMemberName] string caller = "")
        => logger?.LogWithPrefix<string>(LogLevel.Critical, caller, exception, static (state, ex) => $"[{state}]: {ex!.Message}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Critical(this ILogger? logger, string message)
            => logger?.LogWithPrefix(LogLevel.Information, message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Critical<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.LogWithPrefix<TState>(LogLevel.Critical, state, exception, formatter);
}
