using System.Text.Json.Serialization;

namespace InsuranceExtraction.Application.Models;

public class SubmissionData
{
    [JsonPropertyName("insured")]
    public InsuredData? Insured { get; set; }

    [JsonPropertyName("broker")]
    public BrokerData? Broker { get; set; }

    [JsonPropertyName("coverageLines")]
    public List<CoverageLineData> CoverageLines { get; set; } = new();

    [JsonPropertyName("exposures")]
    public List<ExposureData> Exposures { get; set; } = new();

    [JsonPropertyName("losses")]
    public List<LossData> Losses { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public class InsuredData
{
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }

    [JsonPropertyName("industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("yearsInBusiness")]
    public int? YearsInBusiness { get; set; }

    [JsonPropertyName("dotNumber")]
    public string? DotNumber { get; set; }

    [JsonPropertyName("mcNumber")]
    public string? McNumber { get; set; }

    [JsonPropertyName("annualRevenue")]
    public decimal? AnnualRevenue { get; set; }
}

public class BrokerData
{
    [JsonPropertyName("brokerName")]
    public string? BrokerName { get; set; }

    [JsonPropertyName("agencyName")]
    public string? AgencyName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }

    [JsonPropertyName("licenseNumber")]
    public string? LicenseNumber { get; set; }
}

public class CoverageLineData
{
    [JsonPropertyName("lineOfBusiness")]
    public string? LineOfBusiness { get; set; }

    [JsonPropertyName("requestedLimit")]
    public string? RequestedLimit { get; set; }

    [JsonPropertyName("targetPremium")]
    public decimal? TargetPremium { get; set; }

    [JsonPropertyName("currentPremium")]
    public decimal? CurrentPremium { get; set; }

    [JsonPropertyName("effectiveDate")]
    public string? EffectiveDate { get; set; }

    [JsonPropertyName("expirationDate")]
    public string? ExpirationDate { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class ExposureData
{
    [JsonPropertyName("exposureType")]
    public string? ExposureType { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class LossData
{
    [JsonPropertyName("lossDate")]
    public string? LossDate { get; set; }

    [JsonPropertyName("lossAmount")]
    public decimal LossAmount { get; set; }

    [JsonPropertyName("lossType")]
    public string? LossType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("claimNumber")]
    public string? ClaimNumber { get; set; }

    [JsonPropertyName("policyYear")]
    public string? PolicyYear { get; set; }

    [JsonPropertyName("paidAmount")]
    public decimal? PaidAmount { get; set; }

    [JsonPropertyName("reserveAmount")]
    public decimal? ReserveAmount { get; set; }

    [JsonPropertyName("isClosed")]
    public bool IsClosed { get; set; }
}
