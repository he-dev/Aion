using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Core.Providers;
using Aion.Core.Schedulers;
using Aion.Util;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api/[controller]/profiles")]
public class WorkflowsController
(
    ILogger<WorkflowsController> logger,
    FindsWorkflows findsWorkflows,
    ProfileProvider profileProvider,
    SchedulesWorkflowExecution schedulesWorkflowExecution
) : ControllerBase
{
    [HttpGet("{profile}")]
    public async Task<IActionResult> Get(string profile, [FromQuery(Name = "q")] string? filter, [FromQuery] bool? isOn = null)
    {
        await profileProvider.FindLoggerProfile("foo", "bar");

        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;
        var errors = ImmutableList<object>.Empty;
        foreach (var filePath in findsWorkflows.Where(profile, filter ?? FileFilter.Any, FileExtension.Json))
        {
            try
            {
                workflows = workflows.Add(await Workflow.FromFile(filePath));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load workflow from '{WorkflowPath}'.", filePath);
                errors = errors.Add(new { path = filePath, exception = ex.ToString() });
            }
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var result =
            from workflow in workflows
            let next = workflow.CreatesCronTrigger(profile).FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            orderby next.FirstOrDefault(), workflow.Name
            select new
            {
                path = workflow.Path,
                isOn = workflow.IsOn,
                cron = workflow.Cron,
                next = next,
                jobs = workflow.Steps.Count(s => s.IsOn),
            };

        return Ok(new { result, errors });
    }

    // core: This API can synchronize workflows outside the regular synchronization schedule.
    // core: Does not use the synchronization-job here because we want to see the results immediately in the response.
    [HttpPost("{profile}:sync")]
    public async Task<IActionResult> Synchronize(string profile)
    {
        var result = ImmutableList<object>.Empty;
        var errors = ImmutableList<object>.Empty;

        foreach (var path in findsWorkflows.Where(profile, FileFilter.Any, FileExtension.Json))
        {
            try
            {
                if (await Workflow.FromFile(path) is { } workflow)
                {
                    var (sync, next) = await schedulesWorkflowExecution.For(workflow, profile);
                    result = result.Add(new
                    {
                        path,
                        sync,
                        next = next?.ToLocalTime(),
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowPath}'.", path);
                errors = errors.Add(new
                {
                    path,
                    exception = ex.ToString()
                });
            }
        }

        return Ok(new
        {
            result,
            errors
        });
    }

    [HttpPost("{profile}/{name}:startNow")]
    public async Task<IActionResult> StartNow(string profile, string name)
    {
        return await Start(profile, name, async workflow => await schedulesWorkflowExecution.Now(workflow));
    }

    [HttpPost("{profile}/{name}:startIn")]
    public async Task<IActionResult> StartIn(string profile, string name, [FromBody] StartInBody body)
    {
        return await Start(profile, name, async workflow => await schedulesWorkflowExecution.In(workflow, body.Wait));
    }

    [HttpPost("{profile}/{name}:startAt")]
    public async Task<IActionResult> StartAt(string profile, string name, [FromBody] StartAtBody body)
    {
        var startAt = body.When.UseTimeZoneOffsetOrLocal().ToUniversalTime();
        return await Start(profile, name, async workflow => await schedulesWorkflowExecution.At(workflow, startAt));
    }

    private async Task<IActionResult> Start(string profile, string name, Func<Workflow, Task<DateTimeOffset>> action)
    {
        try
        {
            var fileName = findsWorkflows.Where(profile, name, FileExtension.Json).SingleOrThrows();
            var workflow = await Workflow.FromFile(fileName);
            var next = await action(workflow);
            return Ok(new { name, next = next.ToLocalTime() });
        }
        catch (CollectionEmptyException ex)
        {
            return NotFound(new { name }); // todo: say why
        }
        catch (AmbiguousResultException ex)
        {
            return NotFound(new { name }); // todo: say why
        }
        catch (WorkflowNotFoundException)
        {
            return NotFound(new { name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule workflow '{WorkflowName}'.", name);
            return Problem(
                detail: ex.ToString(),
                title: $"Unable to schedule workflow '{name}'.",
                statusCode: 500,
                instance: Request.Path
            );
        }
    }

    public record StartInBody
    {
        public TimeSpan Wait { get; init; }
    }

    // note: Does not validate the input because the scheduler does that already.
    public record StartAtBody
    {
        public DateTimeOffset When { get; init; }
    }
}