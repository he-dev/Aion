using System;
using Aion.Util.Serilog;
using Aion.Util.Services;
using Serilog.Events;

// ReSharper disable NegativeEqualityExpression
// ReSharper disable once ConvertIfStatementToReturnStatement

namespace Aion.Core;

public record WorkflowLogEventSignature(string WorkflowName) : ILogEventSignature
{
    public bool Matches(LogEvent logEvent)
    {
        // core: The workflow logger is allowed to log only console-engine events, no std.
        if (!logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out var source)) return false;
        if (!(source == ConsoleStreamType.Engine)) return false;
        if (!logEvent.TryGetScalar<string>(nameof(WorkflowName), out var workflowName)) return false;
        if (!workflowName.Equals(WorkflowName, StringComparison.InvariantCultureIgnoreCase)) return false;

        return true;
    }
}

public record ConsoleLogEventSignature(string WorkflowName, int StepIndex) : ILogEventSignature
{
    public bool Matches(LogEvent logEvent)
    {
        if (!logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out _)) return false;
        if (!logEvent.TryGetScalar<string>(nameof(WorkflowName), out var workflowName)) return false;
        if (!workflowName.Equals(WorkflowName, StringComparison.InvariantCultureIgnoreCase)) return false;
        if (!logEvent.TryGetScalar<int>(nameof(StepIndex), out var stepIndex)) return false;
        if (!(stepIndex == StepIndex)) return false;

        return true;
    }
}