using System;
using Aion.Util;
using Aion.Util.Serilog;
using Serilog.Events;

namespace Aion.Core;

public record WorkflowLogEventSignature(string WorkflowName) : ILogEventSignature
{
    // ReSharper disable once ConvertIfStatementToReturnStatement
    public bool Matches(LogEvent logEvent)
    {
        if (!logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out var source))
        {
            return false;
        }

        if (source != ConsoleStreamType.Engine)
        {
            return false;
        }

        if (!logEvent.TryGetScalar<string>(nameof(WorkflowName), out var workflowName) || !workflowName.Equals(WorkflowName, StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

public record ConsoleLogEventSignature(string WorkflowName, int StepIndex) : ILogEventSignature
{
    // ReSharper disable once ConvertIfStatementToReturnStatement
    public bool Matches(LogEvent logEvent)
    {
        if (!logEvent.TryGetScalar<ConsoleStreamType>(nameof(ConsoleStreamType), out var consoleStreamType))
        {
            return false;
        }

        if (consoleStreamType == ConsoleStreamType.Engine)
        {
            //return false;
        }

        if (!logEvent.TryGetScalar<string>(nameof(WorkflowName), out var workflowName))
        {
            return false;
        }

        if (!workflowName.Equals(WorkflowName, StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        if (!logEvent.TryGetScalar<int>(nameof(StepIndex), out var stepIndex))
        {
            return false;
        }

        if (stepIndex != StepIndex)
        {
            return false;
        }

        return true;
    }
}