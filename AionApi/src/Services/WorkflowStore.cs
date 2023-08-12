using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AionApi.Models;
using Reusable.Extensions;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Services;

public class WorkflowStore : IAsyncEnumerable<Workflow>
{
    public WorkflowStore(ILogger logger, WorkflowDirectory workflowDirectory)
    {
        Logger = logger;
        WorkflowDirectory = workflowDirectory;
    }

    private ILogger Logger { get; }

    private WorkflowDirectory WorkflowDirectory { get; }

    public async Task<Workflow?> GetWorkflow(string name)
    {
        var fileName = WorkflowDirectory + name;
        using var status = Logger.Begin("GetWorkflow", details: new { name });
        if (!File.Exists(fileName))
        {
            status.LogStop(message: "Workflow not found.", details: new { fileName });
            return default;
        }

        await using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (await JsonSerializer.DeserializeAsync<Workflow>(fileStream) is { } workflow)
        {
            status.LogEnd();
            return workflow.Also(w => w.Name = name);
        }

        status.LogStop(message: "Workflow is empty.", details: new { fileName });
        return default;
    }

    // public async Task Add(Workflow workflow)
    // {
    //     Initialize();
    //     var fileName = WorkflowDirectory + workflow.Name;
    //     using var status = Logger.Start("AddWorkflow", new { name = workflow.Name }, fileName);
    //     await using var workflowStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
    //     await JsonSerializer.SerializeAsync(workflowStream, workflow);
    //     status.Completed();
    // }

    // public bool Delete(string name)
    // {
    //     var fileName = WorkflowDirectory + name;
    //     using var status = Logger.Start("DeleteWorkflow", new { name }, fileName);
    //
    //     if (File.Exists(fileName))
    //     {
    //         File.Delete(fileName);
    //         status.Completed();
    //         return true;
    //     }
    //
    //     status.Canceled(new { reason = "File not found." });
    //     return false;
    // }
    
    // private void Initialize()
    // {
    //     using var status = Logger.Start("InitializeWorkflowDirectory", attachment: DirectoryName);
    //     if (Path.Exists(DirectoryName))
    //     {
    //         status.Canceled(new { reason = "Workflow directory already exists." });
    //     }
    //     else
    //     {
    //         Directory.CreateDirectory(DirectoryName);
    //         status.Completed();
    //     }
    // }
    
    public async IAsyncEnumerator<Workflow> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        foreach (var fileName in WorkflowDirectory)
        {
            if (await GetWorkflow(fileName) is { } workflow)
            {
                yield return workflow;
            }
        }
    }
}