using System;
using System.Collections.Generic;
using System.Diagnostics;
using Aion.Util.Templates;

namespace Aion.Core.Workflows;

public class InstanceVariableGroup(IEnumerable<KeyValuePair<string, string>> variables) : TemplateVariableGroup("Instance")
{
    public required string Name { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables) yield return new KeyValuePair<string, object?>(variable.Key, variable.Value);
    }
}

public class ProfileVariableGroup(IEnumerable<KeyValuePair<string, string>> variables) : TemplateVariableGroup("Profile")
{
    public required string Name { get; init; } = null!;

    public required string Path { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Path), Path);
        foreach (var variable in variables) yield return new KeyValuePair<string, object?>(variable.Key, variable.Value);
    }
}

public class ExecutionVariableGroup() : TemplateVariableGroup("Execution")
{
    public required WorkflowExecutionMode Mode { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Mode), Mode);
    }
}

public class WorkflowVariableGroup(IEnumerable<KeyValuePair<string, string>> variables) : TemplateVariableGroup("Workflow")
{
    public required string Name { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        foreach (var variable in variables) yield return new KeyValuePair<string, object?>(variable.Key, variable.Value);
    }
}

public class StepVariableGroup() : TemplateVariableGroup("Step")
{
    public string? Name { get; init; }

    public required int Index { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Index), Index);

        if (Activity.Current is { } current)
        {
            yield return new KeyValuePair<string, object?>(nameof(Activity.TraceId), current.TraceId);
            yield return new KeyValuePair<string, object?>(nameof(Activity.SpanId), current.SpanId);
            yield return new KeyValuePair<string, object?>(nameof(Activity.ParentId), current.ParentId);
        }
        else
        {
            throw new InvalidOperationException("There is no current activity.");
        }
    }
}