using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Data;

public class LogRepository : ILogRepository
{
    private readonly LogMindDbContext _db;

    public LogRepository(LogMindDbContext db) => _db = db;

    public async Task<IEnumerable<LogEntry>> GetAllAsync(int page = 1, int pageSize = 50)
        => await _db.LogEntries
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<(IEnumerable<LogEntry> Items, bool HasMore, DateTime? NextCursorTs, int? NextCursorId)> QueryAsync(
        string? query, string? source, string? level, DateTime? from, DateTime? to,
        int pageSize, DateTime? cursorTs, int? cursorId)
    {
        var q = _db.LogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(e => e.Message.Contains(query) || (e.StackTrace != null && e.StackTrace.Contains(query)));
        if (!string.IsNullOrWhiteSpace(source)) q = q.Where(e => e.Source == source);
        if (!string.IsNullOrWhiteSpace(level))  q = q.Where(e => e.Level == level);
        if (from.HasValue) q = q.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)   q = q.Where(e => e.Timestamp <= to.Value);

        // Cursor: only fetch rows older than the last seen item (constant-speed regardless of depth)
        if (cursorTs.HasValue && cursorId.HasValue)
            q = q.Where(e => e.Timestamp < cursorTs.Value ||
                             (e.Timestamp == cursorTs.Value && e.Id < cursorId.Value));

        // Fetch one extra to detect hasMore without COUNT(*)
        var items = await q
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Take(pageSize + 1)
            .ToListAsync();

        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);

        var last = items.LastOrDefault();
        return (items, hasMore, last?.Timestamp, last?.Id);
    }

    public async Task<IEnumerable<LogEntry>> SearchAsync(string query, string? source = null, string? level = null)
    {
        var q = _db.LogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(e => e.Message.Contains(query) || (e.StackTrace != null && e.StackTrace.Contains(query)));
        if (!string.IsNullOrWhiteSpace(source))
            q = q.Where(e => e.Source == source);
        if (!string.IsNullOrWhiteSpace(level))
            q = q.Where(e => e.Level == level);
        return await q.OrderByDescending(e => e.Timestamp).Take(200).ToListAsync();
    }

    public async Task<IEnumerable<LogEntry>> GetBySourceAsync(string source)
        => await _db.LogEntries.Where(e => e.Source == source).OrderByDescending(e => e.Timestamp).Take(100).ToListAsync();

    public async Task<IEnumerable<LogEntry>> GetRecentErrorsAsync(int count = 100)
        => await _db.LogEntries
            .Where(e => e.Level == "ERROR" || e.Level == "FATAL")
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToListAsync();

    public async Task<Dictionary<string, int>> GetErrorCountBySourceAsync(DateTime from, DateTime to)
        => await _db.LogEntries
            .Where(e => e.Timestamp >= from && e.Timestamp <= to && (e.Level == "ERROR" || e.Level == "FATAL"))
            .GroupBy(e => e.Source)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public async Task<Dictionary<string, int>> GetErrorCountByLevelAsync(DateTime from, DateTime to)
        => await _db.LogEntries
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .GroupBy(e => e.Level)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public async Task AddRangeAsync(IEnumerable<LogEntry> entries)
    {
        await _db.LogEntries.AddRangeAsync(entries);
        await _db.SaveChangesAsync();
    }

    public async Task<LogEntry?> GetByIdAsync(int id)
        => await _db.LogEntries.FindAsync(id);

    public async Task<int> GetTotalCountAsync(string? source = null, string? level = null)
    {
        var q = _db.LogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(source)) q = q.Where(e => e.Source == source);
        if (!string.IsNullOrWhiteSpace(level)) q = q.Where(e => e.Level == level);
        return await q.CountAsync();
    }
}
