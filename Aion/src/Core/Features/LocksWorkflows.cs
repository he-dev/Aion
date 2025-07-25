using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Features;

public class LocksWorkflows
(
    ILogger<LocksWorkflows> logger,
    FindsWorkflows findsWorkflows
)
{
    public async Task<IImmutableList<string>> Where(WorkflowLock workflowLock, string profile, string filter)
    {
        var workflowFileNames = findsWorkflows.Where(profile, filter, FileExtension.Json).ToImmutableList();

        if (!workflowFileNames.Any())
        {
            throw new WorkflowNotFoundException(filter);
        }

        var workflowLocks = ImmutableList<string>.Empty;

        foreach (var workflowFile in workflowFileNames)
        {
            var workflowLockPath = await workflowLock.SaveFor(workflowFile);
            workflowLocks = workflowLocks.Add(workflowLockPath);
            logger.LogInformation("Workflow '{WorkflowFile}' has been locked.", workflowFile);
        }

        return workflowLocks;
    }
}