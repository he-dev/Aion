using System;
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
    public async Task<IImmutableList<string>> Schedule(string filter, DateTimeOffset startsOnUtc, DateTimeOffset endsOnUtc)
    {
        var workflowFiles = workflowDirectory.FindFiles(filter, FileExtension.Json).ToImmutableList();

        if (!workflowFiles.Any())
        {
            throw new WorkflowNotFoundException(filter);
        }

        var workflowLocks = ImmutableList<string>.Empty;

        foreach (var workflowFile in workflowFiles)
        {
            var workflowLock = await WorkflowLock.Create(startsOnUtc, endsOnUtc).SaveFor(workflowFile);
            workflowLocks = workflowLocks.Add(workflowLock);
        }

        return workflowLocks;
    }

    public async Task<IImmutableList<string>> Schedule(string filter, TimeSpan wait, TimeSpan length)
    {
        var startsOnUtc = DateTimeOffset.UtcNow.Add(wait);
        var endsOnUtc = startsOnUtc.Add(length);

        return await Schedule(filter, startsOnUtc, endsOnUtc);
    }
}