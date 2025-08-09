using System;
using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Aion.Util.Serilog;

public interface ILogEventSignature
{
    bool Matches(LogEvent logEvent);
}

// meta: This sink redirects log events into the custom logger.
public class MapsLogEvent : ILogEventSink
{
    private ConcurrentDictionary<ILogEventSignature, ILogger> Loggers { get; } = new();

    public IDisposable By(ILogEventSignature signature, ILogger to)
    {
        // core: Register the new logger.
        Loggers.GetOrAdd(signature, _ => to);

        // core: Make the caller remove it when done.
        return new RemovesLogger(() =>
        {
            // meta: Serilog recommends disposing of the logger this way.
            if (Loggers.TryRemove(signature, out var logger) && logger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        });
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
    private class RemovesLogger(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}