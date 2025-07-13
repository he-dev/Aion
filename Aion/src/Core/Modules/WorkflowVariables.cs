using System.Collections.Generic;
using System.Diagnostics;
using Aion.Util.Scriban;

namespace Aion.Core.Modules;

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

public class WorkflowVariableGroup(Activity activity) : VariableGroup("Workflow")
{
    public required string Name { get; init; }

    public required WorkflowTriggerGroup Trigger { get; init; }

    public ActivityTraceId TraceId => activity.TraceId;

    public ActivitySpanId SpanId => activity.SpanId;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Trigger), Trigger);
        yield return new KeyValuePair<string, object?>(nameof(TraceId), TraceId);
        yield return new KeyValuePair<string, object?>(nameof(SpanId), SpanId);
    }
}

public class StepVariableGroup(Activity activity) : VariableGroup("Step")
{
    public string? Name { get; init; }

    public required int Index { get; init; }

    public ActivityTraceId TraceId => activity.TraceId;

    public ActivitySpanId SpanId => activity.SpanId;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Index), Index);
        yield return new KeyValuePair<string, object?>(nameof(TraceId), TraceId);
        yield return new KeyValuePair<string, object?>(nameof(SpanId), SpanId);
    }
}