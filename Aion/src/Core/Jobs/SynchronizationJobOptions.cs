using System;
using Aion.Head;

namespace Aion.Core.Jobs;

public record SynchronizationJobOptions : ITimeZoned
{
    public string Cron { get; init; } = null!;

    public string? TimeZoneId { get; init; }

    public TimeZoneInfo TimeZone =>
        string.IsNullOrEmpty(TimeZoneId)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}