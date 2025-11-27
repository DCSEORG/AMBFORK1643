using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
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
    /// Get the configuration status of the chat service
    /// </summary>
    /// <returns>Whether the chat service is configured with Azure OpenAI</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            isConfigured = _chatService.IsConfigured,
            message = _chatService.IsConfigured
                ? "Chat service is configured with Azure OpenAI"
                : "Chat service is using dummy responses. Deploy with deploy-with-chat.sh to enable AI features."
        });
    }

    /// <summary>
    /// Send a chat message and get a response
    /// </summary>
    /// <param name="request">The chat request containing the user's message</param>
    /// <returns>The assistant's response</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message cannot be empty" });
        }

        try
        {
            _logger.LogInformation("Received chat message: {Message}", request.Message);
            var response = await _chatService.GetChatResponseAsync(request.Message);
            
            return Ok(new ChatResponse
            {
                Message = response,
                IsAiPowered = _chatService.IsConfigured
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new { error = "An error occurred while processing your message" });
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool IsAiPowered { get; set; }
}
