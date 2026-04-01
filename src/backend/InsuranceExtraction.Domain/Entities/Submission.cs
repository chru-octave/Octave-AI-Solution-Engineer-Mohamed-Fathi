using InsuranceExtraction.Domain.Enums;

namespace InsuranceExtraction.Domain.Entities;

public class Submission
{
    public int SubmissionId { get; set; }
    public string EmailFilePath { get; set; } = string.Empty;
    public string? EmailSubject { get; set; }
    public string? EmailFrom { get; set; }
    public string? AttachmentList { get; set; }
    public DateTime SubmissionDate { get; set; }
    public DateTime ProcessedDate { get; set; }
    public SubmissionStatus Status { get; set; }
    public double ExtractionConfidence { get; set; }

    /// <summary>Populated when Status == Failed. Human-readable explanation of what went wrong.</summary>
    public string? FailureReason { get; set; }

    public int? InsuredId { get; set; }
    public Insured? Insured { get; set; }

    public int? BrokerId { get; set; }
    public Broker? Broker { get; set; }

    public ICollection<CoverageLine> CoverageLines { get; set; } = new List<CoverageLine>();
    public ICollection<Exposure> Exposures { get; set; } = new List<Exposure>();
    public ICollection<LossHistory> Losses { get; set; } = new List<LossHistory>();
}
