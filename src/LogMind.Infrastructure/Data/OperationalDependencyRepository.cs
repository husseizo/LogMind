using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Data;

public class OperationalDependencyRepository : IOperationalDependencyRepository
{
    private readonly LogMindDbContext _db;

    public OperationalDependencyRepository(LogMindDbContext db) => _db = db;

    public Task<List<OperationalDependency>> FindDownstreamAsync(string sourceSystem) =>
        _db.OperationalDependencies
            .Where(d => d.IsActive && d.SourceSystem == sourceSystem)
            .OrderByDescending(d => d.ImpactWeight)
            .ToListAsync();

    public Task<List<OperationalDependency>> GetAllAsync() =>
        _db.OperationalDependencies.ToListAsync();
}
