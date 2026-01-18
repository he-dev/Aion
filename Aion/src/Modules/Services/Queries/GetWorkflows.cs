using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Toolbox;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Aion.Modules.Services.Queries;

// note: This class is deserialized from appsettings.Profiles.json.
public class GetWorkflows
{
    internal Profile Profile { get; set; } = null!;

    public string Path => System.IO.Path.Join(Profile.Path, WorkflowPath.DefaultName);

    public string[] Includes { get; set; } = [];

    public string[] Excludes { get; set; } = [];

    public IEnumerable<WorkflowPath> Where(WorkflowFilter workflowFilter)
    {
        // core: Pass-1 - Use profile patterns to pre-filter its files.
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(Includes);
        matcher.AddExcludePatterns(Excludes);

        var candidates =
            from match in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(Path))).Files
            select match.Path;

        // core: Pass-2 - Search only the candidates for workflow-filter matches.
        matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(workflowFilter);
        var results = matcher.Match(candidates).Files;

        return
            from filePatternMatch in results
            let pathWithinProfile = filePatternMatch.Path.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar)
            let workflowMatch = new WorkflowPath(Profile.Path, new WorkflowName(pathWithinProfile))
            select workflowMatch;
    }

    public IEnumerable<WorkflowPath> All() => Where("*");

    public WorkflowPath Single(WorkflowFilter workflowFilter) => Where(workflowFilter).SingleOrThrows
    (
        onEmpty: () => new NoWorkflowMatch(Profile.Name, workflowFilter),
        onExtra: () => new AmbiguousWorkflowMatch(Profile.Name, workflowFilter)
    );
}

public class NoWorkflowMatch(string profileName, string workflowNameOrFilter)
    : Exception($"No workflow in '{profileName}' matches '{workflowNameOrFilter}'.");

public class AmbiguousWorkflowMatch(string profileName, string workflowNameOrFilter)
    : Exception($"More than one workflow in '{profileName}' match '{workflowNameOrFilter}'.");