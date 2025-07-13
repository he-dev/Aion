namespace Aion.Home.Jobs;

public record SynchronizationJobOptions
{
    public string Cron { get; init; } = null!;

    public bool IsOn { get; init; } = true;
}