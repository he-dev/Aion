using System;
using System.Threading.Tasks;
using Aion.Core.Commands.Downtimes;
using Aion.Core.Mvc;
using Aion.Core.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Home.Endpoints;

public static class DowntimesEndpoint
{
    public static void MapDowntimes(this WebApplication app)
    {
        var downtimes = app
            .MapGroup("api/profiles/{profileName}")
            .AddEndpointFilter<ProfileExistenceFilter>();
        downtimes.MapPost("downtimes", GetDowntimes);
        downtimes.MapPost("downtimes:start-now", StartDowntimeNow);
        downtimes.MapPost("downtimes:start-in", StartDowntimeIn);
        downtimes.MapPost("downtimes:start-at", StartDowntimeAt);
        downtimes.MapPost("downtimes:end", EndDowntimes);
    }

    private static async Task<IResult> GetDowntimes(GetDowntimes command, string profileName)
    {
        var workflows = await command.Invoke(profileName);
        return Results.Ok(workflows);
    }

    private static async Task<IResult> StartDowntimeNow(StartDowntime command, string profileName, [FromBody] StartDowntimeInBody body)
    {
        var workflows = await command.Invoke(profileName, body.Filter, WorkflowDowntime.StartsIn(TimeSpan.Zero, body.Duration));
        return Results.Accepted($"/api/profiles/{profileName}/downtimes", new { workflows });

    }

    private static async Task<IResult> StartDowntimeIn(StartDowntime command, string profileName, [FromBody] StartDowntimeInBody body)
    {
        var workflows = await command.Invoke(profileName, body.Filter, WorkflowDowntime.StartsIn(body.Wait, body.Duration));
        return Results.Accepted($"/api/profiles/{profileName}/downtimes", new { workflows });    }

    private static async Task<IResult> StartDowntimeAt(StartDowntime command, string profileName, [FromBody] StartDowntimeAtBody body)
    {
        var workflows = await command.Invoke(profileName, body.Filter, WorkflowDowntime.StartsAt(body.StartsAtUtc, body.EndsAtUtc));
        return Results.Accepted($"/api/profiles/{profileName}/downtimes", new { workflows });    }

    private static async Task<IResult> EndDowntimes(EndDowntime command, string profileName, [FromBody] EndDowntimeBody body)
    {
        var workflows = await command.Invoke(profileName, body.Filter);
        return Results.Ok(workflows);
    }
}

public record StartDowntimeInBody
{
    public string Filter { get; init; } = null!;

    public TimeSpan Wait { get; init; }
    public TimeSpan Duration { get; init; }
}

public record StartDowntimeAtBody
{
    public string Filter { get; init; } = null!;

    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }

    public DateTimeOffset StartsAtUtc => StartsAt.ToUniversalTime();
    public DateTimeOffset EndsAtUtc => EndsAt.ToUniversalTime();
}

public record EndDowntimeBody
{
    public string Filter { get; init; } = null!;
}