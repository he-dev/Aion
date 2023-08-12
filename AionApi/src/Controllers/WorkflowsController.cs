using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Filters;
using AionApi.Models;
using AionApi.Services;
using AionApi.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Quartz;

namespace AionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    public WorkflowsController(WorkflowStore store, WorkflowProcess process, WorkflowScheduler scheduler, IOptions<WorkflowEngineOptions> options)
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
        var results =
            await Store
                .Where(workflow => query.Enabled is null || workflow.Enabled == query.Enabled)
                .SelectAwait(async workflow => new
                {
                    name = workflow.Name,
                    isOn = workflow.Enabled,
                    plan = workflow.Schedule,
                    next = 
                        await Scheduler
                            .EnumerateNext(workflow.JobKey, query.AfterTimeUtc ?? DateTimeOffset.UtcNow)
                            .Where(s => s.Wait.TotalHours <= 24)
                            .Select(s => s.Next)
                            .ToLocalTime(query.LocalTime ?? false)
                            .Take(3)
                            .ToListAsync()
                })
                .Where(item => query.Active is null || (query.Active is true && item.next.Any()))
                .OrderBy(item => item.next.FirstOrDefault())
                .ThenBy(item => item.name)
                .ToListAsync();

        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult<IAsyncEnumerable<object>>> ExecuteWorkflow([FromBody] ExecuteWorkflowBody body)
    {
        if (await Store.GetWorkflow(body.Name) is { } workflow)
        {
            return Ok(Process.Start(workflow).Select(result => new
            {
                result.Index,
                result.FileName,
                result.Arguments,
                process = new
                {
                    result.Process.StartInfo.FileName,
                    result.Process.StartInfo.Arguments,
                    result.Process.ExitCode,
                    result.Process.Killed,
                    result.Process.Output,
                    result.Process.Error,
                    Exception = result.Process.Exception?.ToString()
                },
            }));
        }

        return NotFound(new { message = "Workflow not found.", body.Name });
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

public class GetQuery
{
    public string? StartsWith { get; set; }
    public bool? Enabled { get; set; }
    public bool? Active { get; set; }
    public bool? LocalTime { get; set; }
    public DateTimeOffset? AfterTimeUtc { get; set; }
}

public class ExecuteWorkflowBody
{
    public string Name { get; set; } = default!;
}