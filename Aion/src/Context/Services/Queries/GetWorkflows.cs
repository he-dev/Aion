using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Modules;
using Aion.Modules.Scheduler;
using Aion.Modules.Services;
using Aion.Toolbox.Quartz;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Context.Services.Queries;

public class GetWorkflows
(
    ILogger<GetWorkflows> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions,
    CreateWorkflow createWorkflow
)
{
    public async Task<object> Invoke(string profileName, string? workflowFilter, bool? enabled = null)
    {
        var profile = schedulerOptions.Value.Profiles[profileName];

        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;
        var workflowFailure = ImmutableList<object>.Empty;
        var workflowPaths = workflowFilter is not null ? profile.Workflows.Where(workflowFilter) : profile.Workflows.All();
        foreach (var workflowPath in workflowPaths)
        {
            try
            {
                if (workflowPath.WorkflowName.IsUrlSafe)
                {
                    var workflowTemplate = await WorkflowConfiguration.FromFile(workflowPath);
                    var workflow = await createWorkflow.From(workflowTemplate);
                    workflows = workflows.Add(workflow);
                    logger.LogDebug("Successfully loaded workflow from '{WorkflowPath}'.", workflowPath);
                }
                else
                {
                    workflowFailure = workflowFailure.Add(new { path = workflowPath.WorkflowName.RelativePath, issue = "Workflow name is not url-safe." });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load workflow from '{WorkflowPath}'.", workflowPath);
                workflowFailure = workflowFailure.Add(new { path = workflowPath.WorkflowName.RelativePath, issue = ex.ToString() });
            }
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var results =
            from workflow in workflows
            let next = workflow.Trigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            orderby next.FirstOrDefault(), workflow.Name
            select new
            {
                name = workflow.Name,
                path = workflow.Path,
                isOn = workflow.Enabled,
                cron = ((ICronTrigger)workflow.Trigger).CronExpressionString,
                next = next,
                steps = new
                {
                    workflow.Steps.Count,
                    enabled = workflow.Steps.Where(s => s.Enabled).Select(s => s.Index)
                }
            };

        return new
        {
            profile = new
            {
                schedulerOptions.Value.Profiles[profileName].Name,
                schedulerOptions.Value.Profiles[profileName].Path,
            },
            result = results,
            issues = workflowFailure
        };
    }
}

public abstract class CommandExceptionHandler<TException> : IExceptionHandler where TException : Exception
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is TException)
        {
            var response = Evaluate(exception);
            httpContext.Response.StatusCode = response.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(new { error = response.Message }, cancellationToken);
            return true;
        }

        return false;
    }

    protected abstract (int StatusCode, string Message) Evaluate(Exception exception);
}

public class WorkflowNotExecutableExceptionHandler : CommandExceptionHandler<WorkflowNotExecutableException>
{
    protected override (int StatusCode, string Message) Evaluate(Exception exception) => (StatusCodes.Status422UnprocessableEntity, exception.Message);
}

