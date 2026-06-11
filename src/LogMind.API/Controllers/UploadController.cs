using LogMind.Infrastructure.Data;
using LogMind.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly LogMindDbContext _db;

    public UploadController(LogMindDbContext db) => _db = db;

    /// <summary>
    /// Upload a .log, .txt, or .csv file. Entries are parsed in-memory and saved immediately.
    /// </summary>
    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)] // 50 MB
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string source = "Upload")
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".log" or ".txt" or ".csv"))
            return BadRequest(new { error = "Only .log, .txt, and .csv files are supported." });

        var isCsv = ext == ".csv";
        var label = string.IsNullOrWhiteSpace(source) ? "Upload" : source;

        await using var stream = file.OpenReadStream();

        var entries = isCsv
            ? await LogFileParser.ParseCsvStreamAsync(stream, label, file.FileName)
            : await LogFileParser.ParseTextStreamAsync(stream, label, file.FileName);

        if (entries.Count > 0)
        {
            await _db.LogEntries.AddRangeAsync(entries);
            await _db.SaveChangesAsync();
        }

        return Ok(new { count = entries.Count, source = label, fileName = file.FileName });
    }
}
