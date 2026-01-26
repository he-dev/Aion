using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Endpoints.Filters;
using Aion.Core.Services;
using Aion.Core.Services.Profiles;
using Aion.Core.Services.Workflows;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Core.Endpoints;

public static class WorkflowsEndpoint
{
    public static void MapWorkflows(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}").AddEndpointFilter<EnsureProfileExists>();
        workflows.MapGet("workflows", GetWorkflows);
        workflows.MapPost("workflows/{workflowName}:start-now", StartNow);
        workflows.MapPost("workflows/{workflowName}:start-in", StartIn);
        workflows.MapPost("workflows/{workflowName}:start-at", StartAt);
        workflows.MapPost("workflows:sync", Synchronize);
    }

    private static async Task<IResult> GetWorkflows(GetWorkflowsInfo getWorkflowsInfo, string profileName, [FromQuery(Name = "q")] string? workflowFilter)
    {
        var workflows = await getWorkflowsInfo.Invoke(profileName, workflowFilter);
        return Results.Ok(workflows);
    }

    private static async Task<IResult> StartNow(ExecuteWorkflow executeWorkflow, string profileName, string workflowName, [FromBody] WorkflowStartNowBody? body)
    {
        var stepResults = await executeWorkflow.Now(profileName, workflowName, body?.Steps.ToImmutableList());
        var results =
            from stepResult in stepResults
            select new
            {
                step = stepResult.Index,
                stepResult.ExitCode,
                Status = stepResult.Status.ToString(),
                stepResult.Duration,
                stepResult.Exception?.Message
            };
        return Results.Ok(new { profileName, workflowName, results });
    }

    private static async Task<IResult> StartIn(SynchronizeWorkflow synchronizeWorkflow, string profileName, string workflowName, [FromBody] WorkflowStartInBody body)
    {
        var trigger = CreateTrigger.Simple(profileName, workflowName, DateTimeOffset.UtcNow + body.Wait);
        var scheduledFor = await synchronizeWorkflow.Invoke(profileName, workflowName, trigger);
        return Results.Accepted($"/api/profiles/{profileName}/workflows/{workflowName}", new { scheduledFor });
    }

    private static async Task<IResult> StartAt(SynchronizeWorkflow synchronizeWorkflow, string profileName, string workflowName, [FromBody] WorkflowStartAtBody body)
    {
        var trigger = CreateTrigger.Simple(profileName, workflowName, body.WhenUtc);
        var synchronizationResult = await synchronizeWorkflow.Invoke(profileName, workflowName, trigger);
        return Results.Accepted($"/api/profiles/{profileName}/workflows/{workflowName}", new { scheduledFor = synchronizationResult });
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