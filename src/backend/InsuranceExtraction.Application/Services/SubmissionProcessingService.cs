using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using InsuranceExtraction.Domain.Entities;
using InsuranceExtraction.Domain.Enums;
using InsuranceExtraction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InsuranceExtraction.Application.Services;

public class SubmissionProcessingService : ISubmissionProcessingService
{
    private static readonly HashSet<string> _emailExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".eml", ".msg" };

    private readonly IEmailParsingService _emailParser;
    private readonly IDocumentParsingService _documentParser;
    private readonly IClaudeExtractionService _claudeExtractor;
    private readonly AppDbContext _db;
    private readonly ILogger<SubmissionProcessingService> _logger;

    public SubmissionProcessingService(
        IEmailParsingService emailParser,
        IDocumentParsingService documentParser,
        IClaudeExtractionService claudeExtractor,
        AppDbContext db,
        ILogger<SubmissionProcessingService> logger)
    {
        _emailParser = emailParser;
        _documentParser = documentParser;
        _claudeExtractor = claudeExtractor;
        _db = db;
        _logger = logger;
    }

    // ─── Public API ────────────────────────────────────────────────────────

    public async Task<int> ProcessEmailAsync(string filePath)
    {
        _logger.LogInformation("Processing file: {FilePath}", filePath);
        try
        {
            var content = await ParseFileAsync(filePath);
            return await ProcessContentAsync(content, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File parsing failed for {FilePath}", filePath);
            return await SaveFailedSubmissionAsync(filePath, $"File parsing failed: {ex.Message}");
        }
    }

    public async Task<int> ProcessBundleAsync(string primaryFilePath, IEnumerable<string> attachmentPaths)
    {
        _logger.LogInformation("Processing bundle, primary: {Primary}", primaryFilePath);

        // 1. Parse the primary file
        var content = await ParseFileAsync(primaryFilePath);

        // 2. Parse each extra file and inject its text as an attachment on the primary
        foreach (var attPath in attachmentPaths)
        {
            try
            {
                var attDoc = await _documentParser.ParseDocumentAsync(attPath);

                // Combine the document body + any nested attachments it had (e.g. a ZIP)
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(attDoc.PlainTextBody);
                foreach (var nested in attDoc.Attachments.Where(a => !string.IsNullOrWhiteSpace(a.ExtractedText)))
                {
                    sb.AppendLine();
                    sb.AppendLine($"[Nested: {nested.FileName}]");
                    sb.AppendLine(nested.ExtractedText);
                }

                content.Attachments.Add(new AttachmentContent
                {
                    FileName = Path.GetFileName(attPath),
                    ContentType = GetMimeType(Path.GetExtension(attPath)),
                    ExtractedText = sb.ToString().Trim()
                });

                _logger.LogInformation("  + injected bundle attachment: {File}", Path.GetFileName(attPath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bundle attachment parse failed: {Path}", attPath);
                // Add a placeholder so Claude knows the file existed
                content.Attachments.Add(new AttachmentContent
                {
                    FileName = Path.GetFileName(attPath),
                    ContentType = "application/octet-stream",
                    ExtractedText = "[Attachment could not be parsed]"
                });
            }
        }

        try
        {
            return await ProcessContentAsync(content, primaryFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bundle processing failed for {FilePath}", primaryFilePath);
            return await SaveFailedSubmissionAsync(primaryFilePath, $"Bundle processing failed: {ex.Message}");
        }
    }

    // ─── Shared pipeline ───────────────────────────────────────────────────

    private async Task<EmailContent> ParseFileAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return _emailExtensions.Contains(ext)
            ? await _emailParser.ParseEmailAsync(filePath)
            : await _documentParser.ParseDocumentAsync(filePath);
    }

    private async Task<int> ProcessContentAsync(EmailContent emailContent, string originalFilePath)
    {
        var submission = new Submission
        {
            EmailFilePath = originalFilePath,
            SubmissionDate = DateTime.UtcNow,
            ProcessedDate = DateTime.UtcNow,
            Status = SubmissionStatus.Pending
        };

        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        try
        {
            // Populate envelope fields from the parsed content
            submission.EmailSubject = emailContent.Subject;
            submission.EmailFrom = emailContent.From;
            submission.AttachmentList = string.Join(", ", emailContent.Attachments.Select(a => a.FileName));
            if (emailContent.Date != default) submission.SubmissionDate = emailContent.Date;

            // Claude extraction
            SubmissionData extracted;
            try
            {
                extracted = await _claudeExtractor.ExtractDataAsync(emailContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude extraction failed for {FilePath}", originalFilePath);
                submission.Status = SubmissionStatus.Failed;
                submission.FailureReason = $"AI extraction failed: {ex.Message}";
                submission.ProcessedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return submission.SubmissionId;
            }

            submission.ExtractionConfidence = extracted.Confidence;
            submission.Status = extracted.Confidence < 0.7
                ? SubmissionStatus.NeedsReview
                : SubmissionStatus.Processed;

            // Upsert Insured
            if (extracted.Insured != null && !string.IsNullOrWhiteSpace(extracted.Insured.CompanyName))
            {
                var insured = await _db.Insureds
                    .FirstOrDefaultAsync(i => i.CompanyName == extracted.Insured.CompanyName);

                if (insured == null) { insured = new Insured(); _db.Insureds.Add(insured); }

                insured.CompanyName = extracted.Insured.CompanyName;
                insured.Address = extracted.Insured.Address;
                insured.City = extracted.Insured.City;
                insured.State = extracted.Insured.State;
                insured.ZipCode = extracted.Insured.ZipCode;
                insured.Industry = extracted.Insured.Industry;
                insured.YearsInBusiness = extracted.Insured.YearsInBusiness;
                insured.DotNumber = extracted.Insured.DotNumber;
                insured.McNumber = extracted.Insured.McNumber;
                insured.AnnualRevenue = extracted.Insured.AnnualRevenue;

                await _db.SaveChangesAsync();
                submission.InsuredId = insured.InsuredId;
            }

            // Upsert Broker
            if (extracted.Broker != null && !string.IsNullOrWhiteSpace(extracted.Broker.BrokerName))
            {
                var broker = await _db.Brokers
                    .FirstOrDefaultAsync(b => b.BrokerName == extracted.Broker.BrokerName);

                if (broker == null) { broker = new Broker(); _db.Brokers.Add(broker); }

                broker.BrokerName = extracted.Broker.BrokerName ?? string.Empty;
                broker.AgencyName = extracted.Broker.AgencyName ?? string.Empty;
                broker.Email = extracted.Broker.Email;
                broker.Phone = extracted.Broker.Phone;
                broker.Address = extracted.Broker.Address;
                broker.City = extracted.Broker.City;
                broker.State = extracted.Broker.State;
                broker.ZipCode = extracted.Broker.ZipCode;
                broker.LicenseNumber = extracted.Broker.LicenseNumber;

                await _db.SaveChangesAsync();
                submission.BrokerId = broker.BrokerId;
            }

            // Coverage Lines
            foreach (var cl in extracted.CoverageLines)
            {
                _db.CoverageLines.Add(new CoverageLine
                {
                    SubmissionId = submission.SubmissionId,
                    LineOfBusiness = ParseEnum<LineOfBusiness>(cl.LineOfBusiness, LineOfBusiness.Other),
                    RequestedLimit = cl.RequestedLimit,
                    TargetPremium = cl.TargetPremium,
                    CurrentPremium = cl.CurrentPremium,
                    Notes = cl.Notes,
                    EffectiveDate = ParseDate(cl.EffectiveDate),
                    ExpirationDate = ParseDate(cl.ExpirationDate)
                });
            }

            // Exposures
            foreach (var exp in extracted.Exposures)
            {
                _db.Exposures.Add(new Exposure
                {
                    SubmissionId = submission.SubmissionId,
                    ExposureType = ParseEnum<ExposureType>(exp.ExposureType, ExposureType.Other),
                    Quantity = exp.Quantity,
                    Description = exp.Description
                });
            }

            // Loss History
            foreach (var loss in extracted.Losses)
            {
                _db.LossHistory.Add(new LossHistory
                {
                    SubmissionId = submission.SubmissionId,
                    LossDate = ParseDate(loss.LossDate),
                    LossAmount = loss.LossAmount,
                    LossType = loss.LossType,
                    Description = loss.Description,
                    Status = loss.Status,
                    ClaimNumber = loss.ClaimNumber,
                    PolicyYear = loss.PolicyYear,
                    PaidAmount = loss.PaidAmount,
                    ReserveAmount = loss.ReserveAmount,
                    IsClosed = loss.IsClosed
                });
            }

            submission.ProcessedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Processed submission {Id} with confidence {Confidence:P0}  ({Attachments} attachments)",
                submission.SubmissionId, submission.ExtractionConfidence,
                emailContent.Attachments.Count);

            return submission.SubmissionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for {FilePath}", originalFilePath);
            submission.Status = SubmissionStatus.Failed;
            submission.FailureReason = $"Processing error: {ex.Message}";
            submission.ProcessedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return submission.SubmissionId;
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static T ParseEnum<T>(string? value, T defaultValue) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return Enum.TryParse<T>(value, true, out var result) ? result : defaultValue;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        return DateTime.TryParse(dateStr, out var date) ? date : null;
    }

    /// <summary>Creates a Failed submission record when parsing crashes before a record even exists.</summary>
    private async Task<int> SaveFailedSubmissionAsync(string filePath, string reason)
    {
        var submission = new Submission
        {
            EmailFilePath = filePath,
            SubmissionDate = DateTime.UtcNow,
            ProcessedDate = DateTime.UtcNow,
            Status = SubmissionStatus.Failed,
            FailureReason = reason
        };
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();
        return submission.SubmissionId;
    }

    private static string GetMimeType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        _ => "application/octet-stream"
    };
}
