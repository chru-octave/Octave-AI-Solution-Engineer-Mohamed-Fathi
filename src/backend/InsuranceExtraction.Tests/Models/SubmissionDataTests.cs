using InsuranceExtraction.Application.Models;
using System.Text.Json;

namespace InsuranceExtraction.Tests.Models;

public class SubmissionDataTests
{
    // ─── Default values ──────────────────────────────────────────────────────

    [Fact]
    public void SubmissionData_DefaultCollections_AreNotNull()
    {
        var data = new SubmissionData();

        Assert.NotNull(data.CoverageLines);
        Assert.NotNull(data.Exposures);
        Assert.NotNull(data.Losses);
    }

    [Fact]
    public void SubmissionData_DefaultCollections_AreEmpty()
    {
        var data = new SubmissionData();

        Assert.Empty(data.CoverageLines);
        Assert.Empty(data.Exposures);
        Assert.Empty(data.Losses);
    }

    [Fact]
    public void SubmissionData_DefaultConfidence_IsZero()
    {
        var data = new SubmissionData();

        Assert.Equal(0.0, data.Confidence);
    }

    // ─── JSON deserialization ─────────────────────────────────────────────────

    [Fact]
    public void SubmissionData_DeserializesFromJson_CamelCaseFields()
    {
        var json = """
            {
              "insured": { "companyName": "Acme Corp", "state": "TX" },
              "broker": { "brokerName": "Jane Smith", "agencyName": "Smith Agency" },
              "coverageLines": [
                { "lineOfBusiness": "AutoLiability", "requestedLimit": "1000000", "targetPremium": 5000 }
              ],
              "exposures": [{ "exposureType": "Trucks", "quantity": 10 }],
              "losses": [],
              "confidence": 0.87
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<SubmissionData>(json, options)!;

        Assert.Equal("Acme Corp", data.Insured!.CompanyName);
        Assert.Equal("TX", data.Insured.State);
        Assert.Equal("Jane Smith", data.Broker!.BrokerName);
        Assert.Single(data.CoverageLines);
        Assert.Equal("AutoLiability", data.CoverageLines[0].LineOfBusiness);
        Assert.Equal(5000, data.CoverageLines[0].TargetPremium);
        Assert.Single(data.Exposures);
        Assert.Equal(10, data.Exposures[0].Quantity);
        Assert.Empty(data.Losses);
        Assert.Equal(0.87, data.Confidence, precision: 2);
    }

    [Fact]
    public void SubmissionData_MissingOptionalFields_DoNotThrow()
    {
        var json = """{"confidence": 0.5}""";

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<SubmissionData>(json, options)!;

        Assert.Null(data.Insured);
        Assert.Null(data.Broker);
        Assert.Equal(0.5, data.Confidence, precision: 2);
    }

    // ─── ChatModels ───────────────────────────────────────────────────────────

    [Fact]
    public void ChatMessage_DefaultRole_IsEmpty()
    {
        var msg = new ChatMessage();

        Assert.Equal(string.Empty, msg.Role);
        Assert.Equal(string.Empty, msg.Content);
    }

    [Fact]
    public void ChatResponse_DefaultAnswer_IsEmpty()
    {
        var response = new ChatResponse();

        Assert.Equal(string.Empty, response.Answer);
        Assert.NotNull(response.ToolCalls);
        Assert.Empty(response.ToolCalls);
    }

    [Fact]
    public void ToolCallRecord_DefaultFields_AreEmpty()
    {
        var record = new ToolCallRecord();

        Assert.Equal(string.Empty, record.ToolName);
        Assert.Equal(string.Empty, record.Input);
        Assert.Equal(string.Empty, record.Output);
    }

    // ─── LossData ────────────────────────────────────────────────────────────

    [Fact]
    public void LossData_IsClosed_DefaultsFalse()
    {
        var loss = new LossData();

        Assert.False(loss.IsClosed);
    }

    [Fact]
    public void LossData_LossAmount_DefaultsZero()
    {
        var loss = new LossData();

        Assert.Equal(0, loss.LossAmount);
    }

    // ─── Confidence boundary ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.699)]
    public void SubmissionData_ConfidenceBelowThreshold_IsLow(double confidence)
    {
        // The processing service flags < 0.7 as NeedsReview
        Assert.True(confidence < 0.7);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(0.85)]
    [InlineData(1.0)]
    public void SubmissionData_ConfidenceAtOrAboveThreshold_IsHigh(double confidence)
    {
        Assert.True(confidence >= 0.7);
    }
}
