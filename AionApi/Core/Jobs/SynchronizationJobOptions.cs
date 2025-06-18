namespace AionApi.Workflows;

public record SynchronizationJobOptions
{
    public string Cron { get; init; } = null!;
}