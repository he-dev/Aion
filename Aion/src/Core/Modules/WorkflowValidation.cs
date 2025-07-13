using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Aion.Core.Modules;

// role: This class provides extensions that allow us to validate workflows before they are even scheduled.
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

    public static void EnsureRenderable(this Workflow workflow)
    {
        // role: Ensures that templates in each step can be rendered.
        // code: Use fake values for testing.
        using var workflowActivity = new Activity("Test");
        foreach (var step in workflow.Steps)
        {
            using var stepActivity = new Activity("Test");
            step.RenderVariables([
                new ArgumentVariableGroup(workflow.Args),
                new WorkflowVariableGroup(workflowActivity) { Name = "test", Trigger = WorkflowTriggerGroup.Cron },
                new StepVariableGroup(stepActivity) { Name = "test", Index = 0 }
            ]);
        }
    }

    public static void EnsureSchedulable(this Workflow workflow)
    {
        // role: Ensures that the trigger can actually be created from its cron.
        // code: Using the property creates a new trigger each time that would throw an exception if it's invalid.
        workflow.Trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
    }
}

public class WorkflowNameNotUrlSafeException(string workflowName)
    : Exception($"Workflow name '{workflowName}' contains invalid characters. Allowed are only letters, numbers, and: - . _ ~");