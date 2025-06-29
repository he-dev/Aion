namespace Aion.Core.Maintenance;

public record MaintenanceTokenOptions
{
    public string PendingPath { get; init; } = null!;

    public string? ExpiredPath { get; init; }
};