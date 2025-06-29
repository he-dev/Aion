using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aion.Util;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization.NamingConventions;

namespace Aion.Core.Modules;

// ?? Using a custom class allows it to receive a logger.
public class WorkflowFile(ILogger<WorkflowFile> logger)
{
    public async Task<Result<Workflow, Workflow.Issue>> Load(string path, string root)
    {
        // !! We want to show the errors to the API caller, so do not let them escape this method.
        try
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Workflow '{path}' not found.", fileName: path);
            }

            var workflow = Path.GetExtension(path).ToLower() switch
            {
                ".json" => await FromJson(path),
                ".yaml" => await FromYaml(path),
                _ => throw new InvalidOperationException($"Unknown file extension: {path}")
            } ?? throw new WorkflowNullException($"Workflow '{path}' is null.");

            // .. Update meta-properties.
            workflow = workflow with
            {
                Root = root,
                Path = path,
                Steps = workflow.Steps.Select((step, index) => step with { Index = index }).ToList()
            };

            EnsureWorkflowRenderable(workflow);

            return new Result<Workflow, Workflow.Issue>.Success(workflow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading workflow '{workflow}'.", path);

            var issue = new Workflow.Issue
            {
                Path = path,
                Exception = ex
            };

            return new Result<Workflow, Workflow.Issue>.Failure(issue);
        }
    }

    private static void EnsureWorkflowRenderable(Workflow workflow)
    {
        // !! Ensure that the steps can be rendered.
        // ?? Use fake values for testing.
        foreach (var step in workflow.Steps)
        {
            step.RenderVariables([
                new LocalVariableGroup(workflow.Variables),
                new WorkflowVariableGroup { Name = "test", Mode = "test", Cron = "0 0 0 * * ?" },
                new StepVariableGroup { Name = "test", Index = 0 }
            ]);
        }

        // !! Ensure that the trigger can be created.
        // ?? Using the property creates a new trigger each time.
        _ = workflow.Trigger;
    }

    public static async Task<Workflow?> FromJson(string path)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Workflow>(fileStream);
    }

    public static async Task<Workflow?> FromYaml(string path)
    {
        var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        await using var yamlFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var yamlStreamReader = new StreamReader(yamlFileStream);
        return yamlDeserializer.Deserialize<Workflow>(yamlStreamReader);
    }
}

public class WorkflowNullException(string message) : Exception(message);
