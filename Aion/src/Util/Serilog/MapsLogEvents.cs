using System;
using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Aion.Util.Serilog;

public interface ILogEventSignature
{
    public bool Matches(LogEvent logEvent);
}

// meta: This sink redirects log events into the custom logger.
public class MapsLogEvents : ILogEventSink
{
    private ConcurrentDictionary<ILogEventSignature, ILogger> Loggers { get; } = new();

    public Pop By(ILogEventSignature key, ILogger to)
    {
        // core: Register the new logger.
        Loggers.GetOrAdd(key, _ => to);

        // core: Make the caller remove it when done.
        return new Pop(() => Loggers.TryRemove(key, out _), to);
    }

    public void Emit(LogEvent logEvent)
    {
        foreach (var (signature, logger) in Loggers)
        {
            // core: Redirect log events to the custom logger when the signature matches.
            if (signature.Matches(logEvent))
            {
                logger.Write(logEvent);
            }
        }
    }

    // meta: Discard the logger as it's no logger necessary.
    public class Pop(Action pop, ILogger logger) : IDisposable
    {
        // util: Expose the logger back to the caller for convenience to avoids helper variables.
        public ILogger Logger => logger;

        public void Dispose() => pop();
    }
}