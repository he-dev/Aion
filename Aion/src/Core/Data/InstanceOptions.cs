using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion.Core.Data;

public record InstanceOptions
{
    public const string SectionName = "Instance";

    public string Name { get; init; } = null!;

    public Dictionary<string, string> Variables { get; init; } = new();

    public Profile[] Profiles { get; init; } = null!;

    public Profile this[string name]
    {
        get { return Profiles.SingleOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new ProfileNotFoundException(name); }
    }
}

public class ProfileNotFoundException(string profileName) : Exception($"Profile '{profileName}' not found.");