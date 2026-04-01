using InsuranceExtraction.API.Controllers;
using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InsuranceExtraction.Tests.Controllers;

public class UploadControllerTests : IDisposable
{
    private readonly Mock<ISubmissionProcessingService> _processingService;
    private readonly AppDbContext _db;
    private readonly Mock<IWebHostEnvironment> _env;
    private readonly UploadController _sut;

    public UploadControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _processingService = new Mock<ISubmissionProcessingService>();
        _env = new Mock<IWebHostEnvironment>();
        _env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        _sut = new UploadController(
            _processingService.Object,
            _db,
            NullLogger<UploadController>.Instance,
            _env.Object);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IFormFile MakeFormFile(string fileName, string content = "data")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(bytes.Length);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return file.Object;
    }

    // ─── Validation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_NullFiles_ReturnsBadRequest()
    {
        var result = await _sut.Upload(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_EmptyFileList_ReturnsBadRequest()
    {
        var result = await _sut.Upload(new List<IFormFile>());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_ReturnsErrorInResult()
    {
        // .xyz is not in the allowed extensions list
        var file = MakeFormFile("submission.xyz");

        var result = await _sut.Upload(new List<IFormFile> { file });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("error", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_DoesNotCallProcessingService()
    {
        var file = MakeFormFile("submission.xyz");

        await _sut.Upload(new List<IFormFile> { file });

        _processingService.Verify(
            x => x.ProcessEmailAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Upload_PdfFile_IsAllowedAndCallsProcessingService()
    {
        // PDFs are a valid upload type (direct ACORD forms, loss runs)
        _processingService.Setup(x => x.ProcessEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(5);

        var file = MakeFormFile("loss-run.pdf");

        var result = await _sut.Upload(new List<IFormFile> { file });

        _processingService.Verify(x => x.ProcessEmailAsync(It.IsAny<string>()), Times.Once);
        Assert.IsType<OkObjectResult>(result);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ValidEmlFile_CallsProcessingService()
    {
        _processingService.Setup(x => x.ProcessEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(42);

        var file = MakeFormFile("submission.eml");

        await _sut.Upload(new List<IFormFile> { file });

        _processingService.Verify(
            x => x.ProcessEmailAsync(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Upload_ValidEmlFile_ReturnsSuccessResult()
    {
        _processingService.Setup(x => x.ProcessEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(7);

        var file = MakeFormFile("submission.eml");

        var result = await _sut.Upload(new List<IFormFile> { file });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("submissionId", json, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Mixed file list ─────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_MixedFiles_SkipsUnsupportedAndProcessesAllowed()
    {
        _processingService.Setup(x => x.ProcessEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(1);

        var files = new List<IFormFile>
        {
            MakeFormFile("submission.eml"),   // allowed
            MakeFormFile("lossrun.pdf"),       // allowed
            MakeFormFile("bad.xyz"),           // unsupported — skipped
        };

        await _sut.Upload(files);

        // Only the 2 allowed files should reach the processing service
        _processingService.Verify(
            x => x.ProcessEmailAsync(It.IsAny<string>()),
            Times.Exactly(2));
    }

    public void Dispose() => _db.Dispose();
}
