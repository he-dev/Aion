using System;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Util.Quartz;
using AionApi.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AionApi.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class OfflineController
(
    ILogger<OfflineController> logger,
    WorkflowDirectory workflowDirectory,
    WorkflowSchedule workflowSchedule
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var results =
            await workflowDirectory
                .Where(workflow => !workflow.Enabled) // !! Get only disabled workflows here.
                .SelectAwait(workflow => ValueTask.FromResult(new
                {
                    path = workflow.Path,
                    name = workflow.Name,
                    isOn = workflow.Enabled,
                    cron = workflow.Cron,
                    next = workflow.Trigger.FiresAt(DateTimeOffset.UtcNow).Take(3).ToList(),
                    jobs = workflow.Steps.Count(s => s.Enabled)
                }))
                .OrderBy(item => item.next.FirstOrDefault())
                .ThenBy(item => item.name)
                .ToListAsync();

        logger.LogInformation("Found {count} workflows.", results.Count);
        return Ok(results);
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            return Ok(new
            {
                path = workflow.Path,
                name = workflow.Name,
                isOn = workflow.Enabled,
                cron = workflow.Cron,
                next = workflow.Trigger.FiresAt(DateTimeOffset.UtcNow).Take(3).ToList(),
                jobs = workflow.Steps.Count(s => s.Enabled)
            });
        }

        logger.LogError("Workflow '{name}' not found.", name);
        return NotFound(new { name });
    }

    [HttpPost("{name}/start/{delay:int?}")]
    public async Task<IActionResult> Start(string name, int delay = 0)
    {
        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            if (!workflow.Steps.Any(s => s.Enabled))
            {
                logger.LogWarning("Workflow '{name}' has no enabled steps or is empty.", name);
                return UnprocessableEntity(new { name });
            }

            var next =
                delay <= 0
                    ? await workflowSchedule.StartNow(name)
                    : await workflowSchedule.StartLater(name, delay);

            return Ok(new { name, next });
        }

        logger.LogError("Workflow '{name}' not found.", name);
        return NotFound(new { name });
    }
}