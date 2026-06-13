using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface IIncidentRepository
{
    /// <summary>Returns open/investigating incidents whose LastSeenAt is on or after <paramref name="since"/>.
    /// Used by CorrelationService to find candidate incidents to join.</summary>
    Task<List<Incident>> FindOpenCandidatesAsync(DateTime since);

    /// <summary>Returns the incident that contains this log entry, or null.</summary>
    Task<Incident?> FindByLogEntryAsync(int logEntryId);

    Task<Incident> CreateAsync(Incident incident);
    Task UpdateAsync(Incident incident);
    Task AddEventAsync(IncidentEvent evt);

    Task<Incident?> GetByIdAsync(int id);
    Task<List<Incident>> GetRecentAsync(int limit = 20);
}
