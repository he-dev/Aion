using System.IO;

namespace Aion.Modules;

public record WorkflowFilter(string Value)
{
    public const string Any = "*";

    public string Pattern => $"{Value.Replace('.', Path.DirectorySeparatorChar)}.json";

    public static implicit operator string(WorkflowFilter filter) => filter.Pattern;

    public static implicit operator WorkflowFilter(string? value) => new(value ?? Any);
}