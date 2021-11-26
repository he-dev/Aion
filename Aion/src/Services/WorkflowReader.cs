using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Reusable.Extensions;

namespace Aion.Services;

internal class WorkflowReader
{
    private readonly ILogger<WorkflowReader> _logger;

    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        DefaultValueHandling = DefaultValueHandling.Populate
    };

    public WorkflowReader(ILogger<WorkflowReader> logger)
    {
        _logger = logger;
    }

    // Reads all workflows in the specified path.
    public IEnumerable<Workflow> ReadWorkflows(string path)
    {
        return
            Directory
                .GetFiles(path, "*.json")
                .Select(fileName =>
                {
                    try
                    {
                        return ReadWorkflow(fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading robot settings for: {fileName}", fileName);
                        return default;
                    }
                })
                .Where(Conditional.IsNotNull)!;
    }

    // Reads only a single workflow.
    public Workflow ReadWorkflow(string fileName)
    {
        var json = File.ReadAllText(fileName);
        return JsonConvert.DeserializeObject<Workflow>(json, _jsonSerializerSettings)!.Also(s => { s.Name = Path.GetFileNameWithoutExtension(fileName); });
    }
}