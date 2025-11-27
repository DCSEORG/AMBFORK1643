using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly IChatService _chatService;

    public bool IsAiConfigured { get; private set; }

    public ChatModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public void OnGet()
    {
        IsAiConfigured = _chatService.IsConfigured;
    }
}
