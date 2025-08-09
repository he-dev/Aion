using System;
using System.IO;
using System.Threading.Tasks;
using Aion.Core.Services;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Quartz;

namespace Aion.Core;

public class WorkflowMatch(Profile profile, string pathWithinProfile)
{
    public Profile Profile => profile;

    public string PathWithinProfile => pathWithinProfile;

    public string Path => System.IO.Path.Join(Profile.Path, PathWithinProfile);

    public WorkflowName Name => new(PathWithinProfile);

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