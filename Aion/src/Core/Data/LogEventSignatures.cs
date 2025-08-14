using System;
using System.Diagnostics;
using Aion.Util.Flow;
using Aion.Util.Flow.Serilog;
using Aion.Util.Logging;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Serilog.Events;

// ReSharper disable NegativeEqualityExpression
// ReSharper disable once ConvertIfStatementToReturnStatement

namespace Aion.Core.Data;

public class ProfileLogEventSignature : ILogEventSignature
{
    private ProfileLogEventSignature() { }

    public ActivitySpanId SpanId { get; } = Activity.Current?.SpanId ?? throw new InvalidOperationException("Activity.Current is null.");

    public bool Matches(LogEvent logEvent)
    {
        // core: The profile logger is allowed to log only console-engine events, no std.
        if (!logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out var source)) return false;
        if (!(source == ConsoleStreamType.Engine)) return false;
        if (!logEvent.TryGetScalar<ActivitySpanId>(nameof(SpanId), out var spanId)) return false;
        if (!(spanId == SpanId)) return false;

        return true;
    }

    public static ILogEventSignature FromScope() => new ProfileLogEventSignature();
}

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
        if (!logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out var source)) return false;
        if (!(source == ConsoleStreamType.Engine)) return false;
        if (!logEvent.TryGetScalar<string>(nameof(WorkflowExecutionId), out var workflowExecutionId)) return false;
        if (!(ActivitySpanId.CreateFromString(workflowExecutionId) == WorkflowExecutionId)) return false;

        return true;
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