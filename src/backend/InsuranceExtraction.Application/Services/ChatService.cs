using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Tool = Anthropic.SDK.Common.Tool;
using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using InsuranceExtraction.Domain.Enums;
using InsuranceExtraction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceExtraction.Application.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly ILogger<ChatService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string SystemPrompt = @"You are an expert AI analyst for a commercial insurance submission management system.
You help underwriters and analysts interrogate and reason over insurance submission data.

DATABASE SCHEMA (SQLite):
• Submissions   : SubmissionId, EmailSubject, EmailFrom, SubmissionDate, ProcessedDate,
                  Status (Pending/Processed/NeedsReview/Failed), ExtractionConfidence (0.0–1.0),
                  AttachmentList, InsuredId (FK), BrokerId (FK)
• Insureds      : InsuredId, CompanyName, Address, City, State, ZipCode, Industry,
                  YearsInBusiness, DotNumber, McNumber, AnnualRevenue
• Brokers       : BrokerId, BrokerName, AgencyName, Email, Phone, Address, City, State, ZipCode, LicenseNumber
• CoverageLines : CoverageId, SubmissionId (FK), LineOfBusiness
                  (AutoLiability/GeneralLiability/WorkersCompensation/Property/Umbrella/
                   Cargo/PhysicalDamage/MotorTruckCargo/NonTruckingLiability/Other),
                  RequestedLimit, TargetPremium, CurrentPremium, EffectiveDate, ExpirationDate, Notes
• Exposures     : ExposureId, SubmissionId (FK), ExposureType
                  (Trucks/PowerUnits/Tractors/Trailers/Drivers/Miles/AnnualMiles/
                   Payroll/Revenue/Locations/Employees/Radius/States/Other),
                  Quantity, Description
• LossHistory   : LossId, SubmissionId (FK), LossDate, LossAmount, LossType, Description,
                  Status, ClaimNumber, PolicyYear, PaidAmount, ReserveAmount, IsClosed

TOOLS:
• list_submissions  – filter/search submissions list
• get_submission    – full detail (coverages, exposures, losses) for one submission
• run_query         – execute a custom SELECT SQL for complex analysis
• compare_submissions – side-by-side diff of two submissions
• get_analytics     – aggregate statistics across all submissions

GUIDELINES:
- Always call a tool to fetch real data; never invent or assume values.
- For complex aggregation or cross-table questions, prefer run_query with SQL.
- Format your final answers in markdown: use **bold**, tables (|col|col|), and bullet lists.
- Show monetary values formatted as currency (e.g. $1,250,000).
- If the user mentions a submission ID, call get_submission automatically.";

    public ChatService(AppDbContext db, IConfiguration config, ILogger<ChatService> logger)
    {
        _db = db;
        _logger = logger;
        var apiKey = config["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey not configured");
        _client = new AnthropicClient(apiKey);
        _model = config["Anthropic:Model"] ?? AnthropicModels.Claude46Sonnet;
    }

    // ─── Public API ────────────────────────────────────────────────────────

    public async Task<ChatResponse> ChatAsync(List<ChatMessage> messages)
    {
        var tools = BuildTools();
        var anthropicMessages = messages
            .Select(m => new Message(
                m.Role == "user" ? RoleType.User : RoleType.Assistant,
                m.Content))
            .ToList();

        var toolCallRecords = new List<ToolCallRecord>();
        const int maxIterations = 10;

        for (int i = 0; i < maxIterations; i++)
        {
            var parameters = new MessageParameters
            {
                Model = _model,
                MaxTokens = 4096,
                System = new List<SystemMessage> { new(SystemPrompt) },
                Messages = anthropicMessages,
                Tools = tools,
                Stream = false
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);

            // No tool calls → final answer
            if (response.ToolCalls == null || response.ToolCalls.Count == 0)
            {
                var answer = response.Content
                    .OfType<TextContent>()
                    .FirstOrDefault()?.Text
                    ?? response.Message.ToString()
                    ?? string.Empty;

                return new ChatResponse { Answer = answer, ToolCalls = toolCallRecords };
            }

            _logger.LogInformation(
                "Chat iteration {I}: {Count} tool call(s)",
                i + 1, response.ToolCalls.Count);

            // Add assistant turn (with tool-use blocks) to history
            anthropicMessages.Add(response.Message);

            // Execute every tool call Claude requested.
            // FromFunc creates sync delegates → use Invoke<T>(), not InvokeAsync<T>().
            // InvokeAsync expects a Task-returning delegate; calling it on a sync lambda
            // throws "did not return a valid Task".
            foreach (var toolCall in response.ToolCalls)
            {
                _logger.LogInformation("  → tool: {Name}", toolCall.Name);
                string toolResult;
                try
                {
                    toolResult = toolCall.Invoke<string>() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tool {Name} threw during invocation", toolCall.Name);
                    toolResult = System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message });
                }

                anthropicMessages.Add(new Message(toolCall, toolResult));
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.Name ?? string.Empty,
                    Input    = toolCall.Arguments?.ToJsonString() ?? string.Empty,
                    Output   = toolResult ?? string.Empty
                });
            }
        }

        return new ChatResponse
        {
            Answer = "I reached the maximum number of reasoning steps. Please try rephrasing your question.",
            ToolCalls = toolCallRecords
        };
    }

    // ─── Tool definitions ──────────────────────────────────────────────────

    private List<Anthropic.SDK.Common.Tool> BuildTools() => new()
    {
        // ── 1. List submissions ─────────────────────────────────────────────
        Tool.FromFunc(
            "list_submissions",
            ([FunctionParameter("Status filter: Pending, Processed, NeedsReview, Failed", false)] string? status,
             [FunctionParameter("Partial match on insured company name", false)] string? insuredName,
             [FunctionParameter("Partial match on broker name or agency", false)] string? brokerName,
             [FunctionParameter("Line of business filter (e.g. AutoLiability, Cargo)", false)] string? lineOfBusiness,
             [FunctionParameter("Minimum extraction confidence 0.0–1.0", false)] double? minConfidence,
             [FunctionParameter("Earliest submission date YYYY-MM-DD", false)] string? dateFrom,
             [FunctionParameter("Latest submission date YYYY-MM-DD", false)] string? dateTo,
             [FunctionParameter("Maximum results to return (1–50, default 20)", false)] int? limit) =>
            {
                try
                {
                    var query = _db.Submissions
                        .Include(s => s.Insured)
                        .Include(s => s.Broker)
                        .AsQueryable();

                    if (!string.IsNullOrWhiteSpace(status)
                        && Enum.TryParse<SubmissionStatus>(status, true, out var statusEnum))
                        query = query.Where(s => s.Status == statusEnum);

                    if (!string.IsNullOrWhiteSpace(insuredName))
                        query = query.Where(s => s.Insured != null
                            && s.Insured.CompanyName.ToLower().Contains(insuredName.ToLower()));

                    if (!string.IsNullOrWhiteSpace(brokerName))
                        query = query.Where(s => s.Broker != null
                            && (s.Broker.BrokerName.ToLower().Contains(brokerName.ToLower())
                             || s.Broker.AgencyName.ToLower().Contains(brokerName.ToLower())));

                    if (!string.IsNullOrWhiteSpace(lineOfBusiness)
                        && Enum.TryParse<LineOfBusiness>(lineOfBusiness, true, out var lobEnum))
                        query = query.Where(s => s.CoverageLines.Any(c => c.LineOfBusiness == lobEnum));

                    if (minConfidence.HasValue)
                        query = query.Where(s => s.ExtractionConfidence >= minConfidence.Value);

                    if (!string.IsNullOrWhiteSpace(dateFrom)
                        && DateTime.TryParse(dateFrom, out var df))
                        query = query.Where(s => s.SubmissionDate >= df);

                    if (!string.IsNullOrWhiteSpace(dateTo)
                        && DateTime.TryParse(dateTo, out var dt))
                        query = query.Where(s => s.SubmissionDate <= dt);

                    var take = Math.Clamp(limit ?? 20, 1, 50);
                    var items = query
                        .OrderByDescending(s => s.SubmissionDate)
                        .Take(take)
                        .Select(s => new
                        {
                            s.SubmissionId,
                            s.EmailSubject,
                            s.SubmissionDate,
                            s.Status,
                            s.ExtractionConfidence,
                            insuredName = s.Insured != null ? s.Insured.CompanyName : null,
                            brokerName  = s.Broker  != null ? s.Broker.BrokerName   : null,
                            agencyName  = s.Broker  != null ? s.Broker.AgencyName   : null,
                        })
                        .ToList();

                    return JsonSerializer.Serialize(new { count = items.Count, submissions = items }, _jsonOpts);
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = ex.Message });
                }
            }
        ),

        // ── 2. Get full submission detail ──────────────────────────────────
        Tool.FromFunc(
            "get_submission",
            ([FunctionParameter("The numeric ID of the submission to retrieve", true)] int submissionId) =>
            {
                try
                {
                    var s = _db.Submissions
                        .Include(x => x.Insured)
                        .Include(x => x.Broker)
                        .Include(x => x.CoverageLines)
                        .Include(x => x.Exposures)
                        .Include(x => x.Losses)
                        .FirstOrDefault(x => x.SubmissionId == submissionId);

                    if (s == null)
                        return JsonSerializer.Serialize(new { error = $"Submission {submissionId} not found." });

                    return JsonSerializer.Serialize(new
                    {
                        s.SubmissionId,
                        s.EmailSubject,
                        s.EmailFrom,
                        s.SubmissionDate,
                        s.ProcessedDate,
                        s.Status,
                        s.ExtractionConfidence,
                        s.AttachmentList,
                        insured = s.Insured == null ? null : new
                        {
                            s.Insured.CompanyName,
                            s.Insured.Address,
                            s.Insured.City,
                            s.Insured.State,
                            s.Insured.Industry,
                            s.Insured.YearsInBusiness,
                            s.Insured.DotNumber,
                            s.Insured.McNumber,
                            s.Insured.AnnualRevenue
                        },
                        broker = s.Broker == null ? null : new
                        {
                            s.Broker.BrokerName,
                            s.Broker.AgencyName,
                            s.Broker.Email,
                            s.Broker.Phone
                        },
                        coverageLines = s.CoverageLines.Select(c => new
                        {
                            c.LineOfBusiness,
                            c.RequestedLimit,
                            c.TargetPremium,
                            c.CurrentPremium,
                            c.EffectiveDate,
                            c.ExpirationDate,
                            c.Notes
                        }),
                        exposures = s.Exposures.Select(e => new
                        {
                            e.ExposureType,
                            e.Quantity,
                            e.Description
                        }),
                        losses = s.Losses.Select(l => new
                        {
                            l.LossDate,
                            l.LossAmount,
                            l.LossType,
                            l.PaidAmount,
                            l.ReserveAmount,
                            l.IsClosed,
                            l.ClaimNumber,
                            l.PolicyYear
                        })
                    }, _jsonOpts);
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = ex.Message });
                }
            }
        ),

        // ── 3. Run a custom SELECT SQL ─────────────────────────────────────
        Tool.FromFunc(
            "run_query",
            ([FunctionParameter(
                "A read-only SELECT statement to execute against the SQLite database. " +
                "Tables: Submissions, Insureds, Brokers, CoverageLines, Exposures, LossHistory. " +
                "Only SELECT is permitted.", true)] string sql) =>
            {
                try
                {
                    // Safety: only SELECT allowed
                    var trimmed = sql.TrimStart();
                    if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                        return JsonSerializer.Serialize(new { error = "Only SELECT statements are allowed." });

                    var upper = sql.ToUpperInvariant();
                    foreach (var banned in new[] { "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE", "EXEC" })
                    {
                        if (upper.Contains(banned))
                            return JsonSerializer.Serialize(new { error = $"Keyword '{banned}' is not allowed." });
                    }

                    var conn = _db.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.CommandTimeout = 5;

                    using var reader = cmd.ExecuteReader();

                    var columns = Enumerable.Range(0, reader.FieldCount)
                        .Select(i => reader.GetName(i))
                        .ToList();

                    var rows = new List<Dictionary<string, object?>>();
                    while (reader.Read() && rows.Count < 100)
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                            row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                    }

                    return JsonSerializer.Serialize(new
                    {
                        rowCount = rows.Count,
                        columns,
                        rows
                    }, _jsonOpts);
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = ex.Message });
                }
            }
        ),

        // ── 4. Compare two submissions ─────────────────────────────────────
        Tool.FromFunc(
            "compare_submissions",
            ([FunctionParameter("ID of the first submission", true)]  int submissionId1,
             [FunctionParameter("ID of the second submission", true)] int submissionId2) =>
            {
                try
                {
                    var ids = new[] { submissionId1, submissionId2 };
                    var subs = _db.Submissions
                        .Include(x => x.Insured)
                        .Include(x => x.Broker)
                        .Include(x => x.CoverageLines)
                        .Include(x => x.Exposures)
                        .Include(x => x.Losses)
                        .Where(x => ids.Contains(x.SubmissionId))
                        .ToList();

                    object Summary(Domain.Entities.Submission? s) => s == null
                        ? new { error = "not found" }
                        : (object)new
                        {
                            s.SubmissionId,
                            s.Status,
                            s.ExtractionConfidence,
                            insured     = s.Insured?.CompanyName,
                            state       = s.Insured?.State,
                            industry    = s.Insured?.Industry,
                            yearsInBiz  = s.Insured?.YearsInBusiness,
                            annualRev   = s.Insured?.AnnualRevenue,
                            broker      = s.Broker?.BrokerName,
                            coverages   = s.CoverageLines.Select(c => new { c.LineOfBusiness, c.RequestedLimit, c.TargetPremium }),
                            totalLoss   = s.Losses.Sum(l => l.LossAmount),
                            openClaims  = s.Losses.Count(l => !l.IsClosed),
                            exposures   = s.Exposures.Select(e => new { e.ExposureType, e.Quantity })
                        };

                    return JsonSerializer.Serialize(new
                    {
                        submission1 = Summary(subs.FirstOrDefault(x => x.SubmissionId == submissionId1)),
                        submission2 = Summary(subs.FirstOrDefault(x => x.SubmissionId == submissionId2))
                    }, _jsonOpts);
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = ex.Message });
                }
            }
        ),

        // ── 5. Aggregate analytics ─────────────────────────────────────────
        Tool.FromFunc(
            "get_analytics",
            () =>
            {
                try
                {
                    var total   = _db.Submissions.Count();
                    var byStatus = _db.Submissions
                        .GroupBy(s => s.Status)
                        .Select(g => new { status = g.Key.ToString(), count = g.Count() })
                        .ToList();

                    var avgConf  = _db.Submissions.Average(s => (double?)s.ExtractionConfidence) ?? 0;
                    var totalLoss = _db.LossHistory.Sum(l => (decimal?)l.LossAmount) ?? 0m;
                    var openClaims = _db.LossHistory.Count(l => !l.IsClosed);

                    var byLob = _db.CoverageLines
                        .GroupBy(c => c.LineOfBusiness)
                        .Select(g => new { lob = g.Key.ToString(), count = g.Count() })
                        .OrderByDescending(x => x.count)
                        .ToList();

                    var topInsureds = _db.Insureds
                        .Include(i => i.Submissions)
                        .OrderByDescending(i => i.Submissions.Count)
                        .Take(5)
                        .Select(i => new { i.CompanyName, submissionCount = i.Submissions.Count })
                        .ToList();

                    var topBrokers = _db.Brokers
                        .Include(b => b.Submissions)
                        .OrderByDescending(b => b.Submissions.Count)
                        .Take(5)
                        .Select(b => new { b.BrokerName, b.AgencyName, submissionCount = b.Submissions.Count })
                        .ToList();

                    return JsonSerializer.Serialize(new
                    {
                        totalSubmissions = total,
                        byStatus,
                        averageConfidence = Math.Round(avgConf, 3),
                        totalLossAmount = totalLoss,
                        openClaims,
                        byLineOfBusiness = byLob,
                        topInsureds,
                        topBrokers
                    }, _jsonOpts);
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = ex.Message });
                }
            }
        )
    };
}
