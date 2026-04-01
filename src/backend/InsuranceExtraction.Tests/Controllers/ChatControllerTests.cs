using InsuranceExtraction.API.Controllers;
using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InsuranceExtraction.Tests.Controllers;

public class ChatControllerTests
{
    private readonly Mock<IChatService> _chatService;
    private readonly ChatController _sut;

    public ChatControllerTests()
    {
        _chatService = new Mock<IChatService>();
        _sut = new ChatController(_chatService.Object, NullLogger<ChatController>.Instance);
    }

    // ─── Input validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Chat_NullMessages_ReturnsBadRequest()
    {
        var request = new ChatRequest { Messages = null! };

        var result = await _sut.Chat(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Chat_EmptyMessagesList_ReturnsBadRequest()
    {
        var request = new ChatRequest { Messages = new List<ChatMessage>() };

        var result = await _sut.Chat(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Chat_LastMessageNotUser_ReturnsBadRequest()
    {
        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", Content = "Hi there" }
            }
        };

        var result = await _sut.Chat(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Chat_ValidRequest_ReturnsOkWithAnswer()
    {
        var expected = new ChatResponse
        {
            Answer = "You have 5 submissions.",
            ToolCalls = new List<ToolCallRecord>
            {
                new() { ToolName = "list_submissions", Input = "{}", Output = "[]" }
            }
        };

        _chatService.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync(expected);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "How many submissions?" } }
        };

        var result = await _sut.Chat(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expected, ok.Value);
    }

    [Fact]
    public async Task Chat_ValidRequest_CallsChatServiceOnce()
    {
        _chatService.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync(new ChatResponse { Answer = "OK" });

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        await _sut.Chat(request);

        _chatService.Verify(x => x.ChatAsync(It.IsAny<List<ChatMessage>>()), Times.Once);
    }

    // ─── Error handling ──────────────────────────────────────────────────────

    [Fact]
    public async Task Chat_ServiceThrows_Returns500()
    {
        _chatService.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>()))
            .ThrowsAsync(new Exception("Anthropic API unreachable"));

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        var result = await _sut.Chat(request);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
    }

    [Fact]
    public async Task Chat_ConversationHistory_PassedToServiceUnmodified()
    {
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "First question" },
            new() { Role = "assistant", Content = "First answer" },
            new() { Role = "user", Content = "Follow-up question" }
        };

        List<ChatMessage>? captured = null;
        _chatService.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>()))
            .Callback<List<ChatMessage>>(msgs => captured = msgs)
            .ReturnsAsync(new ChatResponse { Answer = "OK" });

        await _sut.Chat(new ChatRequest { Messages = history });

        Assert.NotNull(captured);
        Assert.Equal(3, captured!.Count);
        Assert.Equal("Follow-up question", captured.Last().Content);
    }
}
