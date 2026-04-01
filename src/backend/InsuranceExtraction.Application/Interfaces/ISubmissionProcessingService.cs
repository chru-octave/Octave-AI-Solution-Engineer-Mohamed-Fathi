namespace InsuranceExtraction.Application.Interfaces;

public interface ISubmissionProcessingService
{
    /// <summary>Process a single file (email or document) as one submission.</summary>
    Task<int> ProcessEmailAsync(string filePath);

    /// <summary>
    /// Process a primary file with extra files injected as its attachments —
    /// all treated as a single submission sent to Claude together.
    /// </summary>
    Task<int> ProcessBundleAsync(string primaryFilePath, IEnumerable<string> attachmentPaths);
}
