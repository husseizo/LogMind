using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Data;

public class IncidentRepository : IIncidentRepository
{
    private readonly LogMindDbContext _db;

    public IncidentRepository(LogMindDbContext db) => _db = db;

    public Task<List<Incident>> FindOpenCandidatesAsync(DateTime since) =>
        _db.Incidents
            .Where(i => i.Status != "Resolved" && i.LastSeenAt >= since && i.EventCount < 100)
            .OrderByDescending(i => i.LastSeenAt)
            .Take(50)
            .ToListAsync();

    public async Task<Incident?> FindByLogEntryAsync(int logEntryId)
    {
        var evt = await _db.IncidentEvents
            .Where(e => e.LogEntryId == logEntryId)
            .Include(e => e.Incident)
            .FirstOrDefaultAsync();

        return evt?.Incident;
    }

    public async Task<Incident> CreateAsync(Incident incident)
    {
        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    public async Task UpdateAsync(Incident incident)
    {
        _db.Incidents.Update(incident);
        await _db.SaveChangesAsync();
    }

    public async Task AddEventAsync(IncidentEvent evt)
    {
        _db.IncidentEvents.Add(evt);
        await _db.SaveChangesAsync();
    }

    public Task<Incident?> GetByIdAsync(int id) =>
        _db.Incidents
            .Include(i => i.Events.OrderBy(e => e.Sequence))
                .ThenInclude(e => e.LogEntry)
            .FirstOrDefaultAsync(i => i.Id == id);

    public Task<List<Incident>> GetRecentAsync(int limit = 20) =>
        _db.Incidents
            .OrderByDescending(i => i.LastSeenAt)
            .Take(limit)
            .ToListAsync();
}
