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
using Quartz;

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
            // !! Get only workflows with the matching extension.
            where Path.GetExtension(path).Equals($".{options.Value.WorkflowFileType}", StringComparison.OrdinalIgnoreCase)
            select path;

        foreach (var path in paths)
        {
            // ?? This helper is necessary because try/catch does not allow yield.
            var workflow = default(Workflow)!;
            try
            {
                workflow = await Workflow.FromFile(path, DirectoryTree.Path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading workflow '{workflow}'.", path);
                workflow = new Workflow
                {
                    Enabled = false,
                    Cron = string.Empty,
                    Info = new Workflow.Meta
                    {
                        Root = DirectoryTree.Path,
                        Path = path,
                        Exception = ex
                    },
                };
            }

            yield return workflow;
        }
    }
}

public static class WorkflowDirectoryExtensions
{
    public static async Task<Workflow?> FindWorkflow(this WorkflowDirectory source, string name)
    {
        return await source.FirstOrDefaultAsync(workflow => workflow.Info.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}