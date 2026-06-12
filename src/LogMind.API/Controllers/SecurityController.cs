using Microsoft.AspNetCore.Mvc;

namespace LogMind.API.Controllers;

[ApiController]
[Route("api/security")]
public class SecurityController(IConfiguration cfg) : ControllerBase
{
    // Always open — lets frontend discover whether auth is required
    [HttpGet("status")]
    public IActionResult Status()
    {
        var keyConfigured = !string.IsNullOrWhiteSpace(cfg["Security:ApiKey"]);
        return Ok(new { keyRequired = keyConfigured });
    }
}
