using System.Collections.Generic;
using Aion.Util.Serilog;
using Serilog.Events;

namespace Aion.Core;

public record WorkflowLogEventSignature(string WorkflowName) : ILogEventSignature
{
    public bool Matches(LogEvent logEvent) => logEvent.Matches(new Dictionary<string, object>
    {
        [nameof(WorkflowName)] = WorkflowName
    });
}

public record ConsoleLogEventSignature(string WorkflowName, int StepIndex) : ILogEventSignature
{
    public bool Matches(LogEvent logEvent) => logEvent.Matches(new Dictionary<string, object>
    {
        [nameof(WorkflowName)] = WorkflowName,
        [nameof(StepIndex)] = StepIndex
    });
}