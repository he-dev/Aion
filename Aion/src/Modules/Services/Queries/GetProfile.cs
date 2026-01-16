using Aion.Modules.Scheduler;
using Microsoft.Extensions.Options;

namespace Aion.Modules.Services.Queries;

public class GetProfile(IOptions<SchedulerOptions> schedulerOptions)
{
    public Profile Where(string profileName) => schedulerOptions.Value.Profiles[profileName];
}