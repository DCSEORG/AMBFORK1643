using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetExpensesAsync(ExpenseFilterRequest? filter = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<List<Expense>> GetPendingExpensesForApprovalAsync(string? searchTerm = null);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request);
    Task<bool> UpdateExpenseAsync(UpdateExpenseRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(ApproveExpenseRequest request);
    Task<List<ExpenseCategory>> GetCategoriesAsync();
    Task<List<ExpenseStatus>> GetStatusesAsync();
    Task<List<User>> GetUsersAsync();
    Task<User?> GetUserByIdAsync(int userId);
}

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;
    private readonly bool _useDummyData;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
        _useDummyData = string.IsNullOrEmpty(_connectionString);
    }

    public async Task<List<Expense>> GetExpensesAsync(ExpenseFilterRequest? filter = null)
    {
        if (_useDummyData) return GetDummyExpenses(filter);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@SearchTerm", filter?.SearchTerm ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CategoryId", filter?.CategoryId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", filter?.StatusId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@UserId", filter?.UserId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FromDate", filter?.FromDate ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ToDate", filter?.ToDate ?? (object)DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expenses from database");
            throw new DatabaseException("Failed to retrieve expenses. Check managed identity configuration.", ex, "ExpenseService.cs", 58);
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        if (_useDummyData) return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetExpenseById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expense {ExpenseId} from database", expenseId);
            throw new DatabaseException($"Failed to retrieve expense {expenseId}. Check managed identity configuration.", ex, "ExpenseService.cs", 82);
        }
    }

    public async Task<List<Expense>> GetPendingExpensesForApprovalAsync(string? searchTerm = null)
    {
        if (_useDummyData)
        {
            return GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetPendingExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@SearchTerm", searchTerm ?? (object)DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending expenses from database");
            throw new DatabaseException("Failed to retrieve pending expenses. Check managed identity configuration.", ex, "ExpenseService.cs", 110);
        }
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        if (_useDummyData) return new Random().Next(100, 999);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_CreateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Currency", request.Currency ?? "GBP");

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense in database");
            throw new DatabaseException("Failed to create expense. Check managed identity configuration.", ex, "ExpenseService.cs", 135);
        }
    }

    public async Task<bool> UpdateExpenseAsync(UpdateExpenseRequest request)
    {
        if (_useDummyData) return true;

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_UpdateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", request.StatusId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId} in database", request.ExpenseId);
            throw new DatabaseException($"Failed to update expense {request.ExpenseId}. Check managed identity configuration.", ex, "ExpenseService.cs", 162);
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        if (_useDummyData) return true;

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_SubmitExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", expenseId);
            throw new DatabaseException($"Failed to submit expense {expenseId}. Check managed identity configuration.", ex, "ExpenseService.cs", 183);
        }
    }

    public async Task<bool> ApproveExpenseAsync(ApproveExpenseRequest request)
    {
        if (_useDummyData) return true;

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_ApproveRejectExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@ReviewerId", request.ReviewerId);
            command.Parameters.AddWithValue("@Approve", request.Approve);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving/rejecting expense {ExpenseId}", request.ExpenseId);
            throw new DatabaseException($"Failed to approve/reject expense {request.ExpenseId}. Check managed identity configuration.", ex, "ExpenseService.cs", 206);
        }
    }

    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        if (_useDummyData) return GetDummyCategories();

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetCategories", connection);
            command.CommandType = CommandType.StoredProcedure;

            var categories = new List<ExpenseCategory>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32("CategoryId"),
                    CategoryName = reader.GetString("CategoryName"),
                    IsActive = reader.GetBoolean("IsActive")
                });
            }
            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories from database");
            throw new DatabaseException("Failed to retrieve categories. Check managed identity configuration.", ex, "ExpenseService.cs", 235);
        }
    }

    public async Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        if (_useDummyData) return GetDummyStatuses();

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetStatuses", connection);
            command.CommandType = CommandType.StoredProcedure;

            var statuses = new List<ExpenseStatus>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32("StatusId"),
                    StatusName = reader.GetString("StatusName")
                });
            }
            return statuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching statuses from database");
            throw new DatabaseException("Failed to retrieve statuses. Check managed identity configuration.", ex, "ExpenseService.cs", 263);
        }
    }

    public async Task<List<User>> GetUsersAsync()
    {
        if (_useDummyData) return GetDummyUsers();

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetUsers", connection);
            command.CommandType = CommandType.StoredProcedure;

            var users = new List<User>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32("UserId"),
                    UserName = reader.GetString("UserName"),
                    Email = reader.GetString("Email"),
                    RoleId = reader.GetInt32("RoleId"),
                    ManagerId = reader.IsDBNull("ManagerId") ? null : reader.GetInt32("ManagerId"),
                    IsActive = reader.GetBoolean("IsActive"),
                    RoleName = reader.IsDBNull("RoleName") ? null : reader.GetString("RoleName")
                });
            }
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users from database");
            throw new DatabaseException("Failed to retrieve users. Check managed identity configuration.", ex, "ExpenseService.cs", 296);
        }
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        if (_useDummyData) return GetDummyUsers().FirstOrDefault(u => u.UserId == userId);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetUserById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    UserId = reader.GetInt32("UserId"),
                    UserName = reader.GetString("UserName"),
                    Email = reader.GetString("Email"),
                    RoleId = reader.GetInt32("RoleId"),
                    ManagerId = reader.IsDBNull("ManagerId") ? null : reader.GetInt32("ManagerId"),
                    IsActive = reader.GetBoolean("IsActive"),
                    RoleName = reader.IsDBNull("RoleName") ? null : reader.GetString("RoleName")
                };
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {UserId} from database", userId);
            throw new DatabaseException($"Failed to retrieve user {userId}. Check managed identity configuration.", ex, "ExpenseService.cs", 329);
        }
    }

    private static Expense MapExpenseFromReader(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32("ExpenseId"),
            UserId = reader.GetInt32("UserId"),
            CategoryId = reader.GetInt32("CategoryId"),
            StatusId = reader.GetInt32("StatusId"),
            AmountMinor = reader.GetInt32("AmountMinor"),
            Currency = reader.GetString("Currency"),
            ExpenseDate = reader.GetDateTime("ExpenseDate"),
            Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
            ReceiptFile = reader.IsDBNull("ReceiptFile") ? null : reader.GetString("ReceiptFile"),
            SubmittedAt = reader.IsDBNull("SubmittedAt") ? null : reader.GetDateTime("SubmittedAt"),
            ReviewedBy = reader.IsDBNull("ReviewedBy") ? null : reader.GetInt32("ReviewedBy"),
            ReviewedAt = reader.IsDBNull("ReviewedAt") ? null : reader.GetDateTime("ReviewedAt"),
            CreatedAt = reader.GetDateTime("CreatedAt"),
            CategoryName = reader.IsDBNull("CategoryName") ? null : reader.GetString("CategoryName"),
            StatusName = reader.IsDBNull("StatusName") ? null : reader.GetString("StatusName"),
            UserName = reader.IsDBNull("UserName") ? null : reader.GetString("UserName")
        };
    }

    // Dummy data methods for fallback when database is unavailable
    private List<Expense> GetDummyExpenses(ExpenseFilterRequest? filter = null)
    {
        var expenses = new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, CategoryId = 1, StatusId = 2, AmountMinor = 12000, Currency = "GBP", ExpenseDate = new DateTime(2024, 1, 15), Description = "Taxi from airport to client site", CategoryName = "Travel", StatusName = "Submitted", UserName = "Alice Example" },
            new() { ExpenseId = 2, UserId = 1, CategoryId = 2, StatusId = 2, AmountMinor = 6900, Currency = "GBP", ExpenseDate = new DateTime(2023, 1, 10), Description = "Client lunch meeting", CategoryName = "Meals", StatusName = "Submitted", UserName = "Alice Example" },
            new() { ExpenseId = 3, UserId = 1, CategoryId = 3, StatusId = 3, AmountMinor = 9950, Currency = "GBP", ExpenseDate = new DateTime(2023, 12, 4), Description = "Office stationery", CategoryName = "Supplies", StatusName = "Approved", UserName = "Alice Example" },
            new() { ExpenseId = 4, UserId = 1, CategoryId = 1, StatusId = 3, AmountMinor = 1920, Currency = "GBP", ExpenseDate = new DateTime(2023, 12, 18), Description = "Bus fare", CategoryName = "Travel", StatusName = "Approved", UserName = "Alice Example" }
        };

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                expenses = expenses.Where(e => 
                    (e.Description?.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.CategoryName?.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }
            if (filter.CategoryId.HasValue)
                expenses = expenses.Where(e => e.CategoryId == filter.CategoryId.Value).ToList();
            if (filter.StatusId.HasValue)
                expenses = expenses.Where(e => e.StatusId == filter.StatusId.Value).ToList();
        }

        return expenses;
    }

    private static List<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }

    private static List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee" },
            new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager" }
        };
    }
}

public class DatabaseException : Exception
{
    public string FileName { get; }
    public int LineNumber { get; }

    public DatabaseException(string message, Exception innerException, string fileName, int lineNumber)
        : base(message, innerException)
    {
        FileName = fileName;
        LineNumber = lineNumber;
    }

    public string GetDetailedMessage()
    {
        var innerMessage = InnerException?.Message ?? "Unknown error";
        
        if (innerMessage.Contains("managed identity", StringComparison.OrdinalIgnoreCase) ||
            innerMessage.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            innerMessage.Contains("login failed", StringComparison.OrdinalIgnoreCase))
        {
            return $"Managed Identity Authentication Error in {FileName} at line {LineNumber}: {Message}. " +
                   $"Fix: Ensure the managed identity has been assigned to the Azure SQL Server with db_datareader and db_datawriter roles. " +
                   $"Run the script.sql file against the database to configure identity permissions. " +
                   $"Inner error: {innerMessage}";
        }

        return $"Database Error in {FileName} at line {LineNumber}: {Message}. Inner error: {innerMessage}";
    }
}
