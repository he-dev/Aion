using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Workflows;
using Aion.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class DiagnosticsController
(
    ILogger<OfflineController> logger,
    WorkflowDirectory workflowDirectory
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "q")] string? filter)
    {

        var results =
            await workflowDirectory
                .InRsAsync()
                .Select(issue => new
                {
                    path = issue.Path,
                    flaw = issue.Exception.ToString(),
                })
                .ToListAsync();

        logger.LogInformation("Found {count} workflows.", results.Count);

        return
            results.Any()
                ? Ok(results)
                : NoContent();
    }
}