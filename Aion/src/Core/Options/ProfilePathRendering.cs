using System.Collections.Immutable;
using Aion.Core.Workflows;
using Aion.Util.Templates;
using Microsoft.Extensions.Options;

namespace Aion.Core.Options;

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
                new ProfileVariableGroup(profile.Variables)
                {
                    Name = profile.Name,
                    Path = profile.Path,
                },
            ]);

            profile.Path = profile.Path.Render(variables);
            profile.LoggingFor = (mode) =>
            {
                variables = variables.Add(new ExecutionVariableGroup { Mode = mode });
                return profile.Logging?.ToJsonObject().RenderFilePaths(variables);
            };
        }
    }
}