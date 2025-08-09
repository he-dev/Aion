using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Aion.Core;

public record InstanceOptions
{
    public const string SectionName = "Instance";

    public string Name { get; init; } = null!;

    //public bool SyncOn { get; init; }

    public Dictionary<string, object?> Variables { get; internal init; } = new();

    public Profile[] Profiles { get; init; } = null!;

    public Profile this[string name]
    {
        get { return Profiles.SingleOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new ProfileNotFoundException(name); }
    }
}

public class ProfileNotFoundException(string profileName) : Exception($"Profile '{profileName}' not found.");