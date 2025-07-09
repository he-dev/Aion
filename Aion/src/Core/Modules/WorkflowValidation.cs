using System;

namespace Aion.Core.Modules;

// role: This class provides extensions that allow us to validate workflows before they are even scheduled.
public static class WorkflowValidation
{
    public static void EnsureRenderable(this Workflow workflow)
    {
        // role: Ensures that templates in each step can be rendered.
        // code: Use fake values for testing.
        foreach (var step in workflow.Steps)
        {
            step.RenderVariables([
                new LocalVariableGroup(workflow.Variables),
                new WorkflowVariableGroup { Name = "test", Mode = "test", Cron = "0 0 0 * * ?", JobId = "test" },
                new StepVariableGroup { Name = "test", Index = 0 }
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