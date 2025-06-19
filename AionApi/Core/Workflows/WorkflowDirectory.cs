using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AionApi.Util;
using AionApi.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AionApi.Workflows;

public class WorkflowDirectory
(
    ILogger<WorkflowDirectory> logger,
    IOptions<WorkflowEngineOptions> options
) : IAsyncEnumerable<Workflow>
{
    private IDirectoryTree DirectoryTree { get; } = new DirectoryTree(VariableTemplate.Render(options.Value.WorkflowDirectory, new Dictionary<string, string>()));

    public async Task<Workflow?> FindWorkflow(string name)
    {
        return await this.FirstOrDefaultAsync(workflow => workflow.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

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