using System;

namespace Aion.Home.Jobs;

public record SynchronizationJobOptions : ITimeZoned
{
    public string Cron { get; init; } = null!;

    public bool Disabled { get; init; }

    public string? TimeZoneId { get; init; }

    public TimeZoneInfo TimeZone =>
        string.IsNullOrEmpty(TimeZoneId)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}