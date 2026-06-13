using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LogMind.Infrastructure.Services;

public static class MessageNormalizer
{
    private const int MaxNormalizedLength = 1000;

    // ISO 8601 / common log timestamps: 2026-06-11T00:01:10, 2026-06-11 00:01:10.225 +03:00
    private static readonly Regex Timestamps = new(
        @"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(\.\d+)?(\s?[+-]\d{2}:?\d{2})?",
        RegexOptions.Compiled);

    // Standalone date: 11/06/2026, 2026-06-11
    private static readonly Regex Dates = new(
        @"\b\d{1,4}[\/\-]\d{1,2}[\/\-]\d{2,4}\b",
        RegexOptions.Compiled);

    // Standalone time: 14:32:01, 14:32:01.123
    private static readonly Regex Times = new(
        @"\b\d{2}:\d{2}:\d{2}(\.\d+)?\b",
        RegexOptions.Compiled);

    // GUIDs / UUIDs
    private static readonly Regex Guids = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);

    // Hex memory addresses: 0x7f3a1b2c, 0X1A2B
    private static readonly Regex HexAddresses = new(
        @"\b0[xX][0-9a-fA-F]+\b",
        RegexOptions.Compiled);

    // Port numbers: :5432, :8080, :443
    private static readonly Regex Ports = new(
        @":\d{2,5}\b",
        RegexOptions.Compiled);

    // Source file line references: line 342, at line 12, :line 99
    private static readonly Regex LineNumbers = new(
        @"\b(line|ln|col|column)\s*:?\s*\d+\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Stack frame line numbers: :line 342) or (MyFile.cs:342)
    private static readonly Regex SourceLines = new(
        @"\.cs:\d+",
        RegexOptions.Compiled);

    // Retry / attempt counters: retry 3 of 5, attempt 2/3, attempt 2 of 3
    private static readonly Regex RetryCounters = new(
        @"\b(retry|attempt|try)\s+\d+\s*(of|\/)\s*\d+\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Duration / timeout values: 30000ms, 5s, 120 seconds, 2 minutes
    private static readonly Regex Durations = new(
        @"\b\d+\s*(ms|milliseconds?|seconds?|minutes?|hours?|s|m|h)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Standalone large integers that are clearly dynamic (IDs, sizes, counts > 3 digits)
    private static readonly Regex LargeIntegers = new(
        @"\b\d{4,}\b",
        RegexOptions.Compiled);

    // Runs of 2+ whitespace characters (including newlines)
    private static readonly Regex Whitespace = new(
        @"\s{2,}",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns a stable, normalized form of a raw log message suitable for hashing
    /// and string-similarity comparisons. Dynamic values (timestamps, IDs, ports,
    /// memory addresses, retry counters, durations) are replaced with placeholders.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw;

        s = Timestamps.Replace(s, "<ts>");
        s = Dates.Replace(s, "<date>");
        s = Times.Replace(s, "<time>");
        s = Guids.Replace(s, "<guid>");
        s = HexAddresses.Replace(s, "<addr>");
        s = Ports.Replace(s, ":<port>");
        s = LineNumbers.Replace(s, "<ln>");
        s = SourceLines.Replace(s, ".cs:<ln>");
        s = RetryCounters.Replace(s, "<retry>");
        s = Durations.Replace(s, "<dur>");
        s = LargeIntegers.Replace(s, "<n>");

        s = s.ToLowerInvariant();
        s = Whitespace.Replace(s, " ").Trim();

        if (s.Length > MaxNormalizedLength)
            s = s[..MaxNormalizedLength];

        return s;
    }

    /// <summary>
    /// Returns a lowercase SHA-256 hex string over "source::normalizedMessage".
    /// Source is included so identical errors in different systems produce different hashes.
    /// </summary>
    public static string ComputeHash(string source, string normalizedMessage)
    {
        var input = $"{source ?? string.Empty}::{normalizedMessage ?? string.Empty}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
