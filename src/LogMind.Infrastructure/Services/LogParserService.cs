using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogMind.Infrastructure.Services;

public class LogParserService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogParserService> _logger;
    private readonly Dictionary<string, long> _filePositions = new();
    private readonly Dictionary<string, long> _dbRowOffsets = new();

    public LogParserService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<LogParserService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollLogFilesAsync();
            await PollCacheDbsAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PollLogFilesAsync()
    {
        var sources = _configuration.GetSection("LogSources").GetChildren();
        foreach (var source in sources)
        {
            var sourceName = source["Name"] ?? source.Key;

            var filePath = source["FilePath"];
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                try { await ParseFileAsync(sourceName, filePath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse log file {FilePath} for source {Source}", filePath, sourceName); }
                continue;
            }

            var dirPath = source["DirectoryPath"];
            if (!string.IsNullOrWhiteSpace(dirPath))
            {
                var patterns = (source["FilePattern"] ?? "*.log").Split(';', StringSplitOptions.RemoveEmptyEntries);
                var recursive = source["Recursive"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
                    ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                if (!Directory.Exists(dirPath)) { _logger.LogDebug("Log directory not found: {DirPath}", dirPath); continue; }

                foreach (var pattern in patterns)
                    foreach (var file in Directory.EnumerateFiles(dirPath, pattern.Trim(), recursive))
                    {
                        var isCsv = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
                        try { await ParseFileAsync(sourceName, file, isCsv); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse file {File} for source {Source}", file, sourceName); }
                    }
            }
        }
    }

    private async Task ParseFileAsync(string sourceName, string filePath, bool isCsv = false)
    {
        if (!File.Exists(filePath)) { _logger.LogDebug("Log file not found: {FilePath}", filePath); return; }

        _filePositions.TryGetValue(filePath, out var position);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(position, SeekOrigin.Begin);

        var newEntries = isCsv
            ? await LogFileParser.ParseCsvStreamAsync(stream, sourceName, filePath)
            : await LogFileParser.ParseTextStreamAsync(stream, sourceName, filePath);

        _filePositions[filePath] = stream.Position;

        if (newEntries.Count > 0)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();
            await db.LogEntries.AddRangeAsync(newEntries);
            await db.SaveChangesAsync();
            _logger.LogInformation("Ingested {Count} new log entries from {Source}", newEntries.Count, sourceName);
        }
    }

    private async Task PollCacheDbsAsync()
    {
        var sources = _configuration.GetSection("CacheDbSources").GetChildren();
        foreach (var source in sources)
        {
            var sourceName = source["Name"] ?? source.Key;
            var dbPath = source["DbPath"];
            if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath)) continue;

            var tables = source.GetSection("Tables").GetChildren().ToList();
            foreach (var table in tables)
            {
                var tableName = table["TableName"];
                var tsCol     = table["TimestampColumn"] ?? "created_at";
                var msgCol    = table["MessageColumn"]   ?? "message";
                var levelCol  = table["LevelColumn"]     ?? "level";
                if (string.IsNullOrWhiteSpace(tableName)) continue;

                var offsetKey = $"{dbPath}::{tableName}";
                _dbRowOffsets.TryGetValue(offsetKey, out var lastId);

                try
                {
                    var entries = await ReadSqliteCacheTableAsync(sourceName, dbPath, tableName, tsCol, msgCol, levelCol, lastId);
                    if (entries.Count > 0)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();
                        await db.LogEntries.AddRangeAsync(entries);
                        await db.SaveChangesAsync();
                        _dbRowOffsets[offsetKey] = lastId + entries.Count;
                        _logger.LogInformation("Ingested {Count} rows from cache DB {Source}.{Table}", entries.Count, sourceName, tableName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read cache DB {DbPath} table {Table}", dbPath, tableName);
                }
            }
        }
    }

    private static async Task<List<LogEntry>> ReadSqliteCacheTableAsync(
        string sourceName, string dbPath, string tableName,
        string tsCol, string msgCol, string levelCol, long afterRowNum)
    {
        var entries = new List<LogEntry>();
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;");
        await conn.OpenAsync();

        var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = $"PRAGMA table_info([{tableName}])";
            using var pr = await pragma.ExecuteReaderAsync();
            while (await pr.ReadAsync()) existingCols.Add(pr.GetString(1));
        }

        if (!existingCols.Contains(msgCol)) return entries;

        var hasTs    = existingCols.Contains(tsCol);
        var hasLevel = existingCols.Contains(levelCol);
        var colList  = $"{(hasTs ? $"[{tsCol}]," : "")} {(hasLevel ? $"[{levelCol}]," : "")} [{msgCol}]";
        var sql      = $"SELECT rowid, {colList} FROM [{tableName}] WHERE rowid > {afterRowNum} ORDER BY rowid LIMIT 500";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var ts  = hasTs    && !reader.IsDBNull(1) ? ParseOrNow(reader.GetValue(1)?.ToString()) : DateTime.UtcNow;
            var lvl = hasLevel && !reader.IsDBNull(2) ? LogFileParser.NormalizeLevel(reader.GetValue(2)?.ToString() ?? "INFO") : "INFO";
            var msg = reader.GetValue(hasTs && hasLevel ? 3 : hasTs || hasLevel ? 2 : 1)?.ToString() ?? "";

            entries.Add(new LogEntry { Source = sourceName, LogFile = dbPath, Timestamp = ts, Level = lvl, Message = msg, IngestedAt = DateTime.UtcNow });
        }

        return entries;
    }

    private static DateTime ParseOrNow(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return DateTime.UtcNow;
        if (DateTimeOffset.TryParse(s, out var dto)) return dto.UtcDateTime;
        if (DateTime.TryParse(s, null,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt)) return dt;
        return DateTime.UtcNow;
    }
}
