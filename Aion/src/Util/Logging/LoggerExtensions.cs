using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion.Util.Logging;

public static class LoggerExtensions
{
    // util: The built-in BeginScope does not support anonymous objects, thus this helper.
    public static IDisposable? BeginScopeFrom<T>(this Microsoft.Extensions.Logging.ILogger logger, T state) where T : notnull
    {
        var properties =
            from p in state.GetType().GetProperties()
            select new KeyValuePair<string, object?>(p.Name, p.GetValue(state));

        return logger.BeginScope(properties);
    }
}