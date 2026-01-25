using System.Collections.Generic;
using Aion.Util.Services;

namespace Aion.Util;

// note: This class is deserialized from appsettings.Profiles.json.
public class Profile
{
    public string Path { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    // public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    public string Name => System.IO.Path.GetFileName(Path);

    public ProfileSync Sync { get; set; } = null!;

    public WorkflowPatterns? Workflows { get; set; }

    // meta: This property can be null in JSON, but nulls suck in code, so use empty dictionaries instead.
    // ReSharper disable once CollectionNeverUpdated.Global
    public Dictionary<string, string>? Parameters { get; set; }

    // meta: This property can be null in JSON, but nulls suck in code, so use empty dictionaries instead.
    // ReSharper disable once CollectionNeverUpdated.Global
    public Dictionary<string, string>? Environment { get; set; }

    public GetLoggingPreset GetLoggingPreset => new(Path);
}

public class ProfileSync
{
    public bool Enabled { get; set; }

    public string Cron { get; set; } = null!;
}