using InsuranceExtraction.Application.Models;

namespace InsuranceExtraction.Application.Interfaces;

public interface IEmailParsingService
{
    Task<EmailContent> ParseEmailAsync(string filePath);
}
