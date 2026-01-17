using System.Linq;
using System.Threading.Tasks;
using Aion.Context.Endpoints.Filters;
using Aion.Context.Services.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Context.Endpoints;

public static class SchedulesEndpoint
{
    public static void MapSchedules(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}/schedules").AddEndpointFilter<EnsureProfileExists>();
        workflows.MapGet("", GetSchedules);
    }

    private static async Task<IResult> GetSchedules(GetProfileTriggers getProfileTriggers, string profileName, [FromQuery(Name = "q")] string? workflowFilter)
    {
        var workflows = await getProfileTriggers.Invoke(profileName).FormatResponse(workflowFilter).ToListAsync();
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