using InsuranceExtraction.Domain.Enums;

namespace InsuranceExtraction.Domain.Entities;

public class CoverageLine
{
    public int CoverageId { get; set; }
    public int SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public LineOfBusiness LineOfBusiness { get; set; }
    public string? RequestedLimit { get; set; }
    public decimal? TargetPremium { get; set; }
    public decimal? CurrentPremium { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Notes { get; set; }
}
