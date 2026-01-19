using System.Collections.Generic;
using Aion.Modules.Scheduler;
using Microsoft.Extensions.Options;

namespace Aion.Modules.Services.Queries;

public class GetProfile(IOptions<SchedulerOptions> schedulerOptions)
{
    public Profile Single(string profileName) => schedulerOptions.Value.Profiles[profileName];

    public IEnumerable<Profile> All() => schedulerOptions.Value.Profiles.Values;
}