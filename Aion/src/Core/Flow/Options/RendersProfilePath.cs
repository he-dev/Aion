using System.Collections.Immutable;
using Aion.Core.Data;
using Aion.Util.Flow.Scriban;
using Microsoft.Extensions.Options;

namespace Aion.Core.Flow.Options;

public class RendersProfilePath : IPostConfigureOptions<InstanceOptions>
{
    public void PostConfigure(string? name, InstanceOptions options)
    {
        // core: Render the path of each profile.
        foreach (var profile in options.Profiles)
        {
            // note: Other variables are unknown at this stage, so only ENV is supported.
            profile.Path = RendersTemplates.In(profile.Path, ImmutableList<VariableGroup>.Empty.AddRange
            ([
                new InstanceVariableGroup(options.Variables) { Name = options.Name },
                new ProfileVariableGroup(profile.Variables) { Name = profile.Name },
            ]));
        }
    }
}