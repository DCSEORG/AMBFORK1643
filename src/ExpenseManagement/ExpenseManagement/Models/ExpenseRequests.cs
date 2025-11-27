namespace ExpenseManagement.Models;

public class CreateExpenseRequest
{
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public int CategoryId { get; set; }
    public string? Description { get; set; }
    public int UserId { get; set; }
}

public class UpdateExpenseRequest
{
    public int ExpenseId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public int CategoryId { get; set; }
    public string? Description { get; set; }
    public int StatusId { get; set; }
}

public class ApproveExpenseRequest
{
    public int ExpenseId { get; set; }
    public int ReviewerId { get; set; }
    public bool Approve { get; set; }
}

public class ExpenseFilterRequest
{
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public int? StatusId { get; set; }
    public int? UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
