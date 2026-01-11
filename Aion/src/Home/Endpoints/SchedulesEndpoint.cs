using System.Linq;
using System.Threading.Tasks;
using Aion.Home.Endpoints.Filters;
using Aion.Util.Core.Commands.Schedules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Home.Endpoints;

public static class SchedulesEndpoint
{
    public static void MapSchedules(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}/schedules").AddEndpointFilter<ProfileExistenceFilter>();
        workflows.MapGet("", GetSchedules);
    }

    private static async Task<IResult> GetSchedules(GetWorkflowsTriggers getWorkflowsTriggers, string profileName, [FromQuery(Name = "q")] string? workflowFilter)
    {
        var workflows = await getWorkflowsTriggers.Invoke(profileName).ToResponse(workflowFilter).ToListAsync();
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