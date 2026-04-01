using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceExtraction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly ISubmissionProcessingService _processingService;
    private readonly AppDbContext _db;
    private readonly ILogger<UploadController> _logger;
    private readonly IWebHostEnvironment _env;

    public UploadController(
        ISubmissionProcessingService processingService,
        AppDbContext db,
        ILogger<UploadController> logger,
        IWebHostEnvironment env)
    {
        _processingService = processingService;
        _db = db;
        _logger = logger;
        _env = env;
    }

    private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".eml", ".msg",                         // email formats
        ".pdf",                                 // direct PDFs
        ".docx", ".doc",                        // Word documents (ACORD forms)
        ".xlsx", ".xls",                        // Excel loss runs / SOVs
        ".txt", ".csv",                         // plain text
        ".zip"                                  // bundled archives
    };

    [HttpPost]
    [RequestSizeLimit(100_000_000)] // 100MB
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest("No files provided");

        var results = new List<object>();
        var uploadDir = Path.Combine(_env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadDir);

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName);
            if (!_allowedExtensions.Contains(ext))
            {
                results.Add(new
                {
                    fileName = file.FileName,
                    error = $"Unsupported file type '{ext}'. Accepted: .eml .msg .pdf .docx .xlsx .txt .csv .zip"
                });
                continue;
            }

            try
            {
                // Save file
                var filePath = Path.Combine(uploadDir, $"{Guid.NewGuid()}_{file.FileName}");
                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                // Process
                var submissionId = await _processingService.ProcessEmailAsync(filePath);

                results.Add(new
                {
                    fileName = file.FileName,
                    submissionId,
                    success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for {FileName}", file.FileName);
                results.Add(new { fileName = file.FileName, error = ex.Message });
            }
        }

        return Ok(results);
    }

    /// <summary>
    /// Bundle upload: all files are processed as ONE submission.
    /// The primary file is auto-detected (first .eml/.msg wins; otherwise the first file).
    /// All remaining files are injected as attachments of the primary before Claude processes them.
    /// </summary>
    [HttpPost("bundle")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadBundle(
        [FromForm] string? primaryFileName,
        List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest("No files provided");

        var uploadDir = Path.Combine(_env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadDir);

        // Validate all files first
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName);
            if (!_allowedExtensions.Contains(ext))
                return BadRequest($"Unsupported file type '{ext}' in bundle. Accepted: .eml .msg .pdf .docx .xlsx .txt .csv .zip");
        }

        // Save all files to disk
        var savedPaths = new Dictionary<string, string>(); // originalName → savedPath
        try
        {
            foreach (var file in files)
            {
                var savedPath = Path.Combine(uploadDir, $"{Guid.NewGuid()}_{file.FileName}");
                using var stream = System.IO.File.Create(savedPath);
                await file.CopyToAsync(stream);
                savedPaths[file.FileName] = savedPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bundle file save failed");
            return StatusCode(500, new { error = $"File save failed: {ex.Message}" });
        }

        // Determine primary file:
        // 1. If caller specified one by name, use it
        // 2. Otherwise prefer first .eml/.msg
        // 3. Otherwise fall back to first file
        string primaryPath;
        var emailExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".eml", ".msg" };

        if (!string.IsNullOrWhiteSpace(primaryFileName) && savedPaths.ContainsKey(primaryFileName))
        {
            primaryPath = savedPaths[primaryFileName];
        }
        else
        {
            var emailFile = files.FirstOrDefault(f => emailExtensions.Contains(Path.GetExtension(f.FileName)));
            var chosenName = emailFile?.FileName ?? files[0].FileName;
            primaryPath = savedPaths[chosenName];
        }

        var attachmentPaths = savedPaths.Values
            .Where(p => p != primaryPath)
            .ToList();

        try
        {
            var submissionId = await _processingService.ProcessBundleAsync(primaryPath, attachmentPaths);

            return Ok(new
            {
                submissionId,
                success = true,
                primaryFile = Path.GetFileName(primaryPath),
                attachmentFiles = attachmentPaths.Select(Path.GetFileName).ToList(),
                totalFiles = files.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bundle processing failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("status/{id}")]
    public async Task<IActionResult> GetStatus(int id)
    {
        var submission = await _db.Submissions
            .Select(s => new { s.SubmissionId, s.Status, s.ProcessedDate, s.ExtractionConfidence })
            .FirstOrDefaultAsync(s => s.SubmissionId == id);

        if (submission == null) return NotFound();
        return Ok(submission);
    }
}
