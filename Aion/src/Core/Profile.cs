using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Util;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Aion.Core;

public record Profile
{
    public string Path { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));

    public string Sync { get; set; } = null!;

    public string[] Includes { get; set; } = [];

    public string[] Excludes { get; set; } = [];

    public IEnumerable<WorkflowMatch> Workflows(string? workflowNameOrFilter = null)
    {
        // core: Pass-1 - Use profile patterns to pre-filter its files.
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(Includes);
        matcher.AddExcludePatterns(Excludes);

        var candidates =
            from match in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(Path))).Files
            select match.Path;

        // core: Pass-2 - Search only the candidates for workflow-filter matches.
        var workflowFilter = $"**\\{workflowNameOrFilter ?? "*"}.json";
        matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(workflowFilter);

        var results = matcher.Match(candidates).Files;

        return
            from match in results
            select new WorkflowMatch(this, match.Path);
    }

    public WorkflowMatch Workflow(string workflowName)
    {
        return Workflows(workflowName).SingleOrThrows
        (
            onEmpty: () => new NoMatchException(Name, workflowName),
            onAmbiguous: () => new AmbiguousMatchException(Name, workflowName)
        );
    }

    public async Task<JsonObject> LoggingPreset(string loggingFile, string loggingName)
    {
        // meta: Create the path to the logging-presets-file and load it.
        var presetPath = System.IO.Path.Combine(Path, loggingFile);
        if (await LoggingPresetGroup.FromJson(presetPath) is { } loggingPresetGroup)
        {
            try
            {
                // core: Return the configuration.
                return loggingPresetGroup[loggingName].Serilog;
            }
            // meta: Single will throw this, so let's translate it to something meaningful.
            catch (InvalidOperationException)
            {
                throw new LoggingPresetNotFoundException(loggingFile, loggingName);
            }
        }

        throw new FileNotFoundException($"Logging preset file '{presetPath}' not found.", fileName: presetPath);
    }
}

public class LoggingPresetNotFoundException(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");

