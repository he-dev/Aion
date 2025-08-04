using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Core.Services;
using Aion.Util;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Aion.Core;

public class Profile
{
    public string Path { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));

    public string Sync { get; set; } = null!;

    public string[] Includes { get; set; } = [];

    public string[] Excludes { get; set; } = [];

    [JsonIgnore]
    public WorkflowRepository Workflows => new(this);

    [JsonIgnore]
    public LoggingPresetRepository LoggingPresets => new(this);
}

// meta: Converts the url-safe workflow "path" into a glob-filter.
public record WorkflowFilter(string Value)
{
    public string Pattern => $"**\\{Value.EnsureUrlSafe().Replace('.', Path.DirectorySeparatorChar)}.json";

    public static implicit operator WorkflowFilter(string value) => new(value);
}

public class WorkflowRepository(Profile profile)
{
    public IEnumerable<WorkflowMatch> Where(WorkflowFilter workflowFilter)
    {
        // core: Pass-1 - Use profile patterns to pre-filter its files.
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(profile.Includes);
        matcher.AddExcludePatterns(profile.Excludes);

        var candidates =
            from match in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(profile.Path))).Files
            select match.Path;

        // core: Pass-2 - Search only the candidates for workflow-filter matches.
        matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(workflowFilter.Pattern);
        var results = matcher.Match(candidates).Files;

        return
            from match in results
            select new WorkflowMatch(profile, match.Path);
    }

    public IEnumerable<WorkflowMatch> All() => Where("*");

    public WorkflowMatch Single(string workflowName)
    {
        return Where(workflowName).SingleOrThrows
        (
            onEmpty: () => new NoWorkflowMatch(profile.Name, workflowName),
            onExtra: () => new AmbiguousWorkflowMatch(profile.Name, workflowName)
        );
    }
}

public class LoggingPresetRepository(Profile profile)
{
    public async Task<JsonObject> Single(string file, string preset)
    {
        // meta: Create the path to the logging-presets-file and load it.
        var presetPath = Path.Combine(profile.Path, file);
        if (await LoggingPresetGroup.FromJson(presetPath) is { } loggingPresetGroup)
        {
            try
            {
                // core: Return the configuration.
                return loggingPresetGroup[preset].Serilog;
            }
            // meta: Single will throw this, so let's translate it to something meaningful.
            catch (InvalidOperationException)
            {
                throw new LoggingPresetNotFound(file, preset);
            }
        }

        throw new FileNotFoundException($"Logging preset file '{presetPath}' not found.", fileName: presetPath);
    }
}

public class LoggingPresetNotFound(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");

public class NoWorkflowMatch(string profileName, string workflowNameOrFilter)
    : Exception($"No workflow in '{profileName}' matches '{workflowNameOrFilter}'.");

public class AmbiguousWorkflowMatch(string profileName, string workflowNameOrFilter)
    : Exception($"More than one workflow in '{profileName}' match '{workflowNameOrFilter}'.");