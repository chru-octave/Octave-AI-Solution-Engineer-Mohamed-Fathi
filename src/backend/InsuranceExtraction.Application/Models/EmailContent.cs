namespace InsuranceExtraction.Application.Models;

public class EmailContent
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string PlainTextBody { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public List<AttachmentContent> Attachments { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    /// <summary>Original file type: EML, MSG, PDF, DOCX, XLSX, TXT, ZIP</summary>
    public string SourceFileType { get; set; } = "EML";
}

public class AttachmentContent
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
}
