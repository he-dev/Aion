using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Flairs;

public class FindsWorkflows
(
    ILogger<FindsWorkflows> logger,
    IOptions<EngineOptions> options
)
{
    public const string WorkflowsDirectory = "workflows";

    public IEnumerable<WorkflowPath> Where(string profile, string fileNameFilter)
    {
        if (options.Value.TryGetProfile(profile, out var profileInfo) == false)
        {
            throw new ArgumentException($"Profile '{profile}' not found.", nameof(profile));
        }

        if (string.IsNullOrEmpty(fileNameFilter)) throw new ArgumentException("Value cannot be null or empty.", nameof(fileNameFilter));

        var workflowFilter = $"**\\{fileNameFilter}.json";
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(workflowFilter);

        var profilePath = Path.Join(profileInfo.Path, WorkflowsDirectory);
        logger.LogDebug("Searching for workflows like '{WorkflowFilter}' in '{ProfilePath}'.", workflowFilter, profilePath);

        return
            from path in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(profilePath))).Files
            select new WorkflowPath(profilePath, path.Path);
    }
}



public record FileFilter(string Value)
{
    public static readonly FileFilter Any = new("*");

    public static implicit operator string(FileFilter filter) => filter.Value;
}

public class WorkflowNotFoundException(string filter) : Exception($"Filter '{filter}' does not match any workflows.");