using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Utilities;
using Aion.Util;
using Microsoft.Extensions.Options;

namespace Aion.Core.Workflows;

public class WorkflowDirectory
(
    IOptions<WorkflowEngineOptions> options
) : IAsyncEnumerable<Either<Workflow, Workflow.Issue>>
{
    private IDirectoryTree DirectoryTree { get; } = new DirectoryTree(VariableTemplate.Render(options.Value.WorkflowDirectory, []));

    public async IAsyncEnumerator<Either<Workflow, Workflow.Issue>> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        var paths =
            from branch in DirectoryTree
            from path in branch.Files()
            // !! Get only workflows with the matching extension.
            where Path.GetExtension(path).Equals($".{options.Value.WorkflowFileType}", StringComparison.OrdinalIgnoreCase)
            select path;

        foreach (var path in paths)
        {
            yield return await Workflow.FromFile(path, DirectoryTree.Path);
        }
    }
}

public static class WorkflowDirectoryExtensions
{
    public static async Task<Workflow?> FindWorkflow(this WorkflowDirectory workflows, string name)
    {
        await foreach (var either in workflows)
        {
            switch (either)
            {
                case Either<Workflow, Workflow.Issue>.InL { Value: var workflow } when workflow.Name.Equals(name, StringComparison.OrdinalIgnoreCase):
                    return workflow;
            }
        }

        return null;
    }
}