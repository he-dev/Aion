using System.Collections.Generic;
using System.Diagnostics;
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

    public required string Trigger { get; init; }

    public required ActivityTraceId TraceId { get; init; }

    public required ActivitySpanId SpanId { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Trigger), Trigger);
        yield return new KeyValuePair<string, object?>(nameof(TraceId), TraceId);
    }
}

public class StepVariableGroup() : VariableGroup("Step")
{
    public string? Name { get; init; }

    public required int Index { get; init; }

    public required ActivityTraceId TraceId { get; init; }

    public required ActivitySpanId SpanId { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Index), Index);
    }
}