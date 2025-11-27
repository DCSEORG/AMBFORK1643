-- stored-procedures.sql
-- Stored procedures for Expense Management System
-- All app code uses these stored procedures instead of direct SQL

SET NOCOUNT ON;
GO

-- =============================================
-- Get Expenses with optional filtering
-- =============================================
CREATE OR ALTER PROCEDURE usp_GetExpenses
    @SearchTerm NVARCHAR(200) = NULL,
    @CategoryId INT = NULL,
    @StatusId INT = NULL,
    @UserId INT = NULL,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        c.CategoryName,
        s.StatusName,
        u.UserName
    FROM dbo.Expenses e
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    WHERE (@SearchTerm IS NULL OR e.Description LIKE '%' + @SearchTerm + '%' OR c.CategoryName LIKE '%' + @SearchTerm + '%')
      AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
      AND (@StatusId IS NULL OR e.StatusId = @StatusId)
      AND (@UserId IS NULL OR e.UserId = @UserId)
      AND (@FromDate IS NULL OR e.ExpenseDate >= @FromDate)
      AND (@ToDate IS NULL OR e.ExpenseDate <= @ToDate)
    ORDER BY e.ExpenseDate DESC, e.CreatedAt DESC;
END
GO

-- =============================================
-- Get Expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        c.CategoryName,
        s.StatusName,
        u.UserName
    FROM dbo.Expenses e
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Get Pending Expenses for Approval
-- =============================================
CREATE OR ALTER PROCEDURE usp_GetPendingExpenses
    @SearchTerm NVARCHAR(200) = NULL
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        c.CategoryName,
        s.StatusName,
        u.UserName
    FROM dbo.Expenses e
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    WHERE s.StatusName = 'Submitted'
      AND (@SearchTerm IS NULL OR e.Description LIKE '%' + @SearchTerm + '%' OR c.CategoryName LIKE '%' + @SearchTerm + '%' OR u.UserName LIKE '%' + @SearchTerm + '%')
    ORDER BY e.SubmittedAt ASC;
END
GO

-- =============================================
-- Create Expense
-- =============================================
CREATE OR ALTER PROCEDURE usp_CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @Currency NVARCHAR(3) = 'GBP'
AS
BEGIN
    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, CreatedAt)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS ExpenseId;
END
GO

-- =============================================
-- Update Expense
-- =============================================
CREATE OR ALTER PROCEDURE usp_UpdateExpense
    @ExpenseId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @StatusId INT
AS
BEGIN
    UPDATE dbo.Expenses
    SET CategoryId = @CategoryId,
        AmountMinor = @AmountMinor,
        ExpenseDate = @ExpenseDate,
        Description = @Description,
        StatusId = @StatusId
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Submit Expense for Approval
-- =============================================
CREATE OR ALTER PROCEDURE usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Approve or Reject Expense
-- =============================================
CREATE OR ALTER PROCEDURE usp_ApproveRejectExpense
    @ExpenseId INT,
    @ReviewerId INT,
    @Approve BIT
AS
BEGIN
    DECLARE @StatusId INT;
    
    IF @Approve = 1
        SELECT @StatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';
    ELSE
        SELECT @StatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';

    UPDATE dbo.Expenses
    SET StatusId = @StatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Get Categories
-- =============================================
CREATE OR ALTER PROCEDURE usp_GetCategories
AS
BEGIN
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- =============================================
-- Get Statuses
-- =============================================
CREATE OR ALTER PROCEDURE usp_GetStatuses
AS
BEGIN
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

-- =============================================
-- Get Users
-- =============================================
CREATE OR ALTER PROCEDURE usp_GetUsers
AS
BEGIN
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        u.ManagerId,
        u.IsActive,
        r.RoleName
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- =============================================
-- Get User by ID
-- =============================================
CREATE OR ALTER PROCEDURE usp_GetUserById
    @UserId INT
AS
BEGIN
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        u.ManagerId,
        u.IsActive,
        r.RoleName
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.UserId = @UserId;
END
GO
