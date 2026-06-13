using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMind.Infrastructure.Services;

public class SolutionFeedbackService
{
    private readonly LogMindDbContext _db;
    private readonly ILogger<SolutionFeedbackService> _logger;

    public SolutionFeedbackService(LogMindDbContext db, ILogger<SolutionFeedbackService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// Records a worked/not-worked feedback entry for a solution, then applies side-effects:
    /// - Increments Solution.Upvotes on positive feedback
    /// - Sets Solution.NeedsReview when FailedCount >= 3 AND FailedCount > WorkedCount
    /// - Invalidates related AiExplanationCache entries when NeedsReview is first triggered
    /// </summary>
    public async Task<FeedbackResult> RecordAsync(
        int solutionId, int? logEntryId, bool worked, string? note)
    {
        var solution = await _db.Solutions.FirstOrDefaultAsync(s => s.Id == solutionId);
        if (solution is null)
            throw new KeyNotFoundException($"Solution {solutionId} not found.");

        var feedback = new SolutionFeedback
        {
            SolutionId = solutionId,
            LogEntryId = logEntryId,
            Worked     = worked,
            Note       = note,
            CreatedAt  = DateTime.UtcNow,
        };

        await _db.SolutionFeedback.AddAsync(feedback);

        if (worked) solution.Upvotes++;

        await _db.SaveChangesAsync();

        await ApplyFeedbackEffectsAsync(solution);

        // Re-query counts after effects so the response is always accurate
        var workedCount = await _db.SolutionFeedback.CountAsync(f => f.SolutionId == solutionId && f.Worked);
        var failedCount = await _db.SolutionFeedback.CountAsync(f => f.SolutionId == solutionId && !f.Worked);

        return new FeedbackResult(workedCount, failedCount, solution.NeedsReview, solution.Upvotes);
    }

    // ── Side-effects ─────────────────────────────────────────────────────────

    private async Task ApplyFeedbackEffectsAsync(Solution solution)
    {
        var workedCount = await _db.SolutionFeedback.CountAsync(f => f.SolutionId == solution.Id && f.Worked);
        var failedCount = await _db.SolutionFeedback.CountAsync(f => f.SolutionId == solution.Id && !f.Worked);

        var shouldFlag = failedCount >= 3 && failedCount > workedCount;

        // Only act the first time this solution crosses the review threshold — avoid repeat work
        if (shouldFlag && !solution.NeedsReview)
        {
            solution.NeedsReview = true;
            await _db.SaveChangesAsync();

            // Invalidate cache entries linked to the same KnownIssue so the next
            // explain call bypasses the cache and generates a fresh explanation
            var stale = await _db.AiExplanationCache
                .Where(e => e.RelatedIssueId == solution.KnownIssueId && !e.IsInvalidated)
                .ToListAsync();

            if (stale.Count > 0)
            {
                foreach (var entry in stale)
                    entry.IsInvalidated = true;

                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Solution {SolutionId} flagged NeedsReview; invalidated {Count} cache entries for issue {IssueId}",
                    solution.Id, stale.Count, solution.KnownIssueId);
            }
        }
    }
}

public record FeedbackResult(int WorkedCount, int FailedCount, bool NeedsReview, int Upvotes);
