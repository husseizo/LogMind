using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Data;

public class AlertRepository : IAlertRepository
{
    private readonly LogMindDbContext _db;
    public AlertRepository(LogMindDbContext db) => _db = db;

    public async Task<IEnumerable<Alert>> GetActiveAsync()
        => await _db.Alerts
            .Where(a => !a.IsAcknowledged)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync();

    public async Task<IEnumerable<Alert>> GetAllAsync(int page = 1, int pageSize = 50)
        => await _db.Alerts
            .OrderByDescending(a => a.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task AddAsync(Alert alert)
    {
        await _db.Alerts.AddAsync(alert);
        await _db.SaveChangesAsync();
    }

    public async Task AcknowledgeAsync(int id)
    {
        var alert = await _db.Alerts.FindAsync(id);
        if (alert is null) return;
        alert.IsAcknowledged = true;
        alert.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public Task<int> GetUnacknowledgedCountAsync()
        => _db.Alerts.CountAsync(a => !a.IsAcknowledged);
}
