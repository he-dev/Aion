using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Core.Logging;
using Aion.Core.Workflows;
using Aion.Util;

namespace Aion.Core;

// note: This class is deserialized from appsettings.Profiles.json.
public class Profile
{
    public string Root { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    // public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    public string Name { get; set; } = null!;

    public string Path => System.IO.Path.Join(Root, Name);

    public ProfileSync Sync { get; set; } = null!;

    public WorkflowDirectory Workflows
    {
        get => field; //?? throw new InvalidOperationException("Workflows are not configured.");
        set { field = value.Also(x => x.Profile = this); } // note: Is set implicitly by the json-serializer.
    }

    public Dictionary<string, string> Variables { get; set; } = new();

    public Dictionary<string, string> Environment { get; set; } = new();

    private LoggingPresetRepository LoggingPresets => new(Path);

    public async Task<JsonObject?> GetLoggingOrDefault(LoggingConfiguration loggingConfiguration)
    {
        return loggingConfiguration.Source switch
        {
            LoggingSource.Auto => loggingConfiguration.Custom ?? await LoggingPresets.Find(loggingConfiguration.Preset),
            LoggingSource.Preset => await LoggingPresets.Find(loggingConfiguration.Preset) ?? throw new InvalidOperationException("Preset logging is missing."),
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