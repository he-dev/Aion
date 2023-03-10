using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Models;
using AionApi.Services;
using AionApi.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reusable;

namespace AionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    public WorkflowsController(WorkflowStore store, WorkflowRunner runner, WorkflowScheduler scheduler, IOptions<WorkflowEngineOptions> options)
    {
        Store = store;
        Runner = runner;
        Scheduler = scheduler;
        Options = options;
    }

    private WorkflowStore Store { get; }

    private WorkflowRunner Runner { get; }

    private WorkflowScheduler Scheduler { get; }

    private IOptions<WorkflowEngineOptions> Options { get; }

    [HttpGet("active")]
    public async Task<IActionResult> GetWorkflowJobs([FromQuery(Name = "order-by")] string? orderBy = default)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var triggers = await Scheduler.EnumerateActiveWorkflowCronTriggers().ToListAsync();
        var results = triggers.Select(t => new
        {
            name = t.Key.Name,
            plan = t.CronExpressionString,
            next = t.CronExpressionString!.ToCronExpression().GetTimeAfter(utcNow)
        });
        results = orderBy?.ToLower() switch
        {
            "name" => results.OrderBy(x => x.name),
            "next" => results.OrderBy(x => x.next),
            _ => results
        };
        return Ok(results);
    }

    [HttpGet("store")]
    public async Task<IActionResult> GetWorkflowStore()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var workflows = await Store.EnumerateWorkflows().ToListAsync();
        var results = workflows.Select(w => new
        {
            name = w.Name,
            isOn = w.Enabled,
            plan = w.Schedule,
            next = w.Schedule.ToCronExpression().GetTimeAfter(utcNow)
        });
        return Ok(results);
    }

    [HttpGet("next/{name}/{count:int?}")]
    public async Task<IActionResult> GetNext(string name, int count = 3)
    {
        if (await Store.Get(name) is { } workflow)
        {
            var results = workflow.Schedule.ToCronExpression().Generate
            (
                first: cron => cron.GetTimeAfter(DateTimeOffset.UtcNow),
                next: (cron, previous) => previous.HasValue ? cron.GetTimeAfter(previous.Value) : default
            );
            return Ok(results.Where(x => x.HasValue).Take(count < 100 ? count : 100));
        }

        return NotFound();
    }

    [HttpGet("store/{name}")]
    public async Task<IActionResult> GetWorkflow(string name)
    {
        return await Store.Get(name) is { } workflow ? Ok(workflow) : NotFound();
    }

    [HttpPost]
    public async Task<IEnumerable<AsyncProcess.Result>> RunWorkflow([FromBody] Workflow workflow)
    {
        return await Runner.Run(workflow);
    }

    [HttpPut]
    public async Task<IActionResult> CreateWorkflow([FromBody] Workflow workflow)
    {
        if (Options.Value.UpdaterSchedule.ToCronExpression().GetTimeAfter(DateTimeOffset.UtcNow) is not { } initializationAt)
        {
            return Problem("Workflow will never be scheduled as the updater is not running.");
        }

        if (workflow.Schedule.ToCronExpression().GetTimeAfter(initializationAt) is not { } firstRunAt)
        {
            return Forbid("Workflow will never run.");
        }

        await Store.Add(workflow);
        
        return Ok(new
        {
            initializationAt, 
            firstRunAt
        });
    }

    [HttpDelete("store/{name}")]
    public IActionResult DeleteWorkflow(string name)
    {
        return Store.Delete(name) ? NoContent() : NotFound();
    }
}