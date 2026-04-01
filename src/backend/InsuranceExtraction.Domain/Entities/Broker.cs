namespace InsuranceExtraction.Domain.Entities;

public class Broker
{
    public int BrokerId { get; set; }
    public string BrokerName { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? LicenseNumber { get; set; }

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
