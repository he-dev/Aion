using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Util.Quartz;
using AionApi.Workflows;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace AionApi.Controllers;

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
    public async Task<IActionResult> GetAll()
    {
        var results = new List<object>();
        await foreach (var trigger in workflowSchedules.ConfigureAwait(false))
        {
            results.Add(new
            {
                name = trigger.JobKey.Name,
                cron = ((ICronTrigger)trigger).CronExpressionString,
                next = trigger.FiresAt(trigger.StartTimeUtc).Take(3)
            });
        }

        return Ok(results);
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        await foreach (var trigger in workflowSchedules.ConfigureAwait(false))
        {
            if (trigger.JobKey.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return Ok(new
                {
                    name = trigger.JobKey.Name,
                    cron = ((ICronTrigger)trigger).CronExpressionString,
                    next = trigger.FiresAt(trigger.StartTimeUtc).Take(3)
                });
            }
        }

        return NotFound(new { name });
    }

    [HttpPost("{name}/start/now")]
    public async Task<IActionResult> StartNow(string name)
    {
        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            return Ok(new { workflow = name, Next = await workflowSchedule.StartNow(workflow.Name) });
        }

        return NotFound(new { workflow = name });
    }

    [HttpPost("{name}/start/later/{delay:int?}")]
    public async Task<IActionResult> StartLater(string name, int delay = 30)
    {
        // todo: run the specified workflow; Delayed or immediately?

        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            return Ok(new { workflow = name, Next = await workflowSchedule.StartLater(workflow.Name, delay) });
        }

        return NotFound(new { workflow = name });
    }

    // !! The API needs to be able to reload workflows and reschedule them if necessary.
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