using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Models;
using AionApi.Services;
using AionApi.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reusable.Extensions;

namespace AionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulesController : ControllerBase
{
    public SchedulesController(WorkflowStore store, WorkflowProcess process, WorkflowScheduler scheduler, IOptions<WorkflowEngineOptions> options)
    {
        Store = store;
        Process = process;
        Scheduler = scheduler;
        Options = options;
    }

    private WorkflowStore Store { get; }

    private WorkflowProcess Process { get; }

    private WorkflowScheduler Scheduler { get; }

    private IOptions<WorkflowEngineOptions> Options { get; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetQuery query)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var results =
            await Scheduler
                .EnumerateTriggers(JobGroupNames.Workflows)
                .Where(trigger => query.StartsWith is null || trigger.JobKey.Name.StartsWith(query.StartsWith))
                .Select(trigger => new
                {
                    name = trigger.Key.Name,
                    plan = trigger.CronExpressionString,
                    next = 
                        trigger
                            .CronExpressionString?
                            .ToCronExpression()
                            .GetTimeAfter(utcNow)
                            .Let(next => query.LocalTime is true && next.HasValue ? next.Value.ToLocalTime() : next)
                })
                .ToListAsync();

        return Ok(results);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var deletedJobs =
            await Scheduler
                .EnumerateTriggers(JobGroupNames.Workflows)
                .Select(trigger => trigger.JobKey.Name)
                .WhereAwait(async jobKey => await Scheduler.Delete(jobKey))
                .ToListAsync();

        return Ok(deletedJobs);
    }

    [HttpPut]
    public async Task<IActionResult> Update()
    {
        var createdJobs =
            await Store
                .SelectAwait(async workflow => new { workflow.Name, isNew = await Scheduler.Schedule(workflow) is not null })
                .ToListAsync();

        return Ok(createdJobs);
    }

    // [HttpPut]
    // public async Task<IActionResult> CreateWorkflow([FromBody] Workflow workflow)
    // {
    //     if (Options.Value.UpdaterSchedule.ToCronExpression().GetTimeAfter(DateTimeOffset.UtcNow) is not { } initializationAt)
    //     {
    //         return Problem("Workflow will never be scheduled as the updater is not running.");
    //     }
    //
    //     if (workflow.Schedule.ToCronExpression().GetTimeAfter(initializationAt) is not { } firstRunAt)
    //     {
    //         return Forbid("Workflow will never run.");
    //     }
    //
    //     await Store.Add(workflow);
    //     
    //     return Ok(new
    //     {
    //         initializationAt, 
    //         firstRunAt
    //     });
    // }
    //
    // [HttpDelete("store/{name}")]
    // public IActionResult DeleteWorkflow(string name)
    // {
    //     return Store.Delete(name) ? NoContent() : NotFound();
    // }
}