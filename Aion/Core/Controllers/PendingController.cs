using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Aion.Util.Quartz;
using Aion.Workflows;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Aion.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class PendingController
(
    WorkflowDirectory workflowDirectory,
    WorkflowSchedule workflowSchedule,
    WorkflowSchedule.Collection workflowSchedules
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "q")] string? filter)
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

    [HttpPost("{name}/start/now")]
    public async Task<IActionResult> StartNow(string name)
    {
        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            return Ok(new
            {
                name,
                next = await workflowSchedule.StartNow(workflow.Name)
            });
        }

        return NotFound(new { name });
    }

    [HttpPost("{name}/start/later/{delaySeconds:int?}")]
    public async Task<IActionResult> StartLater(string name, int delaySeconds = 30)
    {
        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            return Ok(new
            {
                name,
                next = await workflowSchedule.StartLater(workflow.Name, delaySeconds)
            });
        }

        return NotFound(new { workflow = name });
    }

    // !! The API needs to be able to synchronize workflows outside its regular schedule.
    [HttpPost("sync")]
    public async Task<IActionResult> Synchronize()
    {
        var results = new List<WorkflowSchedule.SynchronizationResult>();
        await foreach (var workflow in workflowDirectory)
        {
            if (await workflowSchedule.Synchronize(workflow) is { } result)
            {
                results.Add(result);
            }
        }

        return Ok(results);
    }
}