using System.Text;
using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using Microsoft.Extensions.Logging;
using MimeKit;
using MsgReader.Outlook;
using UglyToad.PdfPig;

namespace InsuranceExtraction.Application.Services;

public class EmailParsingService : IEmailParsingService
{
    private readonly ILogger<EmailParsingService> _logger;

    public EmailParsingService(ILogger<EmailParsingService> logger)
    {
        _logger = logger;
    }

    public async Task<EmailContent> ParseEmailAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".msg"
            ? await ParseMsgAsync(filePath)
            : await ParseEmlAsync(filePath);
    }

    // ─── EML ──────────────────────────────────────────────────────────────
    private async Task<EmailContent> ParseEmlAsync(string filePath)
    {
        var emailContent = new EmailContent { FilePath = filePath, SourceFileType = "EML" };

        try
        {
            var message = await MimeMessage.LoadAsync(filePath);

            emailContent.From = message.From.ToString();
            emailContent.To = message.To.ToString();
            emailContent.Subject = message.Subject ?? string.Empty;
            emailContent.Date = message.Date.UtcDateTime;
            emailContent.PlainTextBody = message.TextBody ?? string.Empty;
            emailContent.HtmlBody = message.HtmlBody ?? string.Empty;

            // Extract attachments from top-level attachment list
            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart part)
                    TryAddAttachment(part, emailContent);
            }

            // Also walk multipart structure to catch nested attachments
            if (message.Body is Multipart multipart)
                ExtractFromMultipart(multipart, emailContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse EML {FilePath}", filePath);
        }

        return emailContent;
    }

    // ─── MSG ──────────────────────────────────────────────────────────────
    private Task<EmailContent> ParseMsgAsync(string filePath)
    {
        var emailContent = new EmailContent { FilePath = filePath, SourceFileType = "MSG" };

        try
        {
            using var msg = new Storage.Message(filePath);

            emailContent.From = msg.Sender?.Email ?? msg.Sender?.DisplayName ?? string.Empty;
            emailContent.Subject = msg.Subject ?? string.Empty;
            emailContent.Date = msg.SentOn?.UtcDateTime ?? DateTime.UtcNow;

            // Body: prefer plain text, fall back to RTF-stripped HTML
            emailContent.PlainTextBody = msg.BodyText ?? string.Empty;
            emailContent.HtmlBody = msg.BodyHtml ?? string.Empty;

            // Attachments
            foreach (var att in msg.Attachments)
            {
                if (att is Storage.Attachment attachment)
                {
                    var fileName = attachment.FileName ?? "attachment";
                    var data = attachment.Data;
                    if (data == null || data.Length == 0) continue;

                    var attContent = new AttachmentContent
                    {
                        FileName = fileName,
                        ContentType = GetMimeTypeFromFileName(fileName)
                    };

                    try
                    {
                        using var ms = new MemoryStream(data);
                        attContent.ExtractedText = ExtractTextFromStream(ms, fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract MSG attachment {FileName}", fileName);
                        attContent.ExtractedText = "[Attachment extraction failed]";
                    }

                    emailContent.Attachments.Add(attContent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse MSG {FilePath}", filePath);
        }

        return Task.FromResult(emailContent);
    }

    // ─── Attachment helpers ────────────────────────────────────────────────
    private void TryAddAttachment(MimePart part, EmailContent emailContent)
    {
        var fileName = part.FileName ?? "unknown";

        var attContent = new AttachmentContent
        {
            FileName = fileName,
            ContentType = part.ContentType.MimeType
        };

        try
        {
            using var ms = new MemoryStream();
            part.Content?.DecodeTo(ms);
            ms.Position = 0;
            attContent.ExtractedText = ExtractTextFromStream(ms, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from attachment {FileName}", fileName);
            attContent.ExtractedText = "[Attachment extraction failed]";
        }

        emailContent.Attachments.Add(attContent);
    }

    private void ExtractFromMultipart(Multipart multipart, EmailContent emailContent)
    {
        foreach (var part in multipart)
        {
            if (part is MimePart mimePart && mimePart.IsAttachment)
            {
                var already = emailContent.Attachments.Any(a => a.FileName == (mimePart.FileName ?? "unknown"));
                if (!already)
                    TryAddAttachment(mimePart, emailContent);
            }
            else if (part is Multipart nested)
            {
                ExtractFromMultipart(nested, emailContent);
            }
        }
    }

    /// <summary>Dispatches text extraction based on the file extension.</summary>
    private string ExtractTextFromStream(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => ExtractPdfText(stream),
            ".docx" or ".doc" => DocumentParsingService.ExtractDocxTextFromStream(stream),
            ".xlsx" or ".xls" => DocumentParsingService.ExtractXlsxTextFromStreamAsync(stream).GetAwaiter().GetResult(),
            ".txt" or ".csv" => new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true).ReadToEnd(),
            _ when IsTextMime(fileName) => new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true).ReadToEnd(),
            _ => string.Empty   // binary types we can't read — skip silently
        };
    }

    private static string ExtractPdfText(Stream stream)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(stream);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static bool IsTextMime(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".txt" or ".csv" or ".log" or ".xml" or ".json" or ".html" or ".htm";
    }

    private static string GetMimeTypeFromFileName(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }
}
