using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace Aion.Util.Serilog;

public static class LoggerExtensions
{
    // role: The built-in BeginScope does not support anonymous objects, thus this helper.
    public static IDisposable? BeginScopeFrom<T>(this Microsoft.Extensions.Logging.ILogger logger, T state) where T : notnull
    {
        var properties =
            from p in state.GetType().GetProperties()
            select new KeyValuePair<string, object?>(p.Name, p.GetValue(state));

        return logger.BeginScope(properties);
    }

    // role: We want to use the same ILogger API everywhere, so use the ILoggerFactory.
    public static ILoggerFactory ToLoggerFactory(this global::Serilog.ILogger? logger)
    {
        // util: Wraps the Serilog's logger into Microsoft's logger.
        return
            logger is null
                ? NullLoggerFactory.Instance
                : LoggerFactory.Create(builder => { builder.ClearProviders().AddSerilog(logger, dispose: true); });
    }
}