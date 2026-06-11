using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface IAlertRepository
{
    Task<IEnumerable<Alert>> GetActiveAsync();
    Task<IEnumerable<Alert>> GetAllAsync(int page = 1, int pageSize = 50);
    Task AddAsync(Alert alert);
    Task AcknowledgeAsync(int id);
    Task<int> GetUnacknowledgedCountAsync();
}
