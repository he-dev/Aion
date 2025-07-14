using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Aion.Util.Json;
using Aion.Util.Scriban;

namespace Aion.Core.Modules;

// core: This class provides extensions that allow us to validate workflows before they are even scheduled.
public static class WorkflowValidation
{
    // note:
    // Regex for characters that are "unreserved" in a URI (per RFC 3986) and don't need escaping.
    // This includes alphanumeric characters, hyphen, period, underscore, and tilde.
    // The Regex is compiled for better performance since it will be reused.
    private static readonly Regex UrlSafeChars = new("^[a-zA-Z0-9._~-]+$", RegexOptions.Compiled);


    public static void EnsureUrlSafeName(this Workflow workflow)
    {
        if (!UrlSafeChars.IsMatch(workflow.Name))
        {
            throw new WorkflowNameNotUrlSafeException(workflow.Name);
        }
    }

    // core: Ensures that templates in each step can be rendered.
    public static void EnsureRenderable(this Workflow workflow)
    {
        using var workflowActivity = new Activity("Test");

        // core: Use fake values for testing.
        var variables = ImmutableList<VariableGroup>.Empty.AddRange([
            new ProfileVariableGroup { Name = "Test" },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup(workflowActivity) { Name = "test", Trigger = WorkflowTriggerGroup.Cron }
        ]);

        workflow.Serilog.RenderPaths(variables);
        workflow.Console.RenderPaths(variables);

        foreach (var step in workflow.Steps)
        {
            using var stepActivity = new Activity("Test");
            step.RenderVariables(variables.Add(new StepVariableGroup(stepActivity) { Name = "test", Index = 0 }));
        }
    }

    // core: Ensures that the trigger can actually be created from its cron.
    public static void EnsureSchedulable(this Workflow workflow)
    {
        // core: Using the property creates a new trigger each time that would throw an exception if it's invalid.
        workflow.Trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
    }
}

public class WorkflowNameNotUrlSafeException(string workflowName)
    : Exception($"Workflow name '{workflowName}' contains invalid characters. Allowed are only letters, numbers, and: - . _ ~");