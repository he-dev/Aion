using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Util;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Services;

public class FindsWorkflows
(
    ILogger<FindsWorkflows> logger,
    IOptions<EngineOptions> options
)
{
    public const string WorkflowsDirectory = "workflows";

    public IEnumerable<WorkflowPath> Where__(string profileName, string? workflowNameOrFilter = null)
    {
        var profileInfo = options.Value[profileName];
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        var workflowFilter = $"**\\{workflowNameOrFilter ?? "*"}.json";
        matcher.AddInclude(workflowFilter);

        var workflowsPath = Path.Join(profileInfo.Path, WorkflowsDirectory);
        logger.LogDebug("Searching for workflows like '{WorkflowFilter}' in '{WorkflowsPath}'.", workflowFilter, workflowsPath);


        return
            from path in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(workflowsPath))).Files
            select new WorkflowPath(workflowsPath, path.Path);
    }

    public IEnumerable<WorkflowPath> Where(string profileName, string? workflowNameOrFilter = null)
    {
        var profileInfo = options.Value[profileName];

        // core: Pass-1 - Use profile patters to pre-filter its files.
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(profileInfo.Includes);
        matcher.AddExcludePatterns(profileInfo.Excludes);

        var candidates = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(profileInfo.Path))).Files.Select(f => f.Path);

        // core: Pass-2 - Use the results from the first pass to get the final result.
        var workflowFilter = $"**\\{workflowNameOrFilter ?? "*"}.json";
        matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(workflowFilter);

        var results = matcher.Match(candidates).Files;

        logger.LogDebug("Searching for workflows like '{WorkflowFilter}' in '{WorkflowsPath}'.", workflowFilter, profileInfo.Path);

        return
            from path in results
            select new WorkflowPath(profileInfo.Path, path.Path);
    }

    public WorkflowPath Single(string profileName, string workflowName)
    {
        return Where(profileName, workflowName).SingleOrThrows
        (
            onEmpty: () => new NoMatchException(profileName, workflowName),
            onAmbiguous: () => new AmbiguousMatchException(profileName, workflowName)
        );
    }
}