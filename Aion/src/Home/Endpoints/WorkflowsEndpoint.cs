using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Core.Commands.Workflows;
using Aion.Core.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Home.Endpoints;

public static class WorkflowsEndpoint
{
    public static void MapWorkflows(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}");
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

    private static async Task<IResult> StartNow(ExecuteWorkflow executeWorkflow, string profileName, string workflowName, [FromBody] StartWorkflowBody body)
    {
        var stepResults = await executeWorkflow.Now(profileName, workflowName, WorkflowMode.User, body.Steps.ToImmutableList());
        return Results.Accepted($"/api/profiles/{profileName}/workflows/{workflowName}", new { stepResults });
    }

    private static async Task<IResult> StartIn(ScheduleWorkflow command, string profileName, string workflowName, [FromBody] StartWorkflowInBody body)
    {
        var scheduledFor = await command.Invoke(profileName, workflowName, DateTimeOffset.UtcNow + body.Wait);
        return Results.Accepted($"/api/profiles/{profileName}/workflows/{workflowName}", new { scheduledFor });
    }

    private static async Task<IResult> StartAt(ScheduleWorkflow command, string profileName, string workflowName, [FromBody] StartWorkflowAtBody body)
    {
        var scheduledFor = await command.Invoke(profileName, workflowName, body.WhenUtc);
        return Results.Accepted($"/api/profiles/{profileName}/workflows/{workflowName}", new { scheduledFor });
    }

    private static async Task<IResult> Synchronize(SynchronizeWorkflows command, string profileName)
    {
        var workflows = await command.Invoke(profileName);
        return Results.Ok(workflows);
    }
}

public record StartWorkflowBody
{
    public StepIdentifier[] Steps { get; init; } = null!;
}

public record StartWorkflowInBody
{
    public TimeSpan Wait { get; init; }
}

// note: Does not validate the input because the scheduler does that already.
public record StartWorkflowAtBody
{
    public DateTime When { get; init; }

    public DateTimeOffset WhenUtc => When.ToUniversalTime();
}
