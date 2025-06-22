namespace Aion.Core.Jobs;

public record SynchronizationJobOptions
{
    public string Cron { get; init; } = null!;
}