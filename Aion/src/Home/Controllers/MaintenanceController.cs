using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Controllers;

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
        var lockFileNames = workflowDirectory.FindFiles(FileFilter.Any, FileExtension.Lock);
        var locks = ImmutableList<WorkflowLock>.Empty;
        foreach (var lockFileName in lockFileNames)
        {
            try
            {
                if (await WorkflowLock.FromFile(lockFileName) is { } lockFile)
                {
                    locks = locks.Add(lockFile);
                }
            }
            catch (FileNotFoundException)
            {
                // core: Ignore this error as it is by design.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{LockFileName}'.", lockFileName);
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
                s.Duration,
                s.Remaining,
                s.IsPending,
                s.IsRunning,
                s.IsExpired,
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
            var workflowLock = body.ToWorkflowLock();
            ;
            var lockNames = await workflowMaintenance.Schedule(workflowLock, body.Filter);
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
        [FromBody] StartAtBody body
    )
    {
        try
        {
            var workflowLock = body.ToWorkflowLock();
            var lockedWorkflows = await workflowMaintenance.Schedule(workflowLock, body.Filter);
            return Ok(new { workflowLock, lockedWorkflows });
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

        public WorkflowLock ToWorkflowLock() => WorkflowLock.StartIn(Wait, Duration);
    }

    public record StartAtBody
    {
        public string Filter { get; init; } = null!;

        public DateTimeOffset StartsOn { get; init; }

        public DateTimeOffset EndsOn { get; init; }

        public WorkflowLock ToWorkflowLock() => WorkflowLock.StartAt
        (
            StartsOn.UseTimeZoneOffsetOrLocal().ToUniversalTime(),
            EndsOn.UseTimeZoneOffsetOrLocal().ToUniversalTime()
        );
    }
}