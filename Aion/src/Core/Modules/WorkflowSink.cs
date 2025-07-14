using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Aion.Util.Json;
using Aion.Util.Scriban;
using Serilog;
using Serilog.Core;
using Serilog.Events;


namespace Aion.Core.Modules;

// meta: This custom sink redirects log events into the custom logger configured on the workflow.
public class WorkflowSink : ILogEventSink
{
    private ConcurrentDictionary<object, ILogger?> Loggers { get; } = new();

    public IDisposable Push(string workflowName, JsonObject? configuration, IImmutableList<VariableGroup> variables)
    {
        // core: Create a new logger from the workflow's configuration.
        Loggers.GetOrAdd(workflowName, _ => configuration.RenderPaths(variables).ToLogger());

        // core: Make the caller remove it when done.
        return new Pop(() => Loggers.TryRemove(workflowName, out _));
    }

    public void Emit(LogEvent logEvent)
    {
        // core: Redirect log events to the custom logger when the WorkflowName property exists.
        if (logEvent.Properties.TryGetValue("WorkflowName", out var value) && value is ScalarValue { Value: string } workflowName)
        {
            if (Loggers.TryGetValue(workflowName.Value!, out var logger))
            {
                logger?.Write(logEvent);
            }
        }
    }

    // meta: Discard the logger as it's no logger necessary.
    private class Pop(Action pop) : IDisposable
    {
        public void Dispose() => pop();
    }
}