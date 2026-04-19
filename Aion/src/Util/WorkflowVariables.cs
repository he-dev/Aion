using System.Collections.Generic;
using System.Diagnostics;
using Aion.Util.Templating;
using Aion.Util.Templating.Services;
using Scriban.Runtime;

namespace Aion.Util;

public class ParametersVariableGroup : ScriptObject, IVariableGroup
{
    public ParametersVariableGroup(IEnumerable<KeyValuePair<string, string>>? variables)
    {
        foreach (var (key, value) in variables ?? [])
        {
            Add(key, value);
        }
    }
}

public class SchedulerVariableGroup : PropertyScriptObject, IVariableGroup
{
    public required string Name { get; init; } = null!;
}

public class ProfileVariableGroup : PropertyScriptObject, IVariableGroup
{
    public required string Name { get; init; } = null!;
    public required string Path { get; init; } = null!;
}

public class WorkflowVariableGroup : PropertyScriptObject, IVariableGroup
{
    public required string Name { get; init; }
    public required WorkflowMode Mode { get; init; }
}

public class StepVariableGroup : PropertyScriptObject, IVariableGroup
{
    public string? Name { get; init; }
    public required int Index { get; init; }
    public ActivityTraceId TraceId => Activity.Current?.TraceId ?? default;
    public ActivitySpanId SpanId => Activity.Current?.SpanId ?? default;
    public string? ParentId => Activity.Current?.ParentId;
}