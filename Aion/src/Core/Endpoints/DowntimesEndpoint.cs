using System;
using System.Threading.Tasks;
using Aion.Core.Endpoints.Filters;
using Aion.Core.Services.Commands;
using Aion.Core.Services.Queries;
using Aion.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Core.Endpoints;

public static class DowntimesEndpoint
{
    public static void MapDowntimes(this WebApplication app)
    {
        var downtimes = app
            .MapGroup("api/profiles/{profileName}")
            .AddEndpointFilter<EnsureProfileExists>();
        downtimes.MapPost("downtimes", GetDowntimes);
        downtimes.MapPost("downtimes:start-now", StartNow);
        downtimes.MapPost("downtimes:start-in", StartIn);
        downtimes.MapPost("downtimes:start-at", StartAt);
        downtimes.MapPost("downtimes:end-now", EndNow);
    }

    private static async Task<IResult> GetDowntimes(GetDowntimes getDowntimes, string profileName)
    {
        var workflows = await getDowntimes.Invoke(profileName);
        return Results.Ok(workflows);
    }

    private static async Task<IResult> StartNow(StartDowntime startDowntime, string profileName, [FromBody] DowntimeStartInBody body)
    {
        try
        {
            var workflows = await startDowntime.Now(profileName, body.Filter, WorkflowDowntime.StartsIn(TimeSpan.Zero, body.Duration));
            return Results.Accepted($"/api/profiles/{profileName}/downtimes", new { workflows });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    private static async Task<IResult> StartIn(StartDowntime startDowntime, string profileName, [FromBody] DowntimeStartInBody body)
    {
        try
        {
            var workflows = await startDowntime.Now(profileName, body.Filter, WorkflowDowntime.StartsIn(body.Wait, body.Duration));
            return Results.Accepted($"/api/profiles/{profileName}/downtimes", new { workflows });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    private static async Task<IResult> StartAt(StartDowntime startDowntime, string profileName, [FromBody] DowntimeStartAtBody body)
    {
        try
        {
            var workflows = await startDowntime.Now(profileName, body.Filter, WorkflowDowntime.StartsAt(body.StartsAtUtc, body.EndsAtUtc));
            return Results.Accepted($"/api/profiles/{profileName}/downtimes", new { workflows });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    private static async Task<IResult> EndNow(EndDowntime endDowntime, string profileName, [FromBody] DowntimeEndNowBody body)
    {
        var workflows = await endDowntime.Now(profileName, body.Filter);
        return Results.Ok(workflows);
    }
}

public record DowntimeStartInBody
{
    public string Filter { get; init; } = null!;

    public TimeSpan Wait { get; init; }
    public TimeSpan Duration { get; init; }
}

public record DowntimeStartAtBody
{
    public string Filter { get; init; } = null!;

    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }

    public DateTimeOffset StartsAtUtc => StartsAt.ToUniversalTime();
    public DateTimeOffset EndsAtUtc => EndsAt.ToUniversalTime();
}

public record DowntimeEndNowBody
{
    public string Filter { get; init; } = null!;
}