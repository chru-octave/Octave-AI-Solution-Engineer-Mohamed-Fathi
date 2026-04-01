using System.Text;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace InsuranceExtraction.Application.Services;

public class ClaudeExtractionService : IClaudeExtractionService
{
    private readonly ILogger<ClaudeExtractionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AnthropicClient _client;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const string SystemPrompt = @"You are an expert insurance data extraction specialist for commercial auto and trucking insurance.

Extract structured data from the provided document(s). The input may be an email, a PDF, a Word document, an Excel spreadsheet, a plain text file, or a combination of files from a ZIP archive.

CRITICAL: The PRIMARY CONTENT section contains the main submission data. Attachments/additional sections provide supplementary detail (loss runs, schedules, etc.).

Return ONLY valid JSON with this schema:
{
  ""insured"": {
    ""companyName"": ""string (from email body or extract from filename if unclear)"",
    ""address"": ""string"", ""city"": ""string"", ""state"": ""string"",
    ""zipCode"": ""string"", ""industry"": ""string"",
    ""yearsInBusiness"": ""number or null"",
    ""dotNumber"": ""string or null"",
    ""mcNumber"": ""string or null"",
    ""annualRevenue"": ""number or null""
  },
  ""broker"": {
    ""brokerName"": ""string (often in email signature)"",
    ""agencyName"": ""string"",
    ""email"": ""string"", ""phone"": ""string""
  },
  ""coverageLines"": [
    {
      ""lineOfBusiness"": ""AutoLiability|GeneralLiability|WorkersCompensation|Property|Umbrella|Cargo|PhysicalDamage|MotorTruckCargo|NonTruckingLiability|Other"",
      ""requestedLimit"": ""string"",
      ""targetPremium"": ""number or null"",
      ""currentPremium"": ""number or null"",
      ""effectiveDate"": ""YYYY-MM-DD or null"",
      ""expirationDate"": ""YYYY-MM-DD or null""
    }
  ],
  ""exposures"": [
    {
      ""exposureType"": ""Trucks|PowerUnits|Tractors|Trailers|Drivers|Miles|AnnualMiles|Payroll|Revenue|Locations|Employees|Radius|States|Other"",
      ""quantity"": ""number"",
      ""description"": ""string or null""
    }
  ],
  ""losses"": [
    {
      ""lossDate"": ""YYYY-MM-DD or null"",
      ""lossAmount"": ""number"",
      ""lossType"": ""string"",
      ""description"": ""string or null"",
      ""status"": ""string"",
      ""claimNumber"": ""string or null"",
      ""policyYear"": ""string or null"",
      ""paidAmount"": ""number or null"",
      ""reserveAmount"": ""number or null"",
      ""isClosed"": ""boolean""
    }
  ],
  ""confidence"": ""number (0.0-1.0)""
}

RULES:
- Primary content section = main source for coverage, exposures, company info
- Attachments/additional sections = supplementary (loss runs, schedules)
- For Excel files, each tab may contain different data (exposures, losses, premiums)
- For ZIP archives, combine all extracted documents as one submission
- If company name unclear, derive it from the filename (remove extension and separators)
- Broker info often appears in email signatures or document footers
- Extract ALL data present, use null for missing fields
- Return ONLY the JSON object, no markdown, no explanation";

    public ClaudeExtractionService(
        ILogger<ClaudeExtractionService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        var apiKey = configuration["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic:ApiKey not configured");
        _client = new AnthropicClient(apiKey);

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(exception, "Retry {RetryCount} after {TimeSpan}s", retryCount, timeSpan.TotalSeconds);
                });
    }

    public async Task<SubmissionData> ExtractDataAsync(EmailContent emailContent)
    {
        var prompt = BuildPrompt(emailContent);
        var model = _configuration["Anthropic:Model"] ?? AnthropicModels.Claude46Sonnet;
        var maxTokens = int.Parse(_configuration["Anthropic:MaxTokens"] ?? "4096");

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var messages = new List<Message>
            {
                new(RoleType.User, prompt)
            };

            var systemMessages = new List<SystemMessage>
            {
                new(SystemPrompt)
            };

            var parameters = new MessageParameters
            {
                Model = model,
                MaxTokens = maxTokens,
                System = systemMessages,
                Messages = messages,
                Stream = false
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);
            var rawJson = response.Content.FirstOrDefault()?.ToString() ?? "{}";

            // Strip markdown code fences if present
            rawJson = rawJson.Trim();
            if (rawJson.StartsWith("```"))
            {
                var start = rawJson.IndexOf('{');
                var end = rawJson.LastIndexOf('}');
                if (start >= 0 && end >= 0)
                    rawJson = rawJson[start..(end + 1)];
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<SubmissionData>(rawJson, options) ?? new SubmissionData();

            // Fallback: extract company name from filename
            if (string.IsNullOrWhiteSpace(data.Insured?.CompanyName))
            {
                var fileName = Path.GetFileNameWithoutExtension(emailContent.FilePath);
                var companyName = fileName.Split(" - ").FirstOrDefault()?.Trim() ?? fileName;
                if (data.Insured == null) data.Insured = new InsuredData();
                data.Insured.CompanyName = companyName;
            }

            return data;
        });
    }

    private static string BuildPrompt(EmailContent emailContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SOURCE TYPE: {emailContent.SourceFileType}");
        sb.AppendLine($"FILENAME: {Path.GetFileName(emailContent.FilePath)}");
        sb.AppendLine($"FROM: {emailContent.From}");
        sb.AppendLine($"SUBJECT: {emailContent.Subject}");
        sb.AppendLine($"DATE: {emailContent.Date:yyyy-MM-dd}");
        sb.AppendLine();

        var sourceLabel = emailContent.SourceFileType.ToUpperInvariant() switch
        {
            "EML" or "MSG" => "EMAIL BODY (PRIMARY DATA SOURCE)",
            "PDF" => "PDF DOCUMENT CONTENT (PRIMARY DATA SOURCE)",
            "DOCX" or "DOC" => "WORD DOCUMENT CONTENT (PRIMARY DATA SOURCE)",
            "XLSX" or "XLS" => "SPREADSHEET CONTENT (PRIMARY DATA SOURCE)",
            "ZIP" => "ARCHIVE PRIMARY DOCUMENT (PRIMARY DATA SOURCE)",
            _ => "DOCUMENT CONTENT (PRIMARY DATA SOURCE)"
        };

        sb.AppendLine($"=== {sourceLabel} ===");
        sb.AppendLine(emailContent.PlainTextBody);

        if (!string.IsNullOrWhiteSpace(emailContent.HtmlBody) &&
            string.IsNullOrWhiteSpace(emailContent.PlainTextBody))
        {
            // Strip HTML tags for a rough plain text fallback
            var htmlText = System.Text.RegularExpressions.Regex.Replace(emailContent.HtmlBody, "<[^>]+>", " ");
            sb.AppendLine(htmlText);
        }

        if (emailContent.Attachments.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== ATTACHMENTS (SUPPLEMENTARY DATA) ===");
            foreach (var att in emailContent.Attachments)
            {
                if (!string.IsNullOrWhiteSpace(att.ExtractedText))
                {
                    sb.AppendLine($"--- {att.FileName} ---");
                    // Limit attachment text to avoid token overflow
                    var text = att.ExtractedText.Length > 8000
                        ? att.ExtractedText[..8000] + "\n[truncated]"
                        : att.ExtractedText;
                    sb.AppendLine(text);
                }
            }
        }

        return sb.ToString();
    }
}
