using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api")]
public class DiagnosticsController(ILogger<DiagnosticsController> logger) : ControllerBase
{
    [HttpGet("[controller]/routes")]
    public IActionResult Routes
    (
        [FromServices] IEnumerable<EndpointDataSource> sources
    )
    {
        var patterns = sources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText);
        return Ok(patterns);
    }
}