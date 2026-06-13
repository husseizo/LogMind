namespace LogMind.Core.Models;

public class OperationalDependency
{
    public int Id { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;

    /// <summary>DataFlow | Synchronization | ReadModel | Writeback | Reporting | Notification</summary>
    public string DependencyType { get; set; } = string.Empty;

    /// <summary>Low | Medium | High | Critical</summary>
    public string Criticality { get; set; } = string.Empty;

    /// <summary>One-line description of what this edge carries (e.g. "NeonInvoiceSyncJob pushes invoice state to Odoo").</summary>
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Numeric weight for incident severity scoring. Critical=100, High=75, Medium=50, Low=25.</summary>
    public int ImpactWeight { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
