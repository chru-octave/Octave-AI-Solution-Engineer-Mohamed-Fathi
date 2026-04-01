using InsuranceExtraction.Application.Interfaces;
using InsuranceExtraction.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceExtraction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message (with full conversation history) and get an AI response.
    /// Claude will automatically call tools to query submission data as needed.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (request.Messages == null || request.Messages.Count == 0)
            return BadRequest("Messages array is required and must not be empty.");

        var lastMessage = request.Messages.Last();
        if (lastMessage.Role != "user")
            return BadRequest("The last message must have role 'user'.");

        try
        {
            _logger.LogInformation(
                "Chat request: {MessageCount} messages, last: \"{Last}\"",
                request.Messages.Count,
                lastMessage.Content.Length > 80
                    ? lastMessage.Content[..80] + "…"
                    : lastMessage.Content);

            var response = await _chatService.ChatAsync(request.Messages);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
