using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Aion.Util.Scriban;

namespace Aion.Core;

public class EngineVariableGroup(Dictionary<string, object?> variables) : VariableGroup("Engine")
{
    public required string Name { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables) yield return variable;
    }
}

public class ProfileVariableGroup(Dictionary<string, object?> variables) : VariableGroup("Profile")
{
    public required string Name { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables) yield return variable;
    }
}

public class ExecutionVariableGroup() : VariableGroup("Execution")
{
    public required WorkflowExecutionMode Mode { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        if (Activity.Current is { } current)
        {
            yield return new KeyValuePair<string, object?>(nameof(Mode), Mode);
            yield return new KeyValuePair<string, object?>(nameof(Activity.TraceId), current.TraceId);
            yield return new KeyValuePair<string, object?>(nameof(Activity.SpanId), current.SpanId);
            yield return new KeyValuePair<string, object?>(nameof(Activity.ParentId), current.ParentId);
        }
    }
}

public class WorkflowVariableGroup(IImmutableDictionary<string, object?> variables) : VariableGroup("Workflow")
{
    public required string Name { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables) yield return variable;
    }
}

public class StepVariableGroup() : VariableGroup("Step")
{
    public string? Name { get; init; }

    public required int Index { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Index), Index);
    }
}