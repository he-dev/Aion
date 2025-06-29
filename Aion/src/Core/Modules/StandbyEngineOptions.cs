namespace Aion.Core.Modules;

public record StandbyEngineOptions
{
    public string PendingPath { get; init; } = null!;

    public string ExpiredPath { get; init; } = string.Empty;
}