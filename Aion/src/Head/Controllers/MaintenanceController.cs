using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Head.Controllers;

[ApiController]
[Route("api")]
public class MaintenanceController(ILogger<MaintenanceController> logger) : ControllerBase
{
    [HttpGet("[controller]")]
    public async Task<IActionResult> Get
    (
        [FromServices] WorkflowDirectory workflowDirectory
    )
    {
        var lockFileNames = workflowDirectory.FindFiles(null, FileExtension.Lock);
        var locks = ImmutableList<WorkflowLock>.Empty;
        foreach (var lockFileName in lockFileNames)
        {
            try
            {
                if (await WorkflowLock.FromFile(lockFileName) is { } lockFile)
                {
                    await using (lockFile)
                    {
                        if (lockFile.Remaining > TimeSpan.Zero)
                        {
                            locks = locks.Add(lockFile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{lock}'.", lockFileName);
            }
        }

        var query =
            from s in locks
            orderby s.Remaining descending
            select new
            {
                s.FileName,
                s.CreatedOnUtc,
                s.StartsOnUtc,
                s.EndsOnUtc,
                s.Length,
                s.Remaining,
                s.IsPending
            };

        return Ok(query.ToList());
    }

    [HttpPost("[controller]:startIn")]
    public async Task<IActionResult> StartIn
    (
        [FromServices] WorkflowMaintenance workflowMaintenance,
        [FromBody] StartInBody body
    )
    {
        try
        {
            var lockNames = await workflowMaintenance.Schedule(body.Filter, body.Wait, body.Duration);
            return Ok(new { lockNames });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule maintenance for '{filter}'.", body.Filter);
            return Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    [HttpPost("[controller]:startAt")]
    public async Task<IActionResult> StartAt
    (
        [FromServices] WorkflowMaintenance workflowMaintenance,
        [FromBody] StartAtBody body,
        string name
    )
    {
        try
        {
            var lockNames = await workflowMaintenance.Schedule(body.Filter, body.StartsOnUtc, body.EndsOnUtc);
            return Ok(new { lockNames });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule maintenance for '{filter}'.", body.Filter);
            return Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    public record StartInBody
    {
        public string Filter { get; init; } = null!;

        public TimeSpan Wait { get; init; }

        public TimeSpan Duration { get; init; }
    }

    public record StartAtBody
    {
        public string Filter { get; init; } = null!;

        public DateTimeOffset StartsOnUtc { get; init; }

        public DateTimeOffset EndsOnUtc { get; init; }
    }
}