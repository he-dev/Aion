using System;

namespace Aion.Util.Services.Synchronizations;

// util: This class supports the API response.
public record SynchronizeWorkflowResult(string WorkflowName, Type ActionType)
{
    public DateTimeOffset? NextUtc { get; init; }

    public Exception? Exception { get; init; }
}

public record SynchronizeWorkflowResult<T>(string WorkflowName)
    : SynchronizeWorkflowResult(WorkflowName, typeof(T)) where T : ISynchronizeWorkflow;