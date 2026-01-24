using System.IO;
using System.Text.RegularExpressions;

namespace Aion.Util;

public record WorkflowName(string Value)
{
    // note: group\workflow.json --> group.workflow

    // note:
    // Regex for characters that are "unreserved" in a URI (per RFC 3986) and don't need escaping.
    // This includes alphanumeric characters, hyphen, period, underscore, and tilde.
    // The Regex is compiled for better performance since it will be reused.
    private static readonly Regex MatchesUrlSafeChars = new("^[a-zA-Z0-9._~-]+$", RegexOptions.Compiled);

    public bool IsUrlSafe => MatchesUrlSafeChars.IsMatch(Value);

    public string ToPath()
    {
        var path = Value.Replace('.', Path.DirectorySeparatorChar);
        return Path.ChangeExtension(path, "json");
    }

    public override string ToString() => Value;

    public static implicit operator string(WorkflowName workflowName) => workflowName.ToString();

    public static WorkflowName FromPath(string path)
    {
        path = Path.ChangeExtension(path, null);
        return new WorkflowName(path.Replace(Path.DirectorySeparatorChar, '.'));
    }
}