using System.Collections.Immutable;
using Aion.Util.Services.Templates;
using Aion.Util.Templates;
using Microsoft.Extensions.Options;

namespace Aion.Util.Scheduler;

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
                new ContextVariableGroup(scheduler.Parameters),
                new ContextVariableGroup(profile.Parameters),
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