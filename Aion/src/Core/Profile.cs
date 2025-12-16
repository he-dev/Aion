using System.Collections.Generic;
using System.Text.Json.Serialization;
using Aion.Core.Logging;
using Aion.Core.Workflows;
using Aion.Util;

namespace Aion.Core;

public class Profile
{
    public string Path { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    // public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    public string Name { get; set; } = null!;

    public ProfileSync Sync { get; set; } = null!;

    public WorkflowDirectory Workflows
    {
        get;
        set { field = value.Also(x => x.Profile = this); }
    }

    public Dictionary<string, string> Variables { get; set; } = new();

    public Dictionary<string, string> Environment { get; set; } = new();

    [JsonIgnore]
    public LoggingPresetRepository LoggingPresets => new(Path);
}

public class ProfileSync
{
    public bool Enabled { get; set; }
    public string Cron { get; set; } = null!;
}