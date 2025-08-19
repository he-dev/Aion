using System.Collections.Immutable;
using Aion.Core.Entities;
using Aion.Util.Services;
using Microsoft.Extensions.Options;

namespace Aion.Core.Services.OptionsPostConfiguration;

public class ProfilePathRendering : IPostConfigureOptions<InstanceOptions>
{
    public void PostConfigure(string? name, InstanceOptions instance)
    {
        // core: Render the path of each profile.
        foreach (var profile in instance.Profiles)
        {
            // note: Other variables are unknown at this stage, so only ENV is supported.
            var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
            ([
                new InstanceVariableGroup(instance.Variables) { Name = instance.Name },
                new ProfileVariableGroup(profile.Variables) { Name = profile.Name },
            ]);

            profile.Path = profile.Path.Render(variables);

            profile.RenderLogging = (mode) =>
            {
                variables = variables.Add(new ExecutionVariableGroup { Mode = mode });
                return profile.Logging?.ToJsonObject().RenderFilePaths(variables);
            };
        }
    }
}