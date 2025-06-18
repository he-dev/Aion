namespace AionApi.Workflows;

public record WorkflowEngineOptions
{
    public string WorkflowDirectory { get; init; } = null!;

    // todo: this could be an enum
    public string WorkflowFileType { get; init; } = null!;
}