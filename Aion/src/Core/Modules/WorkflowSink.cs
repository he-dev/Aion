using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Aion.Util.Json;
using Aion.Util.Scriban;
using Serilog;
using Serilog.Core;
using Serilog.Events;


namespace Aion.Core.Modules;

public class WorkflowSink : ILogEventSink
{
    private ConcurrentDictionary<object, ILogger?> Loggers { get; } = new();

    public IDisposable Push(string workflowName, JsonObject? configuration, IImmutableList<VariableGroup> variables)
    {
        Loggers.GetOrAdd(workflowName, _ => configuration.RenderPaths(variables).ToLogger());
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