using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses with optional filtering
    /// </summary>
    /// <param name="searchTerm">Search term to filter by description or category</param>
    /// <param name="categoryId">Filter by category ID</param>
    /// <param name="statusId">Filter by status ID</param>
    /// <param name="userId">Filter by user ID</param>
    /// <returns>List of expenses</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Expense>>> GetExpenses(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] int? statusId = null,
        [FromQuery] int? userId = null)
    {
        try
        {
            var filter = new ExpenseFilterRequest
            {
                SearchTerm = searchTerm,
                CategoryId = categoryId,
                StatusId = statusId,
                UserId = userId
            };
            var expenses = await _expenseService.GetExpensesAsync(filter);
            return Ok(expenses);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error getting expenses");
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            return StatusCode(500, new { error = "An error occurred while retrieving expenses" });
        }
    }

    /// <summary>
    /// Get a specific expense by ID
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <returns>The expense if found</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Expense>> GetExpense(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null)
            {
                return NotFound(new { error = $"Expense with ID {id} not found" });
            }
            return Ok(expense);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error getting expense {ExpenseId}", id);
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the expense" });
        }
    }

    /// <summary>
    /// Get pending expenses for manager approval
    /// </summary>
    /// <param name="searchTerm">Optional search term to filter</param>
    /// <returns>List of pending expenses</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Expense>>> GetPendingExpenses([FromQuery] string? searchTerm = null)
    {
        try
        {
            var expenses = await _expenseService.GetPendingExpensesForApprovalAsync(searchTerm);
            return Ok(expenses);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error getting pending expenses");
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending expenses");
            return StatusCode(500, new { error = "An error occurred while retrieving pending expenses" });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    /// <param name="request">Expense creation request</param>
    /// <returns>The ID of the created expense</returns>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Amount must be greater than 0" });
            }

            var expenseId = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetExpense), new { id = expenseId }, new { expenseId });
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error creating expense");
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "An error occurred while creating the expense" });
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="request">Update request</param>
    /// <returns>Success status</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        try
        {
            if (id != request.ExpenseId)
            {
                return BadRequest(new { error = "ID mismatch" });
            }

            var success = await _expenseService.UpdateExpenseAsync(request);
            if (!success)
            {
                return NotFound(new { error = $"Expense with ID {id} not found" });
            }

            return Ok(new { message = "Expense updated successfully" });
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error updating expense {ExpenseId}", id);
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", id);
            return StatusCode(500, new { error = "An error occurred while updating the expense" });
        }
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SubmitExpense(int id)
    {
        try
        {
            var success = await _expenseService.SubmitExpenseAsync(id);
            if (!success)
            {
                return NotFound(new { error = $"Expense with ID {id} not found" });
            }

            return Ok(new { message = "Expense submitted for approval" });
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error submitting expense {ExpenseId}", id);
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "An error occurred while submitting the expense" });
        }
    }

    /// <summary>
    /// Approve or reject an expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="request">Approval request</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ApproveExpense(int id, [FromBody] ApproveExpenseRequest request)
    {
        try
        {
            if (id != request.ExpenseId)
            {
                return BadRequest(new { error = "ID mismatch" });
            }

            var success = await _expenseService.ApproveExpenseAsync(request);
            if (!success)
            {
                return NotFound(new { error = $"Expense with ID {id} not found" });
            }

            var action = request.Approve ? "approved" : "rejected";
            return Ok(new { message = $"Expense {action} successfully" });
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error approving expense {ExpenseId}", id);
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", id);
            return StatusCode(500, new { error = "An error occurred while processing the approval" });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(IExpenseService expenseService, ILogger<CategoriesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    /// <returns>List of categories</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ExpenseCategory>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ExpenseCategory>>> GetCategories()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error getting categories");
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, new { error = "An error occurred while retrieving categories" });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<StatusesController> _logger;

    public StatusesController(IExpenseService expenseService, ILogger<StatusesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    /// <returns>List of statuses</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ExpenseStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ExpenseStatus>>> GetStatuses()
    {
        try
        {
            var statuses = await _expenseService.GetStatusesAsync();
            return Ok(statuses);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error getting statuses");
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            return StatusCode(500, new { error = "An error occurred while retrieving statuses" });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IExpenseService expenseService, ILogger<UsersController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    /// <returns>List of users</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<User>>> GetUsers()
    {
        try
        {
            var users = await _expenseService.GetUsersAsync();
            return Ok(users);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error getting users");
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "An error occurred while retrieving users" });
        }
    }

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>The user if found</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        try
        {
            var user = await _expenseService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { error = $"User with ID {id} not found" });
            }
            return Ok(user);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error getting user {UserId}", id);
            return StatusCode(500, new { error = ex.GetDetailedMessage() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the user" });
        }
    }
}
