using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Aion.Util.Json;
using Aion.Util.Scriban;
using Serilog.Core;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace Aion.Core.Modules;

public class WorkflowSink : ILogEventSink
{
    private readonly object Lock = new();
    private readonly ConcurrentDictionary<object, ILogger?> Loggers = new();

    public IDisposable Push(string workflowName, JsonObject? configuration, IEnumerable<VariableGroup> variables)
    {
        Loggers.GetOrAdd(workflowName, _ => configuration.ToLogger(variables));
        return new Pop(() => Loggers.TryRemove(workflowName, out _));
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("WorkflowName", out var value) && value is ScalarValue { Value: string } workflowName)
        {
            if (Loggers.TryGetValue(workflowName.Value!, out var logger))
            {
                logger?.Write(logEvent);
            }
        }
    }

    private class Pop(Action pop) : IDisposable
    {
        public void Dispose() => pop();
    }
}