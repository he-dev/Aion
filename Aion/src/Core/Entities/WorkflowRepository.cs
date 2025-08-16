using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Util;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Aion.Core.Entities;

public class WorkflowRepository(Profile profile)
{
    public IEnumerable<WorkflowMatch> Find(WorkflowFilter workflowFilter)
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
        matcher.AddInclude(workflowFilter);
        var results = matcher.Match(candidates).Files;

        return
            from filePatternMatch in results
            let pathWithinProfile = filePatternMatch.Path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            let workflowMatch = new WorkflowMatch(profile, workflowFilter, pathWithinProfile)
            where workflowMatch.Name.IsUrlSafe
            select workflowMatch;
    }

    public IEnumerable<WorkflowMatch> All() => Find("*");

    public WorkflowMatch Single(string workflowName) => Find(workflowName).SingleOrThrows
    (
        onEmpty: () => new NoWorkflowMatch(profile.Name, workflowName),
        onExtra: () => new AmbiguousWorkflowMatch(profile.Name, workflowName)
    );
}

public class NoWorkflowMatch(string profileName, string workflowNameOrFilter)
    : Exception($"No workflow in '{profileName}' matches '{workflowNameOrFilter}'.");

public class AmbiguousWorkflowMatch(string profileName, string workflowNameOrFilter)
    : Exception($"More than one workflow in '{profileName}' match '{workflowNameOrFilter}'.");