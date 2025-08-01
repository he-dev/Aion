using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Aion.Home.Jobs;
using Aion.Util.Json;
using Aion.Util.Quartz;
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
    private static readonly Regex UrlSafeChars = new("^[a-zA-Z0-9._~-]+$", RegexOptions.Compiled);

    public static void EnsureUrlSafeName(this string workflowName)
    {
        if (!UrlSafeChars.IsMatch(workflowName))
        {
            throw new WorkflowNameNotUrlSafeException(workflowName);
        }
    }

    // core: Ensures that templates in each step can be rendered.
    public static void EnsureVariables(this Workflow workflow)
    {
        using var workflowActivity = new Activity("testing-workflow");

        var variables = ImmutableList<VariableGroup>.Empty.AddRange([
            new ProfileVariableGroup { Name = "test" },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup(workflowActivity) { Name = "test" }
        ]);

        workflow.Logging.RenderFilePaths(template => RendersTemplates.In(template, variables));

        foreach (var (step, index) in workflow.Steps.Select((step, index) => (step, index)))
        {
            using var stepActivity = new Activity("testing-step");
            step.RenderTemplates(variables.Add(new StepVariableGroup(stepActivity) { Name = "test", Index = index }));
        }
    }

    // core: Ensures that the trigger can actually be created from its cron.
    public static void EnsureCron(this Workflow workflow)
    {
        // core: This will throw a cron-exception in case it's invalid.
        TriggerBuilder
            .Create()
            .WithIdentity("test", "test")
            .WithCronSchedule(workflow.Cron)
            .Build();
    }
}

public class WorkflowNameNotUrlSafeException(string workflowName)
    : Exception($"Workflow name '{workflowName}' contains invalid characters. Allowed are only letters, numbers, and: - . _ ~");