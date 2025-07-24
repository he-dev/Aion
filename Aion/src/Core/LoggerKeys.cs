using System.Diagnostics.CodeAnalysis;
using Aion.Util.Serilog;
using Serilog.Events;

namespace Aion.Core;

public record WorkflowLoggerKey(string WorkflowName) : ILoggerKey<WorkflowLoggerKey>
{
    public static bool TryCreate(LogEvent logEvent, [MaybeNullWhen(false)] out WorkflowLoggerKey key)
    {
        if (logEvent.TryGetScalar<string>(nameof(WorkflowName), out var workflowName))
        {
            key = new WorkflowLoggerKey(workflowName);
            return true;
        }

        key = null;
        return false;
    }
}

public record ConsoleLoggerKey(string WorkflowName, int StepIndex) : ILoggerKey<ConsoleLoggerKey>
{
    public static bool TryCreate(LogEvent logEvent, [MaybeNullWhen(false)] out ConsoleLoggerKey key)
    {
        if (logEvent.TryGetScalar<string>(nameof(WorkflowName), out var workflowName))
        {
            if (logEvent.TryGetScalar<int>(nameof(StepIndex), out var stepIndex))
            {
                key = new ConsoleLoggerKey(workflowName, stepIndex);
                return true;
            }
        }

        key = null;
        return false;
    }
}