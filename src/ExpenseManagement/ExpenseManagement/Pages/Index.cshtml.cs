using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? CategoryFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? StatusFilter { get; set; }

    public IndexModel(IExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Categories = await _expenseService.GetCategoriesAsync();
            Statuses = await _expenseService.GetStatusesAsync();
            
            var filter = new ExpenseFilterRequest
            {
                SearchTerm = Filter,
                CategoryId = CategoryFilter,
                StatusId = StatusFilter
            };
            
            Expenses = await _expenseService.GetExpensesAsync(filter);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error loading expenses");
            ErrorMessage = ex.GetDetailedMessage();
            await LoadDummyDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses");
            ErrorMessage = $"Error loading data: {ex.Message}";
            await LoadDummyDataAsync();
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(decimal amount, DateTime expenseDate, int categoryId, string? description)
    {
        try
        {
            var request = new CreateExpenseRequest
            {
                Amount = amount,
                ExpenseDate = expenseDate,
                CategoryId = categoryId,
                Description = description,
                UserId = 1 // Default to Alice
            };
            
            await _expenseService.CreateExpenseAsync(request);
            return RedirectToPage();
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error creating expense");
            ErrorMessage = ex.GetDetailedMessage();
            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            ErrorMessage = $"Error creating expense: {ex.Message}";
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(expenseId);
            return RedirectToPage();
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error submitting expense");
            ErrorMessage = ex.GetDetailedMessage();
            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense");
            ErrorMessage = $"Error submitting expense: {ex.Message}";
            await OnGetAsync();
            return Page();
        }
    }

    private async Task LoadDummyDataAsync()
    {
        // Load dummy data for display when DB is unavailable
        Categories = new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel" },
            new() { CategoryId = 2, CategoryName = "Meals" },
            new() { CategoryId = 3, CategoryName = "Supplies" },
            new() { CategoryId = 4, CategoryName = "Accommodation" },
            new() { CategoryId = 5, CategoryName = "Other" }
        };
        
        Statuses = new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
        
        Expenses = new List<Expense>
        {
            new() { ExpenseId = 1, ExpenseDate = new DateTime(2024, 1, 15), CategoryName = "Travel", AmountMinor = 12000, StatusName = "Submitted", Description = "Taxi from airport" },
            new() { ExpenseId = 2, ExpenseDate = new DateTime(2023, 1, 10), CategoryName = "Meals", AmountMinor = 6900, StatusName = "Submitted", Description = "Client lunch" },
            new() { ExpenseId = 3, ExpenseDate = new DateTime(2023, 12, 4), CategoryName = "Supplies", AmountMinor = 9950, StatusName = "Approved", Description = "Office stationery" },
            new() { ExpenseId = 4, ExpenseDate = new DateTime(2023, 12, 18), CategoryName = "Travel", AmountMinor = 1920, StatusName = "Approved", Description = "Bus fare" }
        };
        
        await Task.CompletedTask;
    }
}
