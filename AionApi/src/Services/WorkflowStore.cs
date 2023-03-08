using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AionApi.Models;
using Microsoft.Extensions.Options;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Services;

public class WorkflowStore
{
    public WorkflowStore(ILogger logger, IOptions<WorkflowEngineOptions> options)
    {
        Logger = logger;
        Options = options;
    }

    private ILogger Logger { get; }

    private IOptions<WorkflowEngineOptions> Options { get; }

    private string DirectoryName => Environment.ExpandEnvironmentVariables(Options.Value.StoreDirectory);

    public async Task<Workflow?> Get(string name)
    {
        var fileName = GetFileName(name);
        using var status = Logger.Start("GetWorkflow", new { name }, fileName);
        if (File.Exists(fileName))
        {
            await using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await JsonSerializer.DeserializeAsync<Workflow>(fileStream) is { } workflow)
            {
                status.Completed();
                return workflow;
            }

            status.Canceled(new { reason = "File not deserialized." }, fileName);
        }
        else
        {
            status.Canceled(new { reason = "File not found." }, fileName);
        }

        return default;
    }

    public async Task Add(Workflow workflow)
    {
        Initialize();
        var fileName = GetFileName(workflow.Name);
        using var status = Logger.Start("AddWorkflow", new { name = workflow.Name }, fileName);
        await using var workflowStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(workflowStream, workflow);
        status.Completed();
    }

    public bool Delete(string name)
    {
        var fileName = GetFileName(name);
        using var status = Logger.Start("DeleteWorkflow", new { name }, fileName);

        if (File.Exists(fileName))
        {
            File.Delete(fileName);
            status.Completed();
            return true;
        }

        status.Canceled(new { reason = "File not found." });
        return false;
    }

    public async IAsyncEnumerable<Workflow> EnumerateWorkflows()
    {
        foreach (var fileName in Directory.EnumerateFiles(DirectoryName))
        {
            if (await Get(Path.GetFileNameWithoutExtension(fileName)) is { } workflow)
            {
                yield return workflow;
            }
        }
    }

    private void Initialize()
    {
        using var status = Logger.Start("InitializeWorkflowDirectory", attachment: DirectoryName);
        if (Path.Exists(DirectoryName))
        {
            status.Canceled(new { reason = "Workflow directory already exists." });
        }
        else
        {
            Directory.CreateDirectory(DirectoryName);
            status.Completed();
        }
    }

    private string GetFileName(string name) => Path.Combine(DirectoryName, $"{name}.json");
}