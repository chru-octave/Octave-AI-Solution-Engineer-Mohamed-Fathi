using InsuranceExtraction.Application.Models;

namespace InsuranceExtraction.Application.Interfaces;

public interface IClaudeExtractionService
{
    Task<SubmissionData> ExtractDataAsync(EmailContent emailContent);
}
