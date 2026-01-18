using System.IO;

namespace Aion.Modules;

public record WorkflowPath(string ProfilePath, WorkflowName WorkflowName)
{
    public const string DefaultName = "Workflows";

    public string ProfileName => Path.GetFileName(ProfilePath);

    public override string ToString()=> Path.Join(ProfilePath, DefaultName, WorkflowName.RelativePath);

    public static implicit operator string(WorkflowPath workflowPath)  => workflowPath.ToString();
}