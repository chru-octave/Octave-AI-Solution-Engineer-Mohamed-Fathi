using InsuranceExtraction.Application.Models;

namespace InsuranceExtraction.Application.Interfaces;

public interface IDocumentParsingService
{
    /// <summary>Returns true if this service can handle the given file extension.</summary>
    bool CanHandle(string filePath);

    /// <summary>
    /// Parses a standalone document (PDF, DOCX, XLSX, TXT, ZIP) into an EmailContent
    /// so the same Claude extraction pipeline can be used.
    /// </summary>
    Task<EmailContent> ParseDocumentAsync(string filePath);
}
