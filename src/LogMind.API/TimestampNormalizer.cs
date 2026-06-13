using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMind.API;

/// <summary>
/// One-time startup migrator that converts any LogEntry.Timestamp values stored with
/// timezone offsets or ISO-8601 'T' separators into the plain UTC format that EF Core's
/// SQLite provider writes ("yyyy-MM-dd HH:mm:ss.fffffff"). Idempotent — rows already in
/// the correct format are skipped. Safe to leave in place indefinitely.
/// </summary>
public static class TimestampNormalizer
{
    private const string TargetFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

    public static async Task NormalizeAsync(LogMindDbContext db, ILogger logger)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        // Select only rows whose stored text contains a 'T' separator or a space-before-offset
        // pattern — the two known non-standard formats in this database.
        var selectCmd = conn.CreateCommand();
        selectCmd.CommandText =
            "SELECT Id, Timestamp FROM LogEntries " +
            "WHERE Timestamp LIKE '%T%' OR Timestamp LIKE '% +%' OR Timestamp LIKE '% -%'";

        var updates = new List<(long Id, string Ts)>();
        using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var id    = reader.GetInt64(0);
                var tsStr = reader.GetString(1);

                // Try offset-aware parse first (handles +03:00 / Z suffixes correctly)
                if (DateTimeOffset.TryParse(tsStr, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
                {
                    updates.Add((id, dto.UtcDateTime.ToString(TargetFormat)));
                }
                else if (DateTime.TryParse(tsStr, null,
                        System.Globalization.DateTimeStyles.AssumeUniversal |
                        System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                {
                    updates.Add((id, dt.ToString(TargetFormat)));
                }
                // If neither parse succeeds, leave the row as-is (don't corrupt it)
            }
        }

        if (updates.Count == 0)
        {
            logger.LogDebug("TimestampNormalizer: all timestamps already in UTC format — nothing to do");
            if (!wasOpen) await conn.CloseAsync();
            return;
        }

        // Batch all UPDATEs inside a single transaction — fast even for thousands of rows
        using var tx = await conn.BeginTransactionAsync();
        foreach (var (id, ts) in updates)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = (System.Data.Common.DbTransaction)tx;
            cmd.CommandText = $"UPDATE LogEntries SET Timestamp = '{ts}' WHERE Id = {id}";
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();

        logger.LogInformation(
            "TimestampNormalizer: normalized {Count} LogEntry timestamps to UTC (format: {Format})",
            updates.Count, TargetFormat);

        if (!wasOpen) await conn.CloseAsync();
    }
}
