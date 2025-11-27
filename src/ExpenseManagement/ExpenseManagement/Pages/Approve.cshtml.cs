using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApproveModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ApproveModel> _logger;

    public List<Expense> PendingExpenses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public ApproveModel(IExpenseService expenseService, ILogger<ApproveModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            PendingExpenses = await _expenseService.GetPendingExpensesForApprovalAsync(Filter);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error loading pending expenses");
            ErrorMessage = ex.GetDetailedMessage();
            LoadDummyPendingExpenses();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending expenses");
            ErrorMessage = $"Error loading pending expenses: {ex.Message}";
            LoadDummyPendingExpenses();
        }
    }

    public async Task<IActionResult> OnPostAsync(int expenseId, string action)
    {
        try
        {
            var approve = action == "approve";
            var request = new ApproveExpenseRequest
            {
                ExpenseId = expenseId,
                Approve = approve,
                ReviewerId = 2 // Default to Bob Manager
            };

            var success = await _expenseService.ApproveExpenseAsync(request);
            
            if (success)
            {
                SuccessMessage = approve 
                    ? $"Expense #{expenseId} has been approved." 
                    : $"Expense #{expenseId} has been rejected.";
            }
            else
            {
                ErrorMessage = $"Failed to process expense #{expenseId}.";
            }
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error processing expense approval");
            ErrorMessage = ex.GetDetailedMessage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expense approval");
            ErrorMessage = $"Error processing approval: {ex.Message}";
        }

        await OnGetAsync();
        return Page();
    }

    private void LoadDummyPendingExpenses()
    {
        PendingExpenses = new List<Expense>
        {
            new() { ExpenseId = 1, ExpenseDate = new DateTime(2024, 1, 20), CategoryName = "Travel", AmountMinor = 12000, StatusName = "Submitted", Description = "Taxi from airport", UserName = "Alice Example" },
            new() { ExpenseId = 2, ExpenseDate = new DateTime(2023, 12, 14), CategoryName = "Supplies", AmountMinor = 9950, StatusName = "Submitted", Description = "Office supplies", UserName = "Alice Example" }
        };

        if (!string.IsNullOrEmpty(Filter))
        {
            PendingExpenses = PendingExpenses.Where(e => 
                (e.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.CategoryName?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.UserName?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }
    }
}
