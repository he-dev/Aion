using System;
using System.Collections.Generic;
using System.Diagnostics;
using Aion.Util.Scriban;

namespace Aion.Core;

public class EngineVariableGroup() : VariableGroup("Aion")
{
    public string Instance { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Instance), Instance);
    }
}

public class ProfileVariableGroup() : VariableGroup("Profile")
{
    public string Name { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
    }
}

public class ArgumentVariableGroup(IDictionary<string, object?> variables) : VariableGroup("Arg")
{
    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => variables.GetEnumerator();
}

public class WorkflowVariableGroup() : VariableGroup("Workflow")
{
    public required string Name { get; init; }

    public ActivityTraceId TraceId => Activity.Current?.TraceId ?? throw new InvalidOperationException("There is no activity in scope.");

    public ActivitySpanId SpanId => Activity.Current?.SpanId ?? throw new InvalidOperationException("There is no activity in scope.");

    public ActivitySpanId ParentId => Activity.Current?.ParentSpanId ?? throw new InvalidOperationException("There is no activity in scope.");

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(TraceId), TraceId);
        yield return new KeyValuePair<string, object?>(nameof(SpanId), SpanId);
        yield return new KeyValuePair<string, object?>(nameof(ParentId), ParentId);
    }
}

public class StepVariableGroup() : VariableGroup("Step")
{
    public string? Name { get; init; }

    public required int Index { get; init; }

    public ActivityTraceId TraceId => Activity.Current?.TraceId ?? throw new InvalidOperationException("There is no activity in scope.");

    public ActivitySpanId SpanId => Activity.Current?.SpanId ?? throw new InvalidOperationException("There is no activity in scope.");

    public ActivitySpanId ParentId => Activity.Current?.ParentSpanId ?? throw new InvalidOperationException("There is no activity in scope.");

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Index), Index);
        yield return new KeyValuePair<string, object?>(nameof(TraceId), TraceId);
        yield return new KeyValuePair<string, object?>(nameof(SpanId), SpanId);
        yield return new KeyValuePair<string, object?>(nameof(ParentId), ParentId);
    }
}