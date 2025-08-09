using System;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Aion.Core.Services.Options;

public class EnsuresProfileUniqueness : IValidateOptions<InstanceOptions>
{
    public ValidateOptionsResult Validate(string? name, InstanceOptions options)
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