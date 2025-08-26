namespace Aion.Core.Workflows;

public record WorkflowMatch(Profile Profile, WorkflowFilter Filter, string PathWithinProfile)
{
    public string Path => System.IO.Path.Join(Profile.Path, PathWithinProfile);

    public WorkflowName Name => new(PathWithinProfile);
}