using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface ILogRepository
{
    Task<IEnumerable<LogEntry>> GetAllAsync(int page = 1, int pageSize = 50);
    Task<(IEnumerable<LogEntry> Items, bool HasMore, DateTime? NextCursorTs, int? NextCursorId)> QueryAsync(
        string? query, string? source, string? level, DateTime? from, DateTime? to,
        int pageSize, DateTime? cursorTs, int? cursorId);
    Task<IEnumerable<LogEntry>> SearchAsync(string query, string? source = null, string? level = null);
    Task<IEnumerable<LogEntry>> GetBySourceAsync(string source);
    Task<IEnumerable<LogEntry>> GetRecentErrorsAsync(int count = 100);
    Task<Dictionary<string, int>> GetErrorCountBySourceAsync(DateTime from, DateTime to);
    Task<Dictionary<string, int>> GetErrorCountByLevelAsync(DateTime from, DateTime to);
    Task AddRangeAsync(IEnumerable<LogEntry> entries);
    Task<LogEntry?> GetByIdAsync(int id);
    Task<int> GetTotalCountAsync(string? source = null, string? level = null);
}
