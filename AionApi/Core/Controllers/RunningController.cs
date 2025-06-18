using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AionApi.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class RunningController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok();
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        return Ok();
    }
}