namespace InsuranceExtraction.Domain.Entities;

public class Insured
{
    public int InsuredId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Industry { get; set; }
    public int? YearsInBusiness { get; set; }
    public string? DotNumber { get; set; }
    public string? McNumber { get; set; }
    public decimal? AnnualRevenue { get; set; }

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
