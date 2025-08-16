using System.Collections.Immutable;
using Aion.Core.Entities;
using Aion.Util.Services;
using Microsoft.Extensions.Options;

namespace Aion.Core.Services.OptionsPostConfiguration;

public class ProfilePathRendering : IPostConfigureOptions<InstanceOptions>
{
    public void PostConfigure(string? name, InstanceOptions options)
    {
        // core: Render the path of each profile.
        foreach (var profile in options.Profiles)
        {
            // note: Other variables are unknown at this stage, so only ENV is supported.
            profile.Path = profile.Path.Render(ImmutableList<TemplateVariableGroup>.Empty.AddRange
            ([
                new InstanceVariableGroup(options.Variables) { Name = options.Name },
                new ProfileVariableGroup(profile.Variables) { Name = profile.Name },
            ]));
        }
    }
}