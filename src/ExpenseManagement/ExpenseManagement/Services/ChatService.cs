using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using System.Text.Json;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<string> GetChatResponseAsync(string userMessage);
    bool IsConfigured { get; }
}

public class ChatService : IChatService
{
    private readonly IExpenseService _expenseService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly AzureOpenAIClient? _openAIClient;
    private readonly string? _deploymentName;
    private readonly bool _isConfigured;

    public bool IsConfigured => _isConfigured;

    public ChatService(
        IExpenseService expenseService,
        IConfiguration configuration,
        ILogger<ChatService> logger)
    {
        _expenseService = expenseService;
        _configuration = configuration;
        _logger = logger;

        var endpoint = configuration["OpenAI:Endpoint"];
        _deploymentName = configuration["OpenAI:DeploymentName"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(_deploymentName))
        {
            try
            {
                var managedIdentityClientId = configuration["ManagedIdentityClientId"];
                Azure.Core.TokenCredential credential;

                if (!string.IsNullOrEmpty(managedIdentityClientId))
                {
                    _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                    credential = new ManagedIdentityCredential(managedIdentityClientId);
                }
                else
                {
                    _logger.LogInformation("Using DefaultAzureCredential");
                    credential = new DefaultAzureCredential();
                }

                _openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
                _isConfigured = true;
                _logger.LogInformation("Chat service initialized with endpoint: {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI client");
                _isConfigured = false;
            }
        }
        else
        {
            _logger.LogWarning("Azure OpenAI not configured. Chat will use dummy responses.");
            _isConfigured = false;
        }
    }

    public async Task<string> GetChatResponseAsync(string userMessage)
    {
        if (!_isConfigured || _openAIClient == null)
        {
            return GetDummyResponse(userMessage);
        }

        try
        {
            var chatClient = _openAIClient.GetChatClient(_deploymentName);
            
            var tools = GetFunctionTools();
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt()),
                new UserChatMessage(userMessage)
            };

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var response = await chatClient.CompleteChatAsync(messages, options);
            var assistantMessage = response.Value;

            // Handle function calls
            while (assistantMessage.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(assistantMessage));

                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                    messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                response = await chatClient.CompleteChatAsync(messages, options);
                assistantMessage = response.Value;
            }

            return assistantMessage.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            return $"I encountered an error processing your request: {ex.Message}. Please try again later.";
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful assistant for the Expense Management System. You can help users:
- View their expenses (use get_expenses function)
- Add new expenses (use create_expense function)
- Submit expenses for approval (use submit_expense function)
- View pending expenses awaiting approval (use get_pending_expenses function)
- Approve or reject expenses (use approve_expense function)
- View expense categories (use get_categories function)
- View expense statuses (use get_statuses function)
- View user information (use get_users function)

When listing expenses, format them nicely with:
- Date
- Category
- Amount (in GBP, e.g., £25.40)
- Status
- Description

When creating expenses, ask for:
- Amount in GBP
- Date
- Category (Travel, Meals, Supplies, Accommodation, Other)
- Description

Always be helpful and provide clear information about expense status and next steps.
Currency is in British Pounds (GBP).";
    }

    private List<ChatTool> GetFunctionTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_expenses",
                "Retrieves expenses from the database with optional filtering",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""searchTerm"": { ""type"": ""string"", ""description"": ""Search term to filter expenses"" },
                        ""categoryId"": { ""type"": ""integer"", ""description"": ""Filter by category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)"" },
                        ""statusId"": { ""type"": ""integer"", ""description"": ""Filter by status ID (1=Draft, 2=Submitted, 3=Approved, 4=Rejected)"" },
                        ""userId"": { ""type"": ""integer"", ""description"": ""Filter by user ID"" }
                    }
                }")),

            ChatTool.CreateFunctionTool(
                "get_pending_expenses",
                "Retrieves pending expenses awaiting manager approval",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""searchTerm"": { ""type"": ""string"", ""description"": ""Optional search term to filter pending expenses"" }
                    }
                }")),

            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense entry",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""amount"": { ""type"": ""number"", ""description"": ""Amount in GBP"" },
                        ""expenseDate"": { ""type"": ""string"", ""description"": ""Date of expense in YYYY-MM-DD format"" },
                        ""categoryId"": { ""type"": ""integer"", ""description"": ""Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)"" },
                        ""description"": { ""type"": ""string"", ""description"": ""Description of the expense"" },
                        ""userId"": { ""type"": ""integer"", ""description"": ""User ID (default 1 for Alice)"" }
                    },
                    ""required"": [""amount"", ""expenseDate"", ""categoryId""]
                }")),

            ChatTool.CreateFunctionTool(
                "submit_expense",
                "Submits an expense for manager approval",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""expenseId"": { ""type"": ""integer"", ""description"": ""The ID of the expense to submit"" }
                    },
                    ""required"": [""expenseId""]
                }")),

            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves or rejects an expense (manager only)",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""expenseId"": { ""type"": ""integer"", ""description"": ""The ID of the expense to approve/reject"" },
                        ""approve"": { ""type"": ""boolean"", ""description"": ""True to approve, false to reject"" },
                        ""reviewerId"": { ""type"": ""integer"", ""description"": ""Manager user ID (default 2 for Bob Manager)"" }
                    },
                    ""required"": [""expenseId"", ""approve""]
                }")),

            ChatTool.CreateFunctionTool(
                "get_categories",
                "Retrieves all expense categories",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {}
                }")),

            ChatTool.CreateFunctionTool(
                "get_statuses",
                "Retrieves all expense statuses",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {}
                }")),

            ChatTool.CreateFunctionTool(
                "get_users",
                "Retrieves all users in the system",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {}
                }"))
        };
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);

            return functionName switch
            {
                "get_expenses" => await GetExpensesFunction(args),
                "get_pending_expenses" => await GetPendingExpensesFunction(args),
                "create_expense" => await CreateExpenseFunction(args),
                "submit_expense" => await SubmitExpenseFunction(args),
                "approve_expense" => await ApproveExpenseFunction(args),
                "get_categories" => await GetCategoriesFunction(),
                "get_statuses" => await GetStatusesFunction(),
                "get_users" => await GetUsersFunction(),
                _ => JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> GetExpensesFunction(JsonElement args)
    {
        var filter = new ExpenseFilterRequest();
        
        if (args.TryGetProperty("searchTerm", out var searchTerm))
            filter.SearchTerm = searchTerm.GetString();
        if (args.TryGetProperty("categoryId", out var categoryId))
            filter.CategoryId = categoryId.GetInt32();
        if (args.TryGetProperty("statusId", out var statusId))
            filter.StatusId = statusId.GetInt32();
        if (args.TryGetProperty("userId", out var userId))
            filter.UserId = userId.GetInt32();

        var expenses = await _expenseService.GetExpensesAsync(filter);
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId,
            e.ExpenseDate,
            e.CategoryName,
            AmountGBP = $"£{e.AmountGBP:F2}",
            e.StatusName,
            e.Description,
            e.UserName
        }));
    }

    private async Task<string> GetPendingExpensesFunction(JsonElement args)
    {
        string? searchTerm = null;
        if (args.TryGetProperty("searchTerm", out var term))
            searchTerm = term.GetString();

        var expenses = await _expenseService.GetPendingExpensesForApprovalAsync(searchTerm);
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId,
            e.ExpenseDate,
            e.CategoryName,
            AmountGBP = $"£{e.AmountGBP:F2}",
            e.Description,
            e.UserName
        }));
    }

    private async Task<string> CreateExpenseFunction(JsonElement args)
    {
        var request = new CreateExpenseRequest
        {
            Amount = args.GetProperty("amount").GetDecimal(),
            ExpenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!),
            CategoryId = args.GetProperty("categoryId").GetInt32(),
            Description = args.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            UserId = args.TryGetProperty("userId", out var uid) ? uid.GetInt32() : 1
        };

        var expenseId = await _expenseService.CreateExpenseAsync(request);
        return JsonSerializer.Serialize(new { success = true, expenseId, message = $"Expense created with ID {expenseId}" });
    }

    private async Task<string> SubmitExpenseFunction(JsonElement args)
    {
        var expenseId = args.GetProperty("expenseId").GetInt32();
        var success = await _expenseService.SubmitExpenseAsync(expenseId);
        return JsonSerializer.Serialize(new { success, message = success ? "Expense submitted for approval" : "Failed to submit expense" });
    }

    private async Task<string> ApproveExpenseFunction(JsonElement args)
    {
        var request = new ApproveExpenseRequest
        {
            ExpenseId = args.GetProperty("expenseId").GetInt32(),
            Approve = args.GetProperty("approve").GetBoolean(),
            ReviewerId = args.TryGetProperty("reviewerId", out var rid) ? rid.GetInt32() : 2
        };

        var success = await _expenseService.ApproveExpenseAsync(request);
        var action = request.Approve ? "approved" : "rejected";
        return JsonSerializer.Serialize(new { success, message = success ? $"Expense {action}" : $"Failed to {action} expense" });
    }

    private async Task<string> GetCategoriesFunction()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return JsonSerializer.Serialize(categories);
    }

    private async Task<string> GetStatusesFunction()
    {
        var statuses = await _expenseService.GetStatusesAsync();
        return JsonSerializer.Serialize(statuses);
    }

    private async Task<string> GetUsersFunction()
    {
        var users = await _expenseService.GetUsersAsync();
        return JsonSerializer.Serialize(users.Select(u => new
        {
            u.UserId,
            u.UserName,
            u.Email,
            u.RoleName
        }));
    }

    private static string GetDummyResponse(string userMessage)
    {
        var lowerMessage = userMessage.ToLower();

        if (lowerMessage.Contains("expense") && (lowerMessage.Contains("list") || lowerMessage.Contains("show") || lowerMessage.Contains("view")))
        {
            return @"**Note: GenAI services are not deployed. Using dummy response.**

To enable AI-powered chat, deploy with `deploy-with-chat.sh` instead of `deploy.sh`.

Here's a sample list of expenses:

1. **15/01/2024** - Travel - £120.00 - Submitted
   Taxi from airport to client site

2. **10/01/2023** - Meals - £69.00 - Submitted  
   Client lunch meeting

3. **04/12/2023** - Supplies - £99.50 - Approved
   Office stationery

4. **18/12/2023** - Travel - £19.20 - Approved
   Bus fare";
        }

        if (lowerMessage.Contains("add") || lowerMessage.Contains("create") || lowerMessage.Contains("new"))
        {
            return @"**Note: GenAI services are not deployed. Using dummy response.**

To add a new expense, use the **Add Expense** page. You'll need:
- Amount (in GBP)
- Date
- Category (Travel, Meals, Supplies, Accommodation, Other)
- Description

To enable AI-powered expense creation, deploy with `deploy-with-chat.sh`.";
        }

        if (lowerMessage.Contains("approve") || lowerMessage.Contains("pending"))
        {
            return @"**Note: GenAI services are not deployed. Using dummy response.**

To approve expenses, use the **Approve Expenses** page as a manager.

Sample pending expenses:
1. **20/01/2024** - Travel - £120.00 - Alice Example
2. **14/12/2023** - Supplies - £99.50 - Alice Example

To enable AI-powered approvals, deploy with `deploy-with-chat.sh`.";
        }

        return @"**Note: GenAI services are not deployed. Using dummy response.**

I'm the Expense Management Assistant. I can help you:
- View your expenses
- Add new expenses  
- Submit expenses for approval
- Approve/reject expenses (managers)

To enable full AI capabilities, deploy with `deploy-with-chat.sh` instead of `deploy.sh`.

What would you like help with?";
    }
}
