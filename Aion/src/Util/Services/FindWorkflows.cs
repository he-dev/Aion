using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Meta;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Services;

// note: This class is deserialized from appsettings.Profiles.json.
public class FindWorkflows
(
    ILogger<FindWorkflows> logger
)
{
    public IEnumerable<WorkflowPath> Where(WorkflowSearchCriteria criteria)
    {
        logger.LogInformation("Searching for workflows in '{Path}' matching '{Pattern}'.", criteria.Path, criteria.Pattern);

        // core: Pass-1 - Use profile patterns to pre-filter its files.
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(criteria.Profile.Workflows.Includes);
        matcher.AddExcludePatterns(criteria.Profile.Workflows.Excludes);

        var candidates =
            from match in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(criteria.Path))).Files
            select match.Path;

        // core: Pass-2 - Search only the candidates for workflow-filter matches.
        matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(criteria);
        var results = matcher.Match(candidates).Files;

        return
            from filePatternMatch in results
            let pathWithinProfile = filePatternMatch.Path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            let workflowMatch = new WorkflowPath(criteria.Profile.Path, WorkflowName.FromPath(pathWithinProfile))
            select workflowMatch;
    }

    public WorkflowPath Single(WorkflowSearchCriteria criteria) => Where(criteria).SingleOrThrows
    (
        onEmpty: () => new NoWorkflowMatch(criteria),
        onExtra: () => new AmbiguousWorkflowMatch(criteria)
    );
}

public class NoWorkflowMatch(WorkflowSearchCriteria criteria)
    : Exception($"No workflow in '{criteria.Profile.Name}' matches '{criteria.Pattern}'.");

public class AmbiguousWorkflowMatch(WorkflowSearchCriteria criteria)
    : Exception($"More than one workflow in '{criteria.Profile.Name}' match '{criteria.Pattern}'.");