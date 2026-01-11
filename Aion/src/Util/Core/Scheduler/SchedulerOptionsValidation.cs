using System;
using System.IO;
using Microsoft.Extensions.Options;

namespace Aion.Util.Core.Scheduler;

public class SchedulerOptionsValidation : IValidateOptions<SchedulerOptions>
{
    public ValidateOptionsResult Validate(string? optionsName, SchedulerOptions options)
    {
        foreach (var (profileName, profile) in options.Profiles)
        {
            if (string.IsNullOrEmpty(profile.Path))
            {
                return ValidateOptionsResult.Fail($"Profile '{profileName}' has no path defined.");
            }

            if (!Path.Exists(profile.Path))
            {
                return ValidateOptionsResult.Fail($"Profile '{profileName}' path '{profile.Path}' does not exist.");
            }

            if (!Path.GetFileName(profile.Path).Equals(profileName, StringComparison.InvariantCultureIgnoreCase))
            {
                return ValidateOptionsResult.Fail($"Profile '{profileName}' path '{profile.Path}' does not match the profile name '{profileName}'.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}