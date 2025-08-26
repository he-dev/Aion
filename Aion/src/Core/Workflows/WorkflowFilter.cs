using System.IO;

namespace Aion.Core.Workflows;

public record WorkflowFilter(string Value)
{
    public string Pattern => $"**\\{Value.Replace('.', Path.DirectorySeparatorChar)}.json";

    public static implicit operator string(WorkflowFilter filter) => filter.Pattern;

    public static implicit operator WorkflowFilter(string value) => new(value);

    public static readonly WorkflowFilter Any = new WorkflowFilter("*");
}