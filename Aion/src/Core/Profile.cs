using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aion.Core.Logging;
using Aion.Core.Workflows;

namespace Aion.Core;

public class Profile
{
    public string Path { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));

    public string Sync { get; set; } = null!;

    public bool SyncOn { get; set; }

    public string[] Includes { get; set; } = [];

    public string[] Excludes { get; set; } = [];

    public Dictionary<string, string> Variables { get; set; } = new();

    public Dictionary<string, string> Environment { get; set; } = new();

    public LoggingPreset.Info? Logging { get; set; }

    public Func<WorkflowExecutionMode, JsonObject?> RenderLogging { get; set; } = (_) => null;

    [JsonIgnore]
    public WorkflowRepository Workflows => new(this);

    [JsonIgnore]
    public LoggingPresetRepository LoggingPresets => new(Path);
}