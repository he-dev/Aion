using System;
using System.Linq;
using Aion.Util.Scriban;
using Microsoft.Extensions.Options;

namespace Aion.Core;

public record EngineOptions
{
    public const string SectionName = "Aion";

    public string Instance { get; init; } = null!;

    public bool SyncOn { get; init; }

    public Profile[] Profiles { get; init; } = null!;

    public Profile this[string name]
    {
        get { return Profiles.SingleOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new ProfileNotFoundException(name); }
    }

    public class EnsuresPathsUniqueness : IValidateOptions<EngineOptions>
    {
        public ValidateOptionsResult Validate(string? name, EngineOptions options)
        {
            var duplicatePaths =
                options
                    .Profiles
                    .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

            if (duplicatePaths.Any())
            {
                var duplicatePathsString = string.Join(" | ", duplicatePaths);
                return ValidateOptionsResult.Fail($"Duplicate profile paths found: [{duplicatePathsString}]. All profile paths must be unique.");
            }

            return ValidateOptionsResult.Success;
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

public class ProfileNotFoundException(string profileName) : Exception($"Profile '{profileName}' not found.");


public static class JobDataKeys
{
    public const string ProfileName = nameof(ProfileName);
    public const string WorkflowName = nameof(WorkflowName);
}