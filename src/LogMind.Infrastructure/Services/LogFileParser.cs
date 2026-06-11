using LogMind.Core.Models;
using System.Text.RegularExpressions;

namespace LogMind.Infrastructure.Services;

public static class LogFileParser
{
    // Shared fragment strings for readability
    private const string TS    = @"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?(?:Z|[+-]\d{2}:?\d{2})?";
    private const string LVL   = @"TRACE|DEBUG|INFO|WARN(?:ING)?|ERROR|FATAL|CRITICAL";

    internal static readonly Regex[] LogPatterns =
    [
        // 2024-01-01 00:00:00[.frac] LEVEL message  (also handles comma-ms: Python logging)
        new Regex($@"^(?<timestamp>{TS})\s+(?<level>{LVL})\s+(?<message>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // [LEVEL] 2024-01-01T00:00:00 message
        new Regex($@"^\[(?<level>{LVL})\]\s+(?<timestamp>{TS})\s+(?<message>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // 01/01/2024 00:00:00 LEVEL message  (US date format)
        new Regex(@"^(?<timestamp>\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2}:\d{2})\s+(?<level>ERROR|WARN|INFO|DEBUG|FATAL)\s+(?<message>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // [2024-01-01 00:00:00] [LEVEL] message  /  [2024-01-01 00:00:00] LEVEL message
        new Regex($@"^\[(?<timestamp>{TS})\]\s+\[?(?<level>{LVL})\]?\s+(?<message>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // 2024-01-01 00:00:00 | LEVEL | message  /  2024-01-01 00:00:00 - LEVEL - message
        new Regex($@"^(?<timestamp>{TS})\s*[-|]\s*(?<level>{LVL})\s*[-|]\s*(?<message>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // LEVEL: 2024-01-01 00:00:00 message  /  LEVEL  2024-01-01T...  message
        new Regex($@"^(?<level>{LVL})[:\s]+(?<timestamp>{TS})\s+(?<message>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public static async Task<List<LogEntry>> ParseTextStreamAsync(Stream stream, string sourceName, string filePath)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var entries = new List<LogEntry>();
        string? line;
        string? pendingMessage = null;
        string? pendingLevel = null;
        DateTime? pendingTimestamp = null;
        var stackTraceBuffer = new List<string>();

        while ((line = await reader.ReadLineAsync()) != null)
        {
            var matched = TryParseLogLine(line, out var timestamp, out var level, out var message);
            if (matched)
            {
                if (pendingMessage != null)
                {
                    entries.Add(CreateEntry(sourceName, filePath, pendingTimestamp!.Value, pendingLevel!, pendingMessage, stackTraceBuffer));
                    stackTraceBuffer.Clear();
                }
                pendingTimestamp = timestamp;
                pendingLevel = NormalizeLevel(level!);
                pendingMessage = message;
            }
            else if (pendingMessage != null)
            {
                stackTraceBuffer.Add(line);
            }
        }

        if (pendingMessage != null)
            entries.Add(CreateEntry(sourceName, filePath, pendingTimestamp!.Value, pendingLevel!, pendingMessage, stackTraceBuffer));

        return entries;
    }

    public static async Task<List<LogEntry>> ParseCsvStreamAsync(Stream stream, string sourceName, string filePath)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var entries = new List<LogEntry>();
        string? line;
        var isHeader = true;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (isHeader) { isHeader = false; continue; }
            var parts = line.Split(',', 3);
            DateTime ts = DateTime.UtcNow;
            string lvl = "INFO", msg = line;

            if (parts.Length >= 3 && DateTime.TryParse(parts[0].Trim('"'), out var parsed))
            {
                ts = parsed;
                lvl = NormalizeLevel(parts[1].Trim('"', ' '));
                msg = parts[2].Trim('"');
            }

            entries.Add(CreateEntry(sourceName, filePath, ts, lvl, msg, []));
        }

        return entries;
    }

    public static bool TryParseLogLine(string line, out DateTime timestamp, out string? level, out string? message)
    {
        timestamp = default;
        level = null;
        message = null;

        foreach (var pattern in LogPatterns)
        {
            var match = pattern.Match(line);
            if (!match.Success) continue;
            // Normalize comma-separated milliseconds (e.g. Python logging "12:34:56,789" → "12:34:56.789")
            var tsRaw = match.Groups["timestamp"].Value;
            var tsNorm = System.Text.RegularExpressions.Regex.Replace(tsRaw, @"(\d{2}:\d{2}:\d{2}),(\d+)", "$1.$2");
            if (!DateTime.TryParse(tsNorm, null, System.Globalization.DateTimeStyles.RoundtripKind, out timestamp)) continue;
            level = match.Groups["level"].Value;
            message = match.Groups["message"].Value;
            return true;
        }
        return false;
    }

    public static string NormalizeLevel(string level) => level.ToUpperInvariant() switch
    {
        "WARNING" => "WARN",
        "CRITICAL" => "FATAL",
        _ => level.ToUpperInvariant()
    };

    public static LogEntry CreateEntry(string source, string filePath, DateTime timestamp, string level, string message, List<string> stackLines) =>
        new()
        {
            Source = source,
            LogFile = filePath,
            Timestamp = timestamp,
            Level = level,
            Message = message,
            StackTrace = stackLines.Count > 0 ? string.Join(Environment.NewLine, stackLines) : null,
            IngestedAt = DateTime.UtcNow
        };
}
