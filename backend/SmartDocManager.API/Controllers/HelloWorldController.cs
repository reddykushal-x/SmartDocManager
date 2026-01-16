using Microsoft.AspNetCore.Mvc;

namespace SmartDocManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HelloWorldController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Hello World from Smart Document Analyzer API!" });
    }
}
