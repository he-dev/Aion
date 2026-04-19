using System.Collections.Immutable;
using Aion.Util.Templating.Services;
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
            var variables = ImmutableList<IVariableGroup>.Empty.AddRange
            ([
                new ParametersVariableGroup(scheduler.Parameters),
                new ParametersVariableGroup(profile.Parameters),
                new SchedulerVariableGroup
                {
                    Name = scheduler.Name,
                },
                new ProfileVariableGroup
                {
                    Path = profile.Path,
                    Name = profile.Name,
                },
            ]);

            profile.Path = profile.Path.Render(variables);
        }
    }
}