using System;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Util.Quartz;
using AionApi.Utilities;
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
                    name = workflow.Name,
                    isOn = workflow.Enabled,
                    cron = workflow.Cron,
                    next = workflow.Trigger.FiresAt(DateTimeOffset.UtcNow).Take(3).ToList(),
                    cmds = workflow.Steps.Count(s => s.Enabled)
                }))
                .OrderBy(item => item.next.FirstOrDefault())
                .ThenBy(item => item.name)
                .ToListAsync();

        return Ok(results);
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            return Ok(new
            {
                name = workflow.Name,
                isOn = workflow.Enabled,
                cron = workflow.Cron,
                next = workflow.Trigger.FiresAt(DateTimeOffset.UtcNow).Take(3).ToList(),
                cmds = workflow.Steps.Count(s => s.Enabled)
            });
        }

        return NotFound(new { name });
    }

    [HttpPost("{name}/run")]
    public async Task<IActionResult> Run(string name)
    {
        if (await workflowDirectory.FindWorkflow(name) is { } workflow)
        {
            if (!workflow.Steps.Any(s => s.Enabled))
            {
                logger.LogWarning("Workflow '{name}' has no enabled steps or is empty.", name);
                return UnprocessableEntity(new { name });
            }

            var next = await workflowSchedule.StartLater(name, 30);
            return Ok(new { name, next });
        }

        logger.LogError("Workflow '{name}' not found.", name);
        return NotFound(new { name });
    }
}