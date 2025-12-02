using System;
using System.Collections.Generic;

namespace Aion.Core.Options;

public record SchedulerOptions
{
    public string Name { get; init; } = null!;

    public Dictionary<string, string> Variables { get; init; } = new();

    public Dictionary<string, Profile> Profiles { get; init; } = null!;
}

public class ProfileNotFoundException(string profileName) : Exception($"Profile '{profileName}' not found.");
