namespace LogMind.Core.Models;

public class KnownIssue
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ErrorPattern { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public ICollection<Solution> Solutions { get; set; } = new List<Solution>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
