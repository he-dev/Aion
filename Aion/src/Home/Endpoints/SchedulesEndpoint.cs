using System.Threading.Tasks;
using Aion.Core.Commands.Schedules;
using Aion.Core.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Home.Endpoints;

public static class SchedulesEndpoint
{
    public static void MapSchedules(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}/schedules")
            .AddEndpointFilter<ProfileExistenceFilter>();
        workflows.MapPost("", GetSchedules);
    }

    private static async Task<IResult> GetSchedules(GetSchedules command, string profileName, [FromQuery(Name = "q")] string? workflowFilter)
    {
        var workflows = await command.Invoke(profileName, workflowFilter);
        return Results.Ok(workflows);
    }
}