using System.Collections.Immutable;
using Aion.Util.Scriban;
using Microsoft.Extensions.Options;

namespace Aion.Core.Services.Options;

public class RendersProfilePath : IPostConfigureOptions<EngineOptions>
{
    public void PostConfigure(string? name, EngineOptions options)
    {
        // core: Render the path of each profile.
        foreach (var profile in options.Profiles)
        {
            // note: Other variables are unknown at this stage, so only ENV is supported.
            profile.Path = RendersTemplates.In(profile.Path, ImmutableList<VariableGroup>.Empty.AddRange
            ([
                new EngineVariableGroup(options.Variables) { Name = options.Instance },
                new ProfileVariableGroup(profile.Variables) { Name = profile.Name },
            ]));
        }
    }
}