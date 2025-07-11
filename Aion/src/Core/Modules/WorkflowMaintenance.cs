using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Modules;

public class WorkflowMaintenance
(
    ILogger<WorkflowMaintenance> logger,
    WorkflowDirectory workflowDirectory
)
{
    public async Task<IImmutableList<string>> Schedule(WorkflowLock workflowLock, string filter)
    {
        var workflowFiles = workflowDirectory.FindFiles(filter, FileExtension.Json).ToImmutableList();

        if (!workflowFiles.Any())
        {
            throw new WorkflowNotFoundException(filter);
        }

        var workflowLocks = ImmutableList<string>.Empty;

        foreach (var workflowFile in workflowFiles)
        {
            var workflowLockPath = await workflowLock.SaveFor(workflowFile);
            workflowLocks = workflowLocks.Add(workflowLockPath);
        }

        return workflowLocks;
    }
}