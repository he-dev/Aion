using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aion.Util.Templates;

namespace Aion.Util;

public class ContextVariableGroup(IEnumerable<KeyValuePair<string, string>>? variables) : TemplateVariableGroup("Parameters")
{
    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return
            (variables ?? [])
                .Select(variable => new KeyValuePair<string, object?>(variable.Key, variable.Value))
                .GetEnumerator();
    }
}

public class SchedulerVariableGroup() : TemplateVariableGroup("Scheduler")
{
    public required string Name { get; init; } = null!;

    //public string? Directory => Path.GetDirectoryName(Environment.ProcessPath);

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        //yield return new KeyValuePair<string, object?>(nameof(Directory), Directory);
    }
}

public class ProfileVariableGroup() : TemplateVariableGroup("Profile")
{
    public required string Name { get; init; } = null!;

    public required string Path { get; init; } = null!;

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Path), Path);
    }
}

public class WorkflowVariableGroup() : TemplateVariableGroup("Workflow")
{
    public required string Name { get; init; }

    public WorkflowMode Mode { get; init; }

    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, object?>(nameof(Name), Name);
        yield return new KeyValuePair<string, object?>(nameof(Mode), Mode);
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
    }
}