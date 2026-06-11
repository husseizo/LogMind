using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using LogMind.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogMind.API.Controllers;

[ApiController]
[Route("api/issues/{issueId:int}/solutions")]
public class SolutionsController : ControllerBase
{
    private readonly LogMindDbContext _db;
    private readonly EmbeddingSearchService _embedding;

    public SolutionsController(LogMindDbContext db, EmbeddingSearchService embedding)
    {
        _db = db;
        _embedding = embedding;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int issueId)
    {
        var solutions = await _db.Solutions.Where(s => s.KnownIssueId == issueId).ToListAsync();
        return Ok(solutions);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int issueId, [FromBody] SolutionDto dto)
    {
        var issue = await _db.KnownIssues.FindAsync(issueId);
        if (issue is null) return NotFound();

        var solution = new Solution
        {
            KnownIssueId = issueId,
            Title        = dto.Title,
            Steps        = dto.Steps,
            References   = dto.References,
            Upvotes      = 0,
            CreatedAt    = DateTime.UtcNow
        };

        await _db.Solutions.AddAsync(solution);
        await _db.SaveChangesAsync();

        // Re-index embedding since the issue content changed
        await _embedding.IndexKnownIssueAsync(issue);

        return CreatedAtAction(nameof(GetAll), new { issueId }, solution);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int issueId, int id, [FromBody] SolutionDto dto)
    {
        var solution = await _db.Solutions.FirstOrDefaultAsync(s => s.Id == id && s.KnownIssueId == issueId);
        if (solution is null) return NotFound();

        solution.Title      = dto.Title;
        solution.Steps      = dto.Steps;
        solution.References = dto.References;
        await _db.SaveChangesAsync();

        return Ok(solution);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int issueId, int id)
    {
        var solution = await _db.Solutions.FirstOrDefaultAsync(s => s.Id == id && s.KnownIssueId == issueId);
        if (solution is null) return NotFound();

        _db.Solutions.Remove(solution);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/upvote")]
    public async Task<IActionResult> Upvote(int issueId, int id)
    {
        var solution = await _db.Solutions.FirstOrDefaultAsync(s => s.Id == id && s.KnownIssueId == issueId);
        if (solution is null) return NotFound();
        solution.Upvotes++;
        await _db.SaveChangesAsync();
        return Ok(new { solution.Upvotes });
    }
}

public record SolutionDto(string Title, string Steps, string? References);
