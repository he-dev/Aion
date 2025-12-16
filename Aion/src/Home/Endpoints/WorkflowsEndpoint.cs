using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Commands.Workflows;
using Aion.Core.Mvc;
using Aion.Core.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Home.Endpoints;

public static class WorkflowsEndpoint
{
    public static void MapWorkflows(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}").AddEndpointFilter<ProfileExistenceFilter>();
        workflows.MapGet("workflows", GetWorkflows);
        workflows.MapPost("workflows/{workflowName}:start-now", StartNow);
        workflows.MapPost("workflows/{workflowName}:start-in", StartIn);
        workflows.MapPost("workflows/{workflowName}:start-at", StartAt);
        workflows.MapPost("workflows:sync", Synchronize);
    }

    private static async Task<IResult> GetWorkflows(GetWorkflows command, string profileName, [FromQuery(Name = "q")] string? workflowFilter)
    {
        var workflows = await command.Invoke(profileName, workflowFilter);
        return Results.Ok(workflows);
    }

    private static async Task<IResult> StartNow(ExecuteWorkflow executeWorkflow, string profileName, string workflowName, [FromBody] WorkflowStartNowBody? body)
    {
        var stepResults = await executeWorkflow.Now(profileName, workflowName, body?.Steps.ToImmutableList());
        var results =
            from stepResult in stepResults
            select new
            {
                stepResult.Step.Index,
                stepResult.Step.Name,
                stepResult.ExitCode,
                stepResult.Status,
                stepResult.Duration,
                stepResult.Exception?.Message
            };
        return Results.Ok(new { stepResults = results });
    }

    private static async Task<IResult> StartIn(ScheduleWorkflow command, string profileName, string workflowName, [FromBody] WorkflowStartInBody body)
    {
        var scheduledFor = await command.Invoke(profileName, workflowName, DateTimeOffset.UtcNow + body.Wait);
        return Results.Accepted($"/api/profiles/{profileName}/workflows/{workflowName}", new { scheduledFor });
    }

    private static async Task<IResult> StartAt(ScheduleWorkflow command, string profileName, string workflowName, [FromBody] WorkflowStartAtBody body)
    {
        var scheduledFor = await command.Invoke(profileName, workflowName, body.WhenUtc);
        return Results.Accepted($"/api/profiles/{profileName}/workflows/{workflowName}", new { scheduledFor });
    }

    private static async Task<IResult> Synchronize(SynchronizeProfile synchronizeProfile, string profileName)
    {
        var syncResults = await synchronizeProfile.Invoke(profileName);
        return Results.Ok(syncResults);
    }
}

public record WorkflowStartNowBody
{
    public StepIdentifier[] Steps { get; init; } = [];
}

public record WorkflowStartInBody
{
    public TimeSpan Wait { get; init; }
}

// note: Does not validate the input because the scheduler does that already.
public record WorkflowStartAtBody
{
    public DateTime When { get; init; }

    public DateTimeOffset WhenUtc => When.ToUniversalTime();
}