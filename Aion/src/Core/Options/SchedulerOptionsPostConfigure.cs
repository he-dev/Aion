using System.Collections.Immutable;
using Aion.Core.Workflows;
using Aion.Util.Templates;
using Microsoft.Extensions.Options;

namespace Aion.Core.Options;

public class SchedulerOptionsPostConfigure : IPostConfigureOptions<SchedulerOptions>
{
    public void PostConfigure(string? optionsName, SchedulerOptions scheduler)
    {
        // core: Render the path of each profile.
        foreach (var (profileName, profile) in scheduler.Profiles)
        {
            // core: Set the profile name.
            profile.Name = profileName;

            // note: Other variables are unknown at this stage, so only ENV is supported.
            var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
            ([
                new GlobalVariableGroup(scheduler.Variables),
                new GlobalVariableGroup(profile.Variables),
                new SchedulerVariableGroup
                {
                    Name = scheduler.Name,
                },
                new ProfileVariableGroup
                {
                    Name = profile.Name,
                    Path = profile.Path,
                },
            ]);

            profile.Path = profile.Path.Render(variables);
            // profile.LoggingFor = (mode) =>
            // {
            //     variables = variables.Add(new ExecutionVariableGroup { Mode = mode });
            //     return profile.Logging?.ToJsonObject().RenderFilePaths(variables);
            // };
        }
    }
}