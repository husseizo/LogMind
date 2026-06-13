namespace LogMind.Core.Models;

public class Solution
{
    public int Id { get; set; }
    public int KnownIssueId { get; set; }
    public KnownIssue KnownIssue { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Steps { get; set; } = string.Empty;
    public string? References { get; set; }
    public int Upvotes { get; set; }

    // Set to true when FailedCount >= 3 AND FailedCount > WorkedCount — flags for manual review
    public bool NeedsReview { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<SolutionFeedback> Feedback { get; set; } = new List<SolutionFeedback>();
}
