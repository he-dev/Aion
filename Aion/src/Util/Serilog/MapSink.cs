using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Aion.Util.Serilog;

public interface ILoggerKey<TKey> where TKey : ILoggerKey<TKey>
{
    public static abstract bool TryCreate(LogEvent logEvent, [MaybeNullWhen(false)] out TKey key);
}

// meta: This sink redirects log events into the custom logger.
public class MapSink<TKey> : ILogEventSink where TKey : ILoggerKey<TKey>
{
    private ConcurrentDictionary<TKey, ILogger> Loggers { get; } = new();

    public IDisposable Push(TKey key, ILogger logger)
    {
        // core: Register the new logger.
        Loggers.GetOrAdd(key, _ => logger);

        // core: Make the caller remove it when done.
        return new Pop(() => Loggers.TryRemove(key, out _));
    }

    public void Emit(LogEvent logEvent)
    {
        // core: Redirect log events to the custom logger when the key matches.
        if (TKey.TryCreate(logEvent, out var key) && Loggers.TryGetValue(key, out var logger))
        {
            logger.Write(logEvent);
        }
    }

    // meta: Discard the logger as it's no logger necessary.
    private class Pop(Action pop) : IDisposable
    {
        public void Dispose() => pop();
    }
}