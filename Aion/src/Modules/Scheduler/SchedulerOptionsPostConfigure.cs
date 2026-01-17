using System.Collections.Immutable;
using Aion.Modules.Services.Templates;
using Aion.Modules.Templates;
using Microsoft.Extensions.Options;

namespace Aion.Modules.Scheduler;

public class SchedulerOptionsPostConfigure : IPostConfigureOptions<SchedulerOptions>
{
    public void PostConfigure(string? optionsName, SchedulerOptions scheduler)
    {
        // core: Render the path of each profile.
        foreach (var (profileName, profile) in scheduler.Profiles)
        {
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
                    //Path = profile.Path,
                    Name = profile.Name,
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