using System;
using System.Text.Json.Serialization;
using Aion.Core.Modules;
using Aion.Core.Util;

namespace Aion.Core.Maintenance;

public record MaintenanceToken
{
    public string Filter { get; init; } = null!;

    public DateTimeOffset StartsOnUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresOnUtc { get; init; }
    public DateTimeOffset CreatedOnUtc { get; init; } = DateTimeOffset.UtcNow;

    public TimeSpan Length => ExpiresOnUtc - StartsOnUtc;

    [JsonIgnore]
    public TimeSpan Remaining => ExpiresOnUtc - DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsActive => StartsOnUtc <= DateTimeOffset.UtcNow && ExpiresOnUtc > DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsExpired => ExpiresOnUtc < DateTimeOffset.UtcNow;

}