using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Modules.Logging;
using Aion.Modules.Services;
using Aion.Toolbox;

namespace Aion.Modules;

// note: This class is deserialized from appsettings.Profiles.json.
public class Profile
{
    public string Path { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    // public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    public string Name => System.IO.Path.GetFileName(Path);

    public ProfileSync Sync { get; set; } = null!;

    public FindWorkflow Workflows
    {
        get => field; //?? throw new InvalidOperationException("Workflows are not configured.");
        set { field = value.Also(x => x.Profile = this); } // note: Is set implicitly by the json-serializer.
    }

    public Dictionary<string, string> Variables { get; set; } = new();

    public Dictionary<string, string> Environment { get; set; } = new();

    private FindLoggingPreset FindLoggingPresets => new(Path);

    public async Task<JsonObject?> GetLoggingOrDefault(LoggingConfiguration loggingConfiguration)
    {
        return loggingConfiguration.Source switch
        {
            LoggingSource.Auto => loggingConfiguration.Custom ?? await FindLoggingPresets.Where(loggingConfiguration.Preset),
            LoggingSource.Preset => await FindLoggingPresets.Where(loggingConfiguration.Preset) ?? throw new InvalidOperationException("Preset logging is missing."),
            LoggingSource.Custom => loggingConfiguration.Custom ?? throw new InvalidOperationException("Custom logging is missing."),
            _ => null
        };
    }
}

public class ProfileSync
{
    public bool Enabled { get; set; }
    public string Cron { get; set; } = null!;
}