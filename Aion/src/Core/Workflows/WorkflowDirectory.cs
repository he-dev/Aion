using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Utilities;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Workflows;

public class WorkflowDirectory
(
    ILogger<WorkflowDirectory> logger,
    IOptions<WorkflowEngineOptions> options
) : IAsyncEnumerable<Workflow>
{
    private IDirectoryTree DirectoryTree { get; } = new DirectoryTree(VariableTemplate.Render(options.Value.WorkflowDirectory, []));

    public async IAsyncEnumerator<Workflow> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        var paths =
            from branch in DirectoryTree
            from path in branch.Files()
            where Path.GetExtension(path).Equals($".{options.Value.WorkflowFileType}", StringComparison.OrdinalIgnoreCase)
            select path;

        foreach (var path in paths)
        {
            var workflow = default(Workflow);
            try
            {
                workflow = await Workflow.FromFile(path, DirectoryTree.Path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading workflow '{workflow}'.", path);
                continue;
            }

            if (workflow is not null)
            {
                yield return workflow;
            }
        }
    }
}

public static class WorkflowDirectoryExtensions
{
    public static async Task<Workflow?> FindWorkflow(this WorkflowDirectory source, string name)
    {
        return await source.FirstOrDefaultAsync(workflow => workflow.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}