using System.IO;

namespace Aion.Util;

public record WorkflowPath(string ProfilePath, WorkflowName WorkflowName)
{
    public const string DefaultName = "Workflows";

    public string ProfileName => Path.GetFileName(ProfilePath);

    public override string ToString()=> Path.Join(ProfilePath, DefaultName, WorkflowName.ToPath());

    public static implicit operator string(WorkflowPath workflowPath)  => workflowPath.ToString();
}