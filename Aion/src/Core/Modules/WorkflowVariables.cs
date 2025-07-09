using System.Collections.Generic;
using Aion.Util.Scriban;

namespace Aion.Core.Modules;

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

    public required string JobId { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Mode), Mode);
        yield return new KeyValuePair<string, object?>(nameof(Cron), Cron);
        yield return new KeyValuePair<string, object?>(nameof(JobId), JobId);
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