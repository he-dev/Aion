using System.IO;
using Microsoft.Extensions.Options;

namespace Aion.Core.Options;

public class ProfilePathValidation : IValidateOptions<SchedulerOptions>
{
    public ValidateOptionsResult Validate(string? optionsName, SchedulerOptions options)
    {
        foreach (var (profileName, profile) in options.Profiles)
        {
            if(string.IsNullOrEmpty(profile.Path)) return ValidateOptionsResult.Fail($"Profile '{profileName}' has no path defined.");
            if(!Path.Exists(profile.Path)) return ValidateOptionsResult.Fail($"Profile '{profileName}' path '{profile.Path}' does not exist.");
        }

        return ValidateOptionsResult.Success;
    }
}