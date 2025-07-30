using System;
using System.Linq;
using Aion.Util.Scriban;
using Microsoft.Extensions.Options;

namespace Aion.Core;

public record EngineOptions
{
    public const string SectionName = "Aion";

    public string Name { get; init; } = null!;

    public bool SyncOn { get; init; }

    public ProfileInfo[] Profiles { get; init; } = null!;

    public ProfileInfo this[string name]
    {
        get
        {
            return
                Profiles
                    .SingleOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ProfileNotFoundException(name);
        }
    }

    public class RenderPaths : IPostConfigureOptions<EngineOptions>
    {
        public void PostConfigure(string? name, EngineOptions options)
        {
            // core: Render the path of each profile.
            foreach (var profile in options.Profiles)
            {
                // note: Other variables are unknown at this stage, so only ENV is supported.
                profile.Path = RendersTemplates.In(profile.Path, []);
            }
        }
    }
}

public record ProfileInfo
{
    public string Path { get; set; } = null!;

    // core: The last directory name is the name of the profile.
    // meta: Make sure it does not end with a "/" which would result in a wrong name.
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));

    public string Sync { get; set; } = null!;

    public string[] Includes { get; set; } = [];

    public string[] Excludes { get; set; } = [];
}

public static class JobDataKeys
{
    public const string ProfileName = nameof(ProfileName);
    public const string WorkflowName = nameof(WorkflowName);
    //public const string WorkflowPath = nameof(WorkflowPath);
}