using System.Collections.Generic;
using System.Linq;
using Aion.Core.Util;

namespace Aion.Core.Workflows;

public static class WorkflowExtensions
{
    public static Workflow.Step RenderVariables(this Workflow.Step step, params VariableGroup[] variableGroups)
    {
        return step with
        {
            Script = VariableTemplate.Render(step.Script, variableGroups),
            Args = step.Args.Select(arg => VariableTemplate.Render(arg, variableGroups)).ToList(),
            WorkingDirectory = VariableTemplate.Render(step.WorkingDirectory ?? string.Empty, variableGroups)
        };
    }
}

public class ApplicationVariableGroup() : VariableGroup("App")
{
    public string? Name { get; set; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
    }
}

public class LocalVariableGroup(IDictionary<string, object?> variables) : VariableGroup("Local")
{
    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => variables.GetEnumerator();
}

public class WorkflowVariableGroup() : VariableGroup("Workflow")
{
    public required string Name { get; init; }

    public required string Mode { get; init; }

    public string? Cron { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Mode), Mode);
        yield return new KeyValuePair<string, object?>(nameof(Cron), Cron);
    }
}

public class StepVariableGroup() : VariableGroup("Step")
{
    public required string? Name { get; init; }

    public required int Index { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Index), Index);
    }
}