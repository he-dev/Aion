using System.IO;
using System.Text.RegularExpressions;

namespace Aion.Modules;

public record WorkflowName(string RelativePath)
{
    // note: group\workflow.json --> group.workflow

    // note:
    // Regex for characters that are "unreserved" in a URI (per RFC 3986) and don't need escaping.
    // This includes alphanumeric characters, hyphen, period, underscore, and tilde.
    // The Regex is compiled for better performance since it will be reused.
    private static readonly Regex MatchesUrlSafeChars = new("^[a-zA-Z0-9._~-]+$", RegexOptions.Compiled);

    public bool IsUrlSafe => MatchesUrlSafeChars.IsMatch(Render());

    private string Render() => Path.ChangeExtension(RelativePath, null).Replace(Path.DirectorySeparatorChar, '.');

    public override string ToString() => Render();

    public static implicit operator string(WorkflowName workflowName) => workflowName.ToString();
}