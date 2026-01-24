namespace Aion.Util;

public record WorkflowSearchCriteria(Profile Profile, string Value)
{
    public const string Any = "*";

    public string Path => System.IO.Path.Join(Profile.Path, WorkflowPath.DefaultName);

    public string Pattern => $"{Value.Replace('.', System.IO.Path.DirectorySeparatorChar)}.json";

    public static implicit operator string(WorkflowSearchCriteria searchCriteria) => searchCriteria.Pattern;

    public record All(Profile Profile) : WorkflowSearchCriteria(Profile, Any);

    public static WorkflowSearchCriteria Where(Profile profile, string? pattern = null)
    {
        return
            pattern is null
                ? new All(profile)
                : new WorkflowSearchCriteria(profile, pattern);
    }
}