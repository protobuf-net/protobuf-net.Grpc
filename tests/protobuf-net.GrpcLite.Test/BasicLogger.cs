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
        => _output?.WriteLine(string.IsNullOrWhiteSpace(_prefix) ? formatter(state, exception) : "[" + _prefix + "] " + formatter(state, exception));
    void IDisposable.Dispose() { }
}

internal static class TestExtensions
{
    public static ILogger CreateLogger(this ITestOutputHelper logger, [CallerMemberName] string prefix = "") => new BasicLogger(logger, prefix);
}
