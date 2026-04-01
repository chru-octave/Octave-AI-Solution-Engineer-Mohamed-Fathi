using InsuranceExtraction.Domain.Enums;
using InsuranceExtraction.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceExtraction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubmissionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.Submissions
            .Include(s => s.Insured)
            .Include(s => s.Broker)
            .OrderByDescending(s => s.SubmissionDate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.SubmissionId,
                s.EmailSubject,
                s.EmailFrom,
                s.SubmissionDate,
                s.ProcessedDate,
                s.Status,
                s.ExtractionConfidence,
                s.FailureReason,
                insuredName = s.Insured != null ? s.Insured.CompanyName : null,
                brokerName = s.Broker != null ? s.Broker.BrokerName : null,
                agencyName = s.Broker != null ? s.Broker.AgencyName : null,
                s.AttachmentList
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var submission = await _db.Submissions
            .Include(s => s.Insured)
            .Include(s => s.Broker)
            .Include(s => s.CoverageLines)
            .Include(s => s.Exposures)
            .Include(s => s.Losses)
            .FirstOrDefaultAsync(s => s.SubmissionId == id);

        if (submission == null) return NotFound();
        return Ok(submission);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        var query = _db.Submissions
            .Include(s => s.Insured)
            .Include(s => s.Broker)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var q = request.Query.ToLower();
            query = query.Where(s =>
                (s.Insured != null && s.Insured.CompanyName.ToLower().Contains(q)) ||
                (s.Broker != null && s.Broker.BrokerName.ToLower().Contains(q)) ||
                (s.Broker != null && s.Broker.AgencyName.ToLower().Contains(q)) ||
                (s.EmailSubject != null && s.EmailSubject.ToLower().Contains(q)));
        }

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        if (request.DateFrom.HasValue)
            query = query.Where(s => s.SubmissionDate >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(s => s.SubmissionDate <= request.DateTo.Value);

        var total = await query.CountAsync();
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;

        var items = await query
            .OrderByDescending(s => s.SubmissionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.SubmissionId,
                s.EmailSubject,
                s.EmailFrom,
                s.SubmissionDate,
                s.ProcessedDate,
                s.Status,
                s.ExtractionConfidence,
                s.FailureReason,
                insuredName = s.Insured != null ? s.Insured.CompanyName : null,
                brokerName = s.Broker != null ? s.Broker.BrokerName : null,
                agencyName = s.Broker != null ? s.Broker.AgencyName : null,
                s.AttachmentList
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var submission = await _db.Submissions.FindAsync(id);
        if (submission == null) return NotFound();

        _db.Submissions.Remove(submission);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class SearchRequest
{
    public string? Query { get; set; }
    public SubmissionStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
