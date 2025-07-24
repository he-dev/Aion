using System;
using System.Diagnostics.CodeAnalysis;
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

    public class RenderPaths : IPostConfigureOptions<EngineOptions>
    {
        public void PostConfigure(string? name, EngineOptions options)
        {
            // core: Render the path of each profile.
            foreach (var profile in options.Profiles)
            {
                profile.Path = VariableTemplate.Render(profile.Path, []);
            }
        }
    }
}

public static class EngineOptionsExtensions
{
    public static bool TryGetProfile(this EngineOptions options, string name, [MaybeNullWhen(false)] out ProfileInfo profile)
    {
        if (options.Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } result)
        {
            profile = result;
            return true;
        }

        profile = null;
        return false;
    }
}

public record ProfileInfo
{
    public string Name { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string Sync { get; set; } = null!;
}

public static class JobDataKeys
{
    public const string WorkflowPath = nameof(WorkflowPath);
    public const string ProfileName = nameof(ProfileName);
    public const string ProfilePath = nameof(ProfilePath);
}