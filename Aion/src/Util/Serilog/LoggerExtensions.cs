using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Serilog;

public static class LoggerExtensions
{
    // public static IDisposable? BeginScopeFrom<T>(this ILogger logger, T state, string scopeName = "Scope") where T : notnull
    // {
    //     var scope =
    //         from p in state.GetType().GetProperties()
    //         select new KeyValuePair<string, object?>(p.Name, p.GetValue(state));
    //
    //     return logger.BeginScope(new Dictionary<string, object?>
    //     {
    //         [scopeName] = scope.ToDictionary()
    //     });
    // }

    public static IDisposable? BeginScopeFrom<T>(this ILogger logger, T state) where T : notnull
    {
        var properties =
            from p in state.GetType().GetProperties()
            select new KeyValuePair<string, object?>(p.Name, p.GetValue(state));

        return logger.BeginScope(properties);
    }
}

public enum ProcessFlow
{
    Started,
    Running,
    Completed,
    Canceled,
    Faulted,
}