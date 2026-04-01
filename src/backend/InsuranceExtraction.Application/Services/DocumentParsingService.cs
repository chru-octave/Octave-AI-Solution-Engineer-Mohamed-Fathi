using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace InsuranceExtraction.Application.Services;

/// <summary>
/// Parses standalone documents (PDF, DOCX, XLSX, TXT, CSV, ZIP) into an EmailContent
/// so they flow through the same Claude extraction pipeline as emails.
/// </summary>
public class DocumentParsingService : IDocumentParsingService
{
    private static readonly HashSet<string> _supported = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".txt", ".csv", ".zip" };

    private readonly ILogger<DocumentParsingService> _logger;

    public DocumentParsingService(ILogger<DocumentParsingService> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return _supported.Contains(ext);
    }

    public async Task<EmailContent> ParseDocumentAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath);

        var content = new EmailContent
        {
            FilePath = filePath,
            From = "Direct Upload",
            Subject = fileName,
            Date = DateTime.UtcNow,
            SourceFileType = ext.TrimStart('.').ToUpperInvariant()
        };

        try
        {
            switch (ext)
            {
                case ".pdf":
                    content.PlainTextBody = ExtractPdfText(filePath);
                    break;

                case ".docx":
                case ".doc":
                    content.PlainTextBody = ExtractDocxText(filePath);
                    break;

                case ".xlsx":
                case ".xls":
                    content.PlainTextBody = await ExtractXlsxTextAsync(filePath);
                    break;

                case ".txt":
                case ".csv":
                    content.PlainTextBody = await File.ReadAllTextAsync(filePath);
                    break;

                case ".zip":
                    await ExtractZipAsync(filePath, content);
                    break;

                default:
                    _logger.LogWarning("Unsupported document type: {Ext}", ext);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document {FilePath}", filePath);
            content.PlainTextBody = $"[Document parsing failed: {ex.Message}]";
        }

        return content;
    }

    // ─── PDF ───────────────────────────────────────────────────────────────
    internal static string ExtractPdfText(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ExtractPdfTextFromStream(stream);
    }

    internal static string ExtractPdfText(Stream stream) => ExtractPdfTextFromStream(stream);

    private static string ExtractPdfTextFromStream(Stream stream)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(stream);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    // ─── DOCX ──────────────────────────────────────────────────────────────
    internal static string ExtractDocxText(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ExtractDocxTextFromStream(stream);
    }

    internal static string ExtractDocxTextFromStream(Stream stream)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var para in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                sb.AppendLine(para.InnerText);
            }
            return sb.ToString();
        }
        catch
        {
            // Fallback: use raw InnerText
            stream.Position = 0;
            using var doc = WordprocessingDocument.Open(stream, false);
            return doc.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
        }
    }

    // ─── XLSX ──────────────────────────────────────────────────────────────
    internal static async Task<string> ExtractXlsxTextAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return await ExtractXlsxTextFromStreamAsync(stream);
    }

    internal static Task<string> ExtractXlsxTextFromStreamAsync(Stream stream)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            using var wb = new XLWorkbook(stream);
            foreach (var ws in wb.Worksheets)
            {
                sb.AppendLine($"[Sheet: {ws.Name}]");
                foreach (var row in ws.RowsUsed())
                {
                    var cells = row.CellsUsed().Select(c => c.GetString());
                    sb.AppendLine(string.Join("\t", cells));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        });
    }

    // ─── ZIP ───────────────────────────────────────────────────────────────
    private async Task ExtractZipAsync(string filePath, EmailContent content)
    {
        using var archive = ZipFile.OpenRead(filePath);

        var firstBody = true;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories

            var entryExt = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (!_supported.Contains(entryExt)) continue;

            try
            {
                using var entryStream = entry.Open();
                using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms);
                ms.Position = 0;

                var extractedText = entryExt switch
                {
                    ".pdf" => ExtractPdfText(ms),
                    ".docx" or ".doc" => ExtractDocxTextFromStream(ms),
                    ".xlsx" or ".xls" => await ExtractXlsxTextFromStreamAsync(ms),
                    ".txt" or ".csv" => Encoding.UTF8.GetString(ms.ToArray()),
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(extractedText)) continue;

                if (firstBody)
                {
                    content.PlainTextBody = $"[From ZIP archive: {entry.Name}]\n{extractedText}";
                    firstBody = false;
                }
                else
                {
                    content.Attachments.Add(new AttachmentContent
                    {
                        FileName = entry.Name,
                        ContentType = GetMimeType(entryExt),
                        ExtractedText = extractedText
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract ZIP entry {Name}", entry.Name);
            }
        }
    }

    private static string GetMimeType(string ext) => ext switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        _ => "application/octet-stream"
    };
}
