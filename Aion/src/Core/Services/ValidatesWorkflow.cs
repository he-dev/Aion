using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aion.Util.Scriban;
using Quartz;

namespace Aion.Core.Services;

// core: This class provides extensions that allow us to validate workflows before they are even scheduled.
public static class ValidatesWorkflow
{
    // note:
    // Regex for characters that are "unreserved" in a URI (per RFC 3986) and don't need escaping.
    // This includes alphanumeric characters, hyphen, period, underscore, and tilde.
    // The Regex is compiled for better performance since it will be reused.
    private static readonly Regex MatchesUrlSafeChars = new("^[a-zA-Z0-9._~-]+$", RegexOptions.Compiled);

    public static string EnsureUrlSafe(this string value)
    {
        if (!MatchesUrlSafeChars.IsMatch(value))
        {
            throw new WorkflowNameNotUrlSafeException(value);
        }

        return value;
    }

    public static bool IsUrlSafe(this string value) => MatchesUrlSafeChars.IsMatch(value);


    // core: Ensures that templates in each step can be rendered.
    public static async Task EnsureTemplatesRenderable(this Workflow workflow, Profile profile)
    {
        using var activity = new Activity("testing-workflow");

        var variables = ImmutableList<VariableGroup>.Empty.AddRange(
        [
            new EngineVariableGroup { Instance = "test" },
            new ProfileVariableGroup { Name = "test" },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup { Name = "test" }
        ]);

        if (workflow.Logging is { } workflowLogging)
        {
            await workflowLogging.RenderAsync(profile, variables);
        }

        foreach (var (step, index) in workflow.Steps.Select((step, index) => (step, index)))
        {
            await step.EnsureTemplatesRenderable(index, profile, variables);
        }
    }

    private static async Task EnsureTemplatesRenderable(this Workflow.Step step, int index, Profile profile, IImmutableList<VariableGroup> variables)
    {
        variables = variables.Add(new StepVariableGroup { Index = index, Name = step.Name });
        using var activity = new Activity("testing-step").Start();
        if (step.Logging is { } stepLogging)
        {
            await stepLogging.RenderAsync(profile, variables);
        }

        step.File.Render(variables);
        step.Args.RenderArgList(variables);
        step.Args.RenderArgString(variables);
        step.WorkingDirectory?.Render(variables);
    }

    // core: Ensures that the trigger can actually be created from its cron.
    public static void EnsureCronSchedulable(this Workflow workflow)
    {
        // core: This will throw a cron-exception in case it's invalid.
        TriggerBuilder
            .Create()
            .WithIdentity("test-name", "test-group")
            .WithCronSchedule(workflow.Cron)
            .Build();
    }
}

public class WorkflowNameNotUrlSafeException(string workflowName)
    : Exception($"Workflow name '{workflowName}' contains invalid characters. Allowed are only letters, numbers, and: - . _ ~");