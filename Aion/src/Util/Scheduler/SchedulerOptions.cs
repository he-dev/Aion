using System;
using System.Collections.Generic;

namespace Aion.Util.Scheduler;

public record SchedulerOptions
{
    public string Name { get; init; } = null!;

    public TimeSpan StartDelay { get; init; }

    public Dictionary<string, string>? Parameters { get; init; } = new();

    public Dictionary<string, Profile> Profiles { get; init; } = null!;
}

public class ProfileNotFoundException(string profileName) : Exception($"Profile '{profileName}' not found.");
