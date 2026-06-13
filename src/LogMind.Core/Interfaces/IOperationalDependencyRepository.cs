using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface IOperationalDependencyRepository
{
    /// <summary>Returns active downstream dependencies for the given source system, ordered by ImpactWeight descending.</summary>
    Task<List<OperationalDependency>> FindDownstreamAsync(string sourceSystem);
    Task<List<OperationalDependency>> GetAllAsync();
}
