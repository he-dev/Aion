using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                        return ReadWorkflow(fileName).Also(w => w.Name = Regex.Replace(fileName, $"^{Regex.Escape(path)}", string.Empty));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading workflow: {fileName}", fileName);
                        return default;
                    }
                })
                .Where(Conditional.IsNotNull)!;
    }

    // Reads only a single workflow.
    public Workflow ReadWorkflow(string fileName)
    {
        var json = File.ReadAllText(fileName);
        return JsonConvert.DeserializeObject<Workflow>(json, _jsonSerializerSettings)!;
    }
}