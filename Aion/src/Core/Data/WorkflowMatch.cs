using System.IO;
using System.Threading.Tasks;
using Aion.Core.Flow;

namespace Aion.Core.Data;

public class WorkflowMatch(Profile profile, string pathWithinProfile)
{
    public Profile Profile => profile;

    public string PathWithinProfile => pathWithinProfile;

    public string Path => System.IO.Path.Join(Profile.Path, PathWithinProfile);

    public WorkflowName Name => new(PathWithinProfile);

    // meta: It is necessary to override this method in tests to use test-templates.
    public virtual async Task<WorkflowTemplate> Load()
    {
        return await WorkflowTemplate.FromFile(Path);
    }
}

public record WorkflowName(string PathWithinProfile)
{
    public bool IsUrlSafe => Render().IsUrlSafe();

    private string Render() => Path.ChangeExtension(PathWithinProfile, null).Replace(Path.DirectorySeparatorChar, '.');

    public override string ToString() => Render().EnsureUrlSafe();

    public static implicit operator string(WorkflowName workflowName) => workflowName.ToString();
}