using LogMind.Core.Interfaces;
using LogMind.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IssuesController : ControllerBase
{
    private readonly LogMindDbContext _db;
    private readonly ISearchService _search;
    private readonly IAiExplanationService _ai;
    private readonly ILogRepository _logs;

    public IssuesController(LogMindDbContext db, ISearchService search, IAiExplanationService ai, ILogRepository logs)
    {
        _db = db;
        _search = search;
        _ai = ai;
        _logs = logs;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.KnownIssues.Include(i => i.Solutions).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var issue = await _db.KnownIssues.Include(i => i.Solutions).FirstOrDefaultAsync(i => i.Id == id);
        return issue is null ? NotFound() : Ok(issue);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int topK = 5)
        => Ok(await _search.FindSimilarIssuesAsync(q, topK));

    [HttpGet("for-log/{logId:int}")]
    public async Task<IActionResult> ForLog(int logId)
    {
        var entry = await _logs.GetByIdAsync(logId);
        if (entry is null) return NotFound();

        var similar = await _search.FindSimilarIssuesAsync(entry.Message);
        var suggestion = await _ai.SuggestSolutionAsync(entry, similar);
        return Ok(new { logEntry = entry, similarIssues = similar, aiSuggestion = suggestion });
    }
}
