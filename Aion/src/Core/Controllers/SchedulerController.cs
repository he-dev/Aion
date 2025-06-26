using System;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Jobs;
using Aion.Core.Workflows;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class SchedulerController(ILogger<SchedulerController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromServices] WorkflowSchedule workflowSchedule,
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        [FromQuery(Name = "q")] string? filter
    )
    {
        var utcNow = DateTimeOffset.UtcNow;

        var results =
            await workflowSchedules
                .Where(trigger => filter is null || FileSystemName.MatchesSimpleExpression(filter, trigger.JobKey.Name))
                .Select(trigger => new
                {
                    name = trigger.JobKey.Name,
                    cron = ((ICronTrigger)trigger).CronExpressionString,
                    next = ((ICronTrigger)trigger).FiresAt(utcNow).Take(3)
                })
                .OrderBy(next => next.next.FirstOrDefault())
                .ThenBy(item => item.name)
                .ToListAsync();

        return
            results.Any()
                ? Ok(results)
                : NotFound(new { filter });
    }


    // [HttpPost("{name}/start/{delaySeconds:int?}")]
    // public async Task<IActionResult> Start(string name, int delaySeconds = 0)
    // {
    //     if (await workflowDirectory.Find(name) is { } workflow)
    //     {
    //         var next =
    //             delaySeconds <= 0
    //                 ? await workflowSchedule.StartNow(workflow)
    //                 : await workflowSchedule.StartLater(workflow, delaySeconds);
    //
    //         return Ok(new { name, next });
    //     }
    //
    //     logger.LogDebug("Workflow '{name}' not found.", name);
    //
    //     return NotFound(new { name });
    // }
}