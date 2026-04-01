using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using InsuranceExtraction.Application.Services;
using InsuranceExtraction.Domain.Enums;
using InsuranceExtraction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InsuranceExtraction.Tests.Services;

public class SubmissionProcessingServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailParsingService> _emailParser;
    private readonly Mock<IDocumentParsingService> _documentParser;
    private readonly Mock<IClaudeExtractionService> _claudeExtractor;
    private readonly SubmissionProcessingService _sut;

    public SubmissionProcessingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _emailParser = new Mock<IEmailParsingService>();
        _documentParser = new Mock<IDocumentParsingService>();
        _claudeExtractor = new Mock<IClaudeExtractionService>();

        _sut = new SubmissionProcessingService(
            _emailParser.Object,
            _documentParser.Object,
            _claudeExtractor.Object,
            _db,
            NullLogger<SubmissionProcessingService>.Instance);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private void SetupHappyPath(double confidence = 0.9, string companyName = "Acme Trucking", string brokerName = "John Smith")
    {
        _emailParser.Setup(x => x.ParseEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailContent
            {
                FilePath = "test.eml",
                Subject = "New Submission",
                From = "broker@agency.com",
                PlainTextBody = "Please quote the attached."
            });

        _claudeExtractor.Setup(x => x.ExtractDataAsync(It.IsAny<EmailContent>()))
            .ReturnsAsync(new SubmissionData
            {
                Confidence = confidence,
                Insured = new InsuredData { CompanyName = companyName, State = "TX" },
                Broker = new BrokerData { BrokerName = brokerName, AgencyName = "Smith Agency" },
                CoverageLines = new List<CoverageLineData>
                {
                    new() { LineOfBusiness = "AutoLiability", RequestedLimit = "1000000", TargetPremium = 5000 }
                },
                Exposures = new List<ExposureData>
                {
                    new() { ExposureType = "Trucks", Quantity = 10 }
                },
                Losses = new List<LossData>
                {
                    new() { LossAmount = 25000, LossType = "Collision", IsClosed = true }
                }
            });
    }

    // ─── Status tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessEmailAsync_HighConfidence_SetsProcessedStatus()
    {
        SetupHappyPath(confidence: 0.9);

        var id = await _sut.ProcessEmailAsync("test.eml");

        var submission = await _db.Submissions.FindAsync(id);
        Assert.Equal(SubmissionStatus.Processed, submission!.Status);
    }

    [Fact]
    public async Task ProcessEmailAsync_LowConfidence_SetsNeedsReviewStatus()
    {
        SetupHappyPath(confidence: 0.5);

        var id = await _sut.ProcessEmailAsync("test.eml");

        var submission = await _db.Submissions.FindAsync(id);
        Assert.Equal(SubmissionStatus.NeedsReview, submission!.Status);
    }

    [Fact]
    public async Task ProcessEmailAsync_ExtractionThrows_SetsFailedStatus()
    {
        _emailParser.Setup(x => x.ParseEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailContent { FilePath = "bad.eml" });

        _claudeExtractor.Setup(x => x.ExtractDataAsync(It.IsAny<EmailContent>()))
            .ThrowsAsync(new Exception("Claude API timeout"));

        var id = await _sut.ProcessEmailAsync("bad.eml");

        var submission = await _db.Submissions.FindAsync(id);
        Assert.Equal(SubmissionStatus.Failed, submission!.Status);
    }

    [Fact]
    public async Task ProcessEmailAsync_ExtractionConfidenceStoredCorrectly()
    {
        SetupHappyPath(confidence: 0.85);

        var id = await _sut.ProcessEmailAsync("test.eml");

        var submission = await _db.Submissions.FindAsync(id);
        Assert.Equal(0.85, submission!.ExtractionConfidence);
    }

    // ─── Insured / Broker upsert tests ────────────────────────────────────────

    [Fact]
    public async Task ProcessEmailAsync_NewInsured_CreatesInsuredRecord()
    {
        SetupHappyPath(companyName: "Blue Freight LLC");

        await _sut.ProcessEmailAsync("test.eml");

        var insured = await _db.Insureds.FirstOrDefaultAsync(i => i.CompanyName == "Blue Freight LLC");
        Assert.NotNull(insured);
    }

    [Fact]
    public async Task ProcessEmailAsync_SameInsuredTwice_OnlyOneInsuredRecord()
    {
        SetupHappyPath(companyName: "Blue Freight LLC");

        await _sut.ProcessEmailAsync("test1.eml");
        await _sut.ProcessEmailAsync("test2.eml");

        var count = await _db.Insureds.CountAsync(i => i.CompanyName == "Blue Freight LLC");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProcessEmailAsync_NewBroker_CreatesBrokerRecord()
    {
        SetupHappyPath(brokerName: "Jane Doe");

        await _sut.ProcessEmailAsync("test.eml");

        var broker = await _db.Brokers.FirstOrDefaultAsync(b => b.BrokerName == "Jane Doe");
        Assert.NotNull(broker);
    }

    [Fact]
    public async Task ProcessEmailAsync_SameBrokerTwice_OnlyOneBrokerRecord()
    {
        SetupHappyPath(brokerName: "Jane Doe");

        await _sut.ProcessEmailAsync("test1.eml");
        await _sut.ProcessEmailAsync("test2.eml");

        var count = await _db.Brokers.CountAsync(b => b.BrokerName == "Jane Doe");
        Assert.Equal(1, count);
    }

    // ─── Related records tests ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessEmailAsync_StoresCoverageLines()
    {
        SetupHappyPath();

        var id = await _sut.ProcessEmailAsync("test.eml");

        var lines = await _db.CoverageLines.Where(c => c.SubmissionId == id).ToListAsync();
        Assert.Single(lines);
        Assert.Equal(LineOfBusiness.AutoLiability, lines[0].LineOfBusiness);
    }

    [Fact]
    public async Task ProcessEmailAsync_StoresExposures()
    {
        SetupHappyPath();

        var id = await _sut.ProcessEmailAsync("test.eml");

        var exposures = await _db.Exposures.Where(e => e.SubmissionId == id).ToListAsync();
        Assert.Single(exposures);
        Assert.Equal(ExposureType.Trucks, exposures[0].ExposureType);
    }

    [Fact]
    public async Task ProcessEmailAsync_StoresLossHistory()
    {
        SetupHappyPath();

        var id = await _sut.ProcessEmailAsync("test.eml");

        var losses = await _db.LossHistory.Where(l => l.SubmissionId == id).ToListAsync();
        Assert.Single(losses);
        Assert.Equal(25000, losses[0].LossAmount);
        Assert.True(losses[0].IsClosed);
    }

    [Fact]
    public async Task ProcessEmailAsync_EmailSubjectStoredOnSubmission()
    {
        SetupHappyPath();

        var id = await _sut.ProcessEmailAsync("test.eml");

        var submission = await _db.Submissions.FindAsync(id);
        Assert.Equal("New Submission", submission!.EmailSubject);
    }

    public void Dispose() => _db.Dispose();
}
