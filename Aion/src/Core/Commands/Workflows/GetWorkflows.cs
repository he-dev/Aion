using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Commands.Workflows;

public class GetWorkflows
(
    ILogger<GetWorkflows> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions,
    RenderWorkflow renderWorkflow
)
{
    public async Task<object> Invoke(string profileName, string? workflowFilter, bool? enabled = null)
    {
        var profile = schedulerOptions.Value.Profiles[profileName];

        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;
        var workflowFailure = ImmutableList<object>.Empty;
        var matchesWorkflows = workflowFilter is not null ? profile.Workflows.Find(workflowFilter) : profile.Workflows.All();
        foreach (var workflowMatch in matchesWorkflows)
        {
            try
            {
                if (workflowMatch.Name.IsUrlSafe)
                {
                    workflows = workflows.Add(await renderWorkflow.For(workflowMatch));
                    logger.LogDebug("Successfully loaded workflow from '{WorkflowPath}'.", workflowMatch.Path);
                }
                else
                {
                    workflowFailure = workflowFailure.Add(new { path = workflowMatch.PathWithinProfile, issue = "Workflow name is not url-safe." });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load workflow from '{WorkflowPath}'.", workflowMatch.Path);
                workflowFailure = workflowFailure.Add(new { path = workflowMatch.PathWithinProfile, issue = ex.ToString() });
            }
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var result =
            from match in workflows
            let trigger = match.CreateTrigger()
            let next = trigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            //orderby next.FirstOrDefault(), match.Name
            orderby match.Name.ToString()
            select new
            {
                path = match.Path,
                name = match.Name,
                isOn = match.Enabled,
                cron = ((ICronTrigger)trigger).CronExpressionString,
                next = next,
                jobs = match.Steps.Count(s => s.Enabled),
            };

        return new
        {
            profile = new
            {
                schedulerOptions.Value.Profiles[profileName].Name,
                schedulerOptions.Value.Profiles[profileName].Path,
            },
            result,
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

public class CommandException<TCommand>(string message, Exception? inner = null) : Exception(message, inner);