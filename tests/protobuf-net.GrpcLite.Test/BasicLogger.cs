using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace protobuf_net.GrpcLite.Test;

sealed class BasicLogger : ILogger, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _prefix;

    public BasicLogger(ITestOutputHelper output, [CallerMemberName] string prefix = "")
    {
        _output = output;
        _prefix = prefix;
    }

    IDisposable ILogger.BeginScope<TState>(TState state) => null!;

    bool ILogger.IsEnabled(LogLevel logLevel) => _output is not null;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _output?.WriteLine(Format<TState>(logLevel, eventId, state, exception, formatter, _prefix));
    void IDisposable.Dispose() { }

    public static string Format<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string prefix)
        => Padded(logLevel) + (string.IsNullOrWhiteSpace(prefix) ? formatter(state, exception) : "[" + prefix + "] " + formatter(state, exception));

    private static string Padded(LogLevel level)
    {
        switch (level)
        {   // for alignment purposes
            case LogLevel.Trace:
                return "trace : ";
            case LogLevel.Debug:
                return "debug : ";
            case LogLevel.Information:
                return "info  : ";
            case LogLevel.Warning:
                return "warn  : ";
            case LogLevel.Error:
                return "ERROR : ";
            case LogLevel.Critical:
                return "CRIT  : ";
            default: return level.ToString() + " : ";
        }
    }
}

internal static class TestExtensions
{
    public static ILogger CreateLogger(this ITestOutputHelper logger, [CallerMemberName] string prefix = "") => new BasicLogger(logger, prefix);
}
