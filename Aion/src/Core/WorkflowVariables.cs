using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Aion.Util.Scriban;

namespace Aion.Core;

public class InstanceVariableGroup(Dictionary<string, string> variables) : VariableGroup("Instance")
{
    public required string Name { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables) yield return new KeyValuePair<string, object?>(variable.Key, variable.Value);
    }
}

public class ProfileVariableGroup(Dictionary<string, string> variables) : VariableGroup("Profile")
{
    public required string Name { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables) yield return new KeyValuePair<string, object?>(variable.Key, variable.Value);
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

public class WorkflowVariableGroup(IImmutableDictionary<string, string>? variables) : VariableGroup("Workflow")
{
    public required string Name { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables ?? ImmutableDictionary<string, string>.Empty) yield return new KeyValuePair<string, object?>(variable.Key, variable.Value);
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