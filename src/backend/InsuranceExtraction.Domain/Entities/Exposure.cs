using InsuranceExtraction.Domain.Enums;

namespace InsuranceExtraction.Domain.Entities;

public class Exposure
{
    public int ExposureId { get; set; }
    public int SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public ExposureType ExposureType { get; set; }
    public decimal Quantity { get; set; }
    public string? Description { get; set; }
}
