using System;
using System.Diagnostics;
using Aion.Util;
using Aion.Util.Logging;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Serilog.Events;

// ReSharper disable NegativeEqualityExpression
// ReSharper disable once ConvertIfStatementToReturnStatement

namespace Aion.Core.Logging;


public class WorkflowSignatureScope : ILogEventSignature, IDisposable
{
    public WorkflowSignatureScope(ILogger logger)
    {
        WorkflowExecutionId = ActivitySpanId.CreateRandom();
        Scope = logger.BeginScopeFrom(new { WorkflowExecutionId });
    }

    public ActivitySpanId WorkflowExecutionId { get; init; }

    private IDisposable? Scope { get; init; }

    public bool Matches(LogEvent logEvent)
    {
        // core: The workflow logger is allowed to log only console-engine events, no std.
        if (logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out var source))
        {
            // core: The workflow logger is not allowed to log std.out and std.err events.
            if (source is ConsoleStreamType.StdOut or ConsoleStreamType.StdErr)
            {
                return false;
            }
        }

        // core: The workflow logger is allowed to log only events with the workflow-execution-ID that is the same as this one.
        if (logEvent.TryGetScalar<string>(nameof(WorkflowExecutionId), out var workflowExecutionId))
        {
            return ActivitySpanId.CreateFromString(workflowExecutionId) == WorkflowExecutionId;
        }

        return false;
    }

    public void Dispose() => Scope?.Dispose();
}

public class StepSignatureScope : ILogEventSignature, IDisposable
{
    public StepSignatureScope(ILogger logger)
    {
        StepExecutionId = ActivitySpanId.CreateRandom();
        Scope = logger.BeginScopeFrom(new { StepExecutionId });
    }

    public ActivitySpanId StepExecutionId { get; init; }

    private IDisposable? Scope { get; init; }

    public bool Matches(LogEvent logEvent)
    {
        if (!logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out _)) return false;
        if (!logEvent.TryGetScalar<string>(nameof(StepExecutionId), out var stepExecutionId)) return false;
        if (!(ActivitySpanId.CreateFromString(stepExecutionId) == StepExecutionId)) return false;

        return true;
    }

    public void Dispose() => Scope?.Dispose();
}