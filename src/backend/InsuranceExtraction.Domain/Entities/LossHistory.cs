namespace InsuranceExtraction.Domain.Entities;

public class LossHistory
{
    public int LossId { get; set; }
    public int SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public DateTime? LossDate { get; set; }
    public decimal LossAmount { get; set; }
    public string? LossType { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? ClaimNumber { get; set; }
    public string? PolicyYear { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? ReserveAmount { get; set; }
    public string? Claimant { get; set; }
    public bool IsClosed { get; set; }
}
