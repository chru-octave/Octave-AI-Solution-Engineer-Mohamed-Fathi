using InsuranceExtraction.Domain.Enums;
using InsuranceExtraction.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceExtraction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        var total = await _db.Submissions.CountAsync();
        var thisWeek = await _db.Submissions.CountAsync(s => s.SubmissionDate >= weekAgo);
        var needsReview = await _db.Submissions.CountAsync(s => s.Status == SubmissionStatus.NeedsReview);
        var failed = await _db.Submissions.CountAsync(s => s.Status == SubmissionStatus.Failed);
        var processed = await _db.Submissions.CountAsync(s => s.Status == SubmissionStatus.Processed);

        var avgConfidence = total > 0
            ? await _db.Submissions.AverageAsync(s => s.ExtractionConfidence)
            : 0.0;

        // Line of business breakdown
        var lobBreakdown = await _db.CoverageLines
            .GroupBy(c => c.LineOfBusiness)
            .Select(g => new { lob = g.Key.ToString(), count = g.Count() })
            .ToListAsync();

        // Recent submissions (last 10)
        var recent = await _db.Submissions
            .Include(s => s.Insured)
            .Include(s => s.Broker)
            .OrderByDescending(s => s.SubmissionDate)
            .Take(10)
            .Select(s => new
            {
                s.SubmissionId,
                s.EmailSubject,
                s.SubmissionDate,
                s.Status,
                s.ExtractionConfidence,
                insuredName = s.Insured != null ? s.Insured.CompanyName : null,
                brokerName = s.Broker != null ? s.Broker.BrokerName : null
            })
            .ToListAsync();

        // Total premium by LOB
        var premiumByLob = await _db.CoverageLines
            .Where(c => c.TargetPremium.HasValue)
            .GroupBy(c => c.LineOfBusiness)
            .Select(g => new
            {
                lob = g.Key.ToString(),
                totalPremium = g.Sum(c => c.TargetPremium ?? 0)
            })
            .ToListAsync();

        // Submissions by month (last 6 months)
        var sixMonthsAgo = now.AddMonths(-6);
        var byMonth = await _db.Submissions
            .Where(s => s.SubmissionDate >= sixMonthsAgo)
            .GroupBy(s => new { s.SubmissionDate.Year, s.SubmissionDate.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                count = g.Count()
            })
            .OrderBy(x => x.year).ThenBy(x => x.month)
            .ToListAsync();

        return Ok(new
        {
            total,
            thisWeek,
            needsReview,
            failed,
            processed,
            avgConfidence,
            lobBreakdown,
            premiumByLob,
            byMonth,
            recent
        });
    }
}
