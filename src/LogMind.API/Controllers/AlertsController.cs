using LogMind.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LogMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRepository _alerts;
    public AlertsController(IAlertRepository alerts) => _alerts = alerts;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _alerts.GetAllAsync(page, pageSize));

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
        => Ok(await _alerts.GetActiveAsync());

    [HttpGet("count")]
    public async Task<IActionResult> GetUnacknowledgedCount()
        => Ok(new { count = await _alerts.GetUnacknowledgedCountAsync() });

    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id)
    {
        await _alerts.AcknowledgeAsync(id);
        return NoContent();
    }
}
