using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AionApi.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reusable.IO;

namespace AionApi.Workflows;

public class WorkflowDirectoryName(IOptions<WorkflowEngineOptions> options)
{
    public string Value { get; } = VariableTemplate.Render(options.Value.WorkflowDirectory, new Dictionary<string, string>());

    public static implicit operator string(WorkflowDirectoryName workflowDirectoryName) => workflowDirectoryName.Value;

    public static implicit operator int(WorkflowDirectoryName workflowDirectoryName) => workflowDirectoryName.Value.Length;
}

public class WorkflowName(WorkflowDirectoryName workflowDirectoryName)
{
    public string Create(string fileName)
    {
        // ?? Turns "C:\\foo\\bar\\baz.yaml" to "bar.baz" when the workflow-directory-name is "C:\\foo"
        var name = fileName[(workflowDirectoryName + 1)..].Replace('\\', '.');
        return name[..^Path.GetExtension(fileName).Length];
    }
}

public class WorkflowDirectory
(
    ILogger<WorkflowDirectory> logger,
    IOptions<WorkflowEngineOptions> options,
    WorkflowDirectoryName workflowDirectoryName,
    WorkflowName workflowName
) : IAsyncEnumerable<Workflow>
{
    private IDirectoryTree DirectoryTree { get; } = new DirectoryTree(workflowDirectoryName);

    public async Task<Workflow?> FindWorkflow(string name)
    {
        return await this.FirstOrDefaultAsync(workflow => workflow.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async IAsyncEnumerator<Workflow> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        var items =
            from branch in DirectoryTree
            from fileName in branch.Files()
            let workflowName = workflowName.Create(fileName)
            where Path.GetExtension(fileName).Equals($".{options.Value.WorkflowFileType}", StringComparison.OrdinalIgnoreCase)
            select new { fileName, workflowName };

        foreach (var item in items)
        {
            var workflow = default(Workflow);
            try
            {
                workflow = await Workflow.FromFile(item.fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading workflow '{workflow}'.", item.workflowName);
                continue;
            }

            if (workflow is not null)
            {
                yield return workflow with { Name = item.workflowName };
            }
        }
    }
}