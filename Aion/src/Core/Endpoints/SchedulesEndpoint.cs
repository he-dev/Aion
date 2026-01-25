using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Endpoints.Filters;
using Aion.Core.Services.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Core.Endpoints;

public static class SchedulesEndpoint
{
    public static void MapSchedules(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}/schedules").AddEndpointFilter<EnsureProfileExists>();
        workflows.MapGet("", GetSchedules);
    }

    private static async Task<IResult> GetSchedules(GetProfileTriggers getProfileTriggers, string profileName, [FromQuery(Name = "q")] string? workflowFilter)
    {
        var workflows = await getProfileTriggers.Where(profileName).FormatResponse(workflowFilter).ToListAsync();
        return Results.Ok(workflows);
    }
}

public enum OrderBy
{
    Name,
    Path,
    Cron,
    Next
}