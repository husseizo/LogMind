using LogMind.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LogMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogRepository _logs;
    private readonly IAiExplanationService _ai;

    public LogsController(ILogRepository logs, IAiExplanationService ai)
    {
        _logs = logs;
        _ai = ai;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _logs.GetAllAsync(page, pageSize));

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? source, [FromQuery] string? level)
        => Ok(await _logs.SearchAsync(q, source, level));

    [HttpGet("errors")]
    public async Task<IActionResult> GetErrors([FromQuery] int count = 100)
        => Ok(await _logs.GetRecentErrorsAsync(count));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entry = await _logs.GetByIdAsync(id);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpGet("{id:int}/explain")]
    public async Task<IActionResult> Explain(int id)
    {
        var entry = await _logs.GetByIdAsync(id);
        if (entry is null) return NotFound();
        var explanation = await _ai.ExplainErrorAsync(entry);
        return Ok(new { explanation });
    }

    [HttpGet("stats/by-source")]
    public async Task<IActionResult> StatsBySource([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-7);
        var t = to ?? DateTime.UtcNow;
        return Ok(await _logs.GetErrorCountBySourceAsync(f, t));
    }

    [HttpGet("stats/by-level")]
    public async Task<IActionResult> StatsByLevel([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-7);
        var t = to ?? DateTime.UtcNow;
        return Ok(await _logs.GetErrorCountByLevelAsync(f, t));
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count([FromQuery] string? source, [FromQuery] string? level)
        => Ok(new { count = await _logs.GetTotalCountAsync(source, level) });
}
