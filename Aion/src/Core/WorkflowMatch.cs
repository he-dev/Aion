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
    private WorkflowTemplate? _template;

    public Profile Profile => profile;

    public string PathWithinProfile => pathWithinProfile;

    public string Path => System.IO.Path.Join(Profile.Path, PathWithinProfile);

    public WorkflowName Name => new(PathWithinProfile);

    public WorkflowTemplate Template => _template ?? throw new InvalidOperationException("Workflow has not been loaded yet.");

    // meta: The parameter makes testing easy.
    // public async Task<WorkflowMatch> Load(WorkflowTemplate? template = null)
    // {
    //     _template = template ?? await WorkflowTemplate.FromFile(Path);
    //     return this;
    // }

    // meta: Creating workflow-matches for tests is easier this way.
    internal static async Task<WorkflowMatch> Fake(Profile profile, string pathWithinProfile, WorkflowTemplate template)
    {
        //return await new WorkflowMatch(profile, pathWithinProfile).Load(template);
        return new WorkflowMatch(profile, pathWithinProfile);
    }
}

public record WorkflowName(string PathWithinProfile)
{
    public bool IsUrlSafe => Render().IsUrlSafe();

    private string Render() => Path.ChangeExtension(PathWithinProfile, null).Replace(Path.DirectorySeparatorChar, '.');

    public override string ToString() => Render().EnsureUrlSafe();

    public static implicit operator string(WorkflowName workflowName) => workflowName.ToString();
}