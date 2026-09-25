using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SubscriptionRepository> _logger;

    public SubscriptionRepository(ApplicationDbContext db, ILogger<SubscriptionRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetActivePlansAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT p.*
            FROM dbo.SubscriptionPlans p
            WHERE p.IsActive = 1
            ORDER BY p.DisplayOrder ASC, p.Price ASC";

        var plans = await _db.SubscriptionPlans
            .FromSqlRaw(sql)
            .AsNoTracking()
            .ToListAsync(ct);

        return plans.Select(p => new SubscriptionPlanDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            BillingCycleDays = p.BillingCycleDays,
            MaxActiveJobs = p.MaxActiveJobs,
            Features = ParseFeatures(p.FeaturesJson),
            IsActive = p.IsActive,
            DisplayOrder = p.DisplayOrder
        }).ToList();
    }

    public async Task<SubscriptionPlan?> GetPlanByIdAsync(int planId, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.Int) { Value = planId };
        const string sql = "SELECT p.* FROM dbo.SubscriptionPlans p WHERE p.Id = @id";

        return await _db.SubscriptionPlans
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CompanySubscriptionDto?> GetActiveSubscriptionAsync(Guid companyId, CancellationToken ct = default)
    {
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            SELECT TOP(1) 
                s.Id, s.PlanId, p.Name AS PlanName, p.Code AS PlanCode, s.Status, 
                s.StartDate, s.EndDate, s.MaxActiveJobsSnapshot AS MaxActiveJobs
            FROM dbo.CompanySubscriptions s
            INNER JOIN dbo.SubscriptionPlans p ON s.PlanId = p.Id
            WHERE s.CompanyId = @companyId 
              AND s.Status = 1 
              AND s.EndDate >= SYSDATETIMEOFFSET()
            ORDER BY s.EndDate DESC";

        var row = await _db.Database
            .SqlQueryRaw<ActiveSubscriptionRowResult>(sql, companyIdParam)
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            // Check if there is an expired or pending subscription to inform UI
            const string fallbackSql = @"
                SELECT TOP(1) 
                    s.Id, s.PlanId, p.Name AS PlanName, p.Code AS PlanCode, s.Status, 
                    s.StartDate, s.EndDate, s.MaxActiveJobsSnapshot AS MaxActiveJobs
                FROM dbo.CompanySubscriptions s
                INNER JOIN dbo.SubscriptionPlans p ON s.PlanId = p.Id
                WHERE s.CompanyId = @companyId
                ORDER BY s.EndDate DESC, s.CreatedAt DESC";

            row = await _db.Database
                .SqlQueryRaw<ActiveSubscriptionRowResult>(fallbackSql, new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId })
                .FirstOrDefaultAsync(ct);

            if (row is null) return null;
        }

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((row.EndDate - DateTimeOffset.UtcNow).TotalDays));
        var isExpired = row.EndDate <= DateTimeOffset.UtcNow || row.Status != (byte)SubscriptionStatus.Active;

        return new CompanySubscriptionDto
        {
            Id = row.Id,
            PlanId = row.PlanId,
            PlanName = row.PlanName,
            PlanCode = row.PlanCode,
            Status = (SubscriptionStatus)row.Status,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            MaxActiveJobs = row.MaxActiveJobs,
            DaysRemaining = isExpired ? 0 : daysRemaining,
            IsExpired = isExpired,
            IsExpiringSoon = !isExpired && daysRemaining <= 7
        };
    }

    public async Task<int> GetActiveJobsCountAsync(Guid companyId, CancellationToken ct = default)
    {
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };
        const string sql = @"
            SELECT COUNT(1) AS Value
            FROM dbo.Jobs j
            WHERE j.CompanyId = @companyId 
              AND j.IsClosed = 0 
              AND j.DeadLine >= SYSDATETIMEOFFSET()";

        return await _db.Database
            .SqlQueryRaw<int>(sql, companyIdParam)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentTransactionDto>> GetCompanyTransactionHistoryAsync(Guid companyId, CancellationToken ct = default)
    {
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            SELECT 
                t.Id, t.CompanyId, c.CompanyName, t.InvoiceNumber, p.Name AS PlanName, 
                t.PaymentMethod, t.Amount, t.Currency, t.Status, t.BkashPaymentId, 
                t.BkashTrxId, t.PayerMsisdn, t.CustomerReference, t.PaymentExecuteTime, t.CreatedAt
            FROM dbo.PaymentTransactions t
            INNER JOIN dbo.Companies c ON t.CompanyId = c.Id
            INNER JOIN dbo.SubscriptionPlans p ON t.PlanId = p.Id
            WHERE t.CompanyId = @companyId
            ORDER BY t.CreatedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<PaymentTransactionRowResult>(sql, companyIdParam)
            .ToListAsync(ct);

        return rows.Select(MapTransactionDto).ToList();
    }

    public async Task<PaymentTransaction?> GetTransactionByInvoiceAsync(string invoiceNumber, CancellationToken ct = default)
    {
        var invoiceParam = new SqlParameter("@inv", SqlDbType.NVarChar, 50) { Value = invoiceNumber.Trim() };
        const string sql = "SELECT t.* FROM dbo.PaymentTransactions t WHERE t.InvoiceNumber = @inv";

        return await _db.PaymentTransactions
            .FromSqlRaw(sql, invoiceParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaymentTransaction?> GetTransactionByBkashPaymentIdAsync(string paymentId, CancellationToken ct = default)
    {
        var pidParam = new SqlParameter("@pid", SqlDbType.NVarChar, 100) { Value = paymentId.Trim() };
        const string sql = "SELECT t.* FROM dbo.PaymentTransactions t WHERE t.BkashPaymentId = @pid";

        return await _db.PaymentTransactions
            .FromSqlRaw(sql, pidParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaymentTransaction> CreateTransactionAsync(
        Guid companyId,
        int planId,
        string invoiceNumber,
        decimal amount,
        string paymentMethod,
        string? customerRef = null,
        string? bkashPaymentId = null,
        CancellationToken ct = default)
    {
        var transactionId = Guid.NewGuid();
        const string sql = @"
            INSERT INTO dbo.PaymentTransactions (
                Id, CompanyId, PlanId, InvoiceNumber, PaymentMethod, Amount, Currency, 
                Status, BkashPaymentId, CustomerReference, CreatedAt
            )
            VALUES (
                @id, @companyId, @planId, @invoiceNo, @method, @amount, N'BDT', 
                0, @bkashPaymentId, @customerRef, SYSDATETIMEOFFSET()
            )";

        await _db.Database.ExecuteSqlRawAsync(sql, new object[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = transactionId },
            new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId },
            new SqlParameter("@planId", SqlDbType.Int) { Value = planId },
            new SqlParameter("@invoiceNo", SqlDbType.NVarChar, 50) { Value = invoiceNumber.Trim() },
            new SqlParameter("@method", SqlDbType.NVarChar, 30) { Value = paymentMethod.Trim() },
            new SqlParameter("@amount", SqlDbType.Decimal) { Value = amount, Precision = 18, Scale = 2 },
            new SqlParameter("@bkashPaymentId", SqlDbType.NVarChar, 100) { Value = (object?)bkashPaymentId ?? DBNull.Value },
            new SqlParameter("@customerRef", SqlDbType.NVarChar, 100) { Value = (object?)customerRef ?? DBNull.Value }
        }, ct);

        return (await GetTransactionByInvoiceAsync(invoiceNumber, ct))!;
    }

    public async Task<bool> CompletePaymentAndActivateSubscriptionAsync(
        Guid transactionId,
        string? bkashTrxId,
        string? payerMsisdn,
        DateTimeOffset executeTime,
        string? rawResponse,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // 1. Fetch transaction
                var trxParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = transactionId };
                const string getTrxSql = "SELECT t.* FROM dbo.PaymentTransactions t WHERE t.Id = @id";
                var transaction = await _db.PaymentTransactions.FromSqlRaw(getTrxSql, trxParam).FirstOrDefaultAsync(ct);

                if (transaction is null)
                {
                    return false;
                }

                if (transaction.Status == PaymentTransactionStatus.Completed)
                {
                    // Idempotent: already completed
                    return true;
                }

                // 2. Fetch plan
                var plan = await GetPlanByIdAsync(transaction.PlanId, ct);
                if (plan is null)
                {
                    return false;
                }

                // 3. Determine cumulative StartDate and EndDate
                var activeSub = await GetActiveSubscriptionAsync(transaction.CompanyId, ct);
                DateTimeOffset newStartDate;
                DateTimeOffset newEndDate;

                if (activeSub != null && !activeSub.IsExpired && activeSub.EndDate > DateTimeOffset.UtcNow)
                {
                    // Cumulative extension: preserve remaining days and add plan days
                    newStartDate = activeSub.StartDate;
                    newEndDate = activeSub.EndDate.AddDays(plan.BillingCycleDays);
                }
                else
                {
                    // Fresh activation from right now
                    newStartDate = DateTimeOffset.UtcNow;
                    newEndDate = DateTimeOffset.UtcNow.AddDays(plan.BillingCycleDays);
                }

                // Expire any existing active subscriptions for this company
                const string expireOldSql = @"
                    UPDATE dbo.CompanySubscriptions 
                    SET Status = 2, UpdatedAt = SYSDATETIMEOFFSET() 
                    WHERE CompanyId = @companyId AND Status = 1";
                await _db.Database.ExecuteSqlRawAsync(expireOldSql, new object[] {
                    new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = transaction.CompanyId }
                }, ct);

                // 4. Insert new active subscription
                var newSubId = Guid.NewGuid();
                const string insertSubSql = @"
                    INSERT INTO dbo.CompanySubscriptions (
                        Id, CompanyId, PlanId, Status, StartDate, EndDate, 
                        MaxActiveJobsSnapshot, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        @id, @companyId, @planId, 1, @start, @end, 
                        @maxJobs, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
                    )";

                await _db.Database.ExecuteSqlRawAsync(insertSubSql, new object[] {
                    new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = newSubId },
                    new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = transaction.CompanyId },
                    new SqlParameter("@planId", SqlDbType.Int) { Value = plan.Id },
                    new SqlParameter("@start", SqlDbType.DateTimeOffset) { Value = newStartDate },
                    new SqlParameter("@end", SqlDbType.DateTimeOffset) { Value = newEndDate },
                    new SqlParameter("@maxJobs", SqlDbType.Int) { Value = plan.MaxActiveJobs }
                }, ct);

                // 5. Update transaction to Completed
                const string updateTrxSql = @"
                    UPDATE dbo.PaymentTransactions
                    SET Status = 1,
                        SubscriptionId = @subId,
                        BkashTrxId = @trxId,
                        PayerMsisdn = @msisdn,
                        PaymentExecuteTime = @execTime,
                        RawGatewayResponse = @raw
                    WHERE Id = @trxIdKey";

                await _db.Database.ExecuteSqlRawAsync(updateTrxSql, new object[] {
                    new SqlParameter("@trxIdKey", SqlDbType.UniqueIdentifier) { Value = transactionId },
                    new SqlParameter("@subId", SqlDbType.UniqueIdentifier) { Value = newSubId },
                    new SqlParameter("@trxId", SqlDbType.NVarChar, 100) { Value = (object?)bkashTrxId ?? DBNull.Value },
                    new SqlParameter("@msisdn", SqlDbType.NVarChar, 30) { Value = (object?)payerMsisdn ?? DBNull.Value },
                    new SqlParameter("@execTime", SqlDbType.DateTimeOffset) { Value = executeTime },
                    new SqlParameter("@raw", SqlDbType.NVarChar, -1) { Value = (object?)rawResponse ?? DBNull.Value }
                }, ct);

                await dbTransaction.CommitAsync(ct);
                _logger.LogInformation("Company {CompanyId} subscription activated for plan {PlanCode} until {EndDate}",
                    transaction.CompanyId, plan.Code, newEndDate);
                return true;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to complete payment and activate subscription for transaction {TransactionId}", transactionId);
                throw;
            }
        });
    }

    public async Task<bool> MarkTransactionFailedAsync(Guid transactionId, string? reason, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.PaymentTransactions
            SET Status = 2,
                RawGatewayResponse = @raw
            WHERE Id = @id AND Status = 0";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, new object[] {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = transactionId },
            new SqlParameter("@raw", SqlDbType.NVarChar, -1) { Value = (object?)reason ?? DBNull.Value }
        }, ct);

        return rows > 0;
    }

    public async Task<bool> SubmitManualPaymentRequestAsync(
        Guid companyId,
        int planId,
        string paymentMethod,
        string referenceNumber,
        string? customerContact,
        string? notes,
        CancellationToken ct = default)
    {
        var plan = await GetPlanByIdAsync(planId, ct);
        if (plan is null) return false;

        var invoiceNo = $"INV-OFFLINE-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var transactionId = Guid.NewGuid();

        const string sql = @"
            INSERT INTO dbo.PaymentTransactions (
                Id, CompanyId, PlanId, InvoiceNumber, PaymentMethod, Amount, Currency, 
                Status, CustomerReference, PayerMsisdn, RawGatewayResponse, CreatedAt
            )
            VALUES (
                @id, @companyId, @planId, @invoiceNo, @method, @amount, N'BDT', 
                0, @ref, @contact, @notes, SYSDATETIMEOFFSET()
            )";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, new object[] {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = transactionId },
            new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId },
            new SqlParameter("@planId", SqlDbType.Int) { Value = planId },
            new SqlParameter("@invoiceNo", SqlDbType.NVarChar, 50) { Value = invoiceNo },
            new SqlParameter("@method", SqlDbType.NVarChar, 30) { Value = paymentMethod.Trim() },
            new SqlParameter("@amount", SqlDbType.Decimal) { Value = plan.Price, Precision = 18, Scale = 2 },
            new SqlParameter("@ref", SqlDbType.NVarChar, 100) { Value = referenceNumber.Trim() },
            new SqlParameter("@contact", SqlDbType.NVarChar, 30) { Value = (object?)customerContact?.Trim() ?? DBNull.Value },
            new SqlParameter("@notes", SqlDbType.NVarChar, -1) { Value = (object?)notes?.Trim() ?? DBNull.Value }
        }, ct);

        return rows > 0;
    }

    public async Task<AdminMonetizationDashboardViewModel> GetAdminDashboardMetricsAsync(CancellationToken ct = default)
    {
        // 1. KPI Aggregates
        const string kpiSql = @"
            SELECT 
                ISNULL((SELECT SUM(Amount) FROM dbo.PaymentTransactions WHERE Status = 1), 0) AS TotalRevenueBdt,
                (SELECT COUNT(DISTINCT CompanyId) FROM dbo.CompanySubscriptions WHERE Status = 1 AND EndDate >= SYSDATETIMEOFFSET()) AS ActiveSubscriptionsCount,
                (SELECT COUNT(DISTINCT CompanyId) FROM dbo.CompanySubscriptions WHERE Status = 1 AND EndDate >= SYSDATETIMEOFFSET() AND EndDate <= DATEADD(day, 7, SYSDATETIMEOFFSET())) AS ExpiringSoonCount,
                (SELECT COUNT(1) FROM dbo.PaymentTransactions WHERE Status = 0) AS PendingApprovalsCount";

        var kpi = await _db.Database.SqlQueryRaw<AdminKpiRowResult>(kpiSql).FirstOrDefaultAsync(ct)
                  ?? new AdminKpiRowResult();

        // 2. Pending Requests
        const string pendingSql = @"
            SELECT 
                t.Id AS TransactionId, t.CompanyId, c.CompanyName, u.Email AS ContactEmail, 
                t.InvoiceNumber, p.Name AS PlanName, t.Amount, t.PaymentMethod, 
                t.CustomerReference, t.PayerMsisdn, t.CreatedAt
            FROM dbo.PaymentTransactions t
            INNER JOIN dbo.Companies c ON t.CompanyId = c.Id
            INNER JOIN dbo.AspNetUsers u ON c.UserId = u.Id
            INNER JOIN dbo.SubscriptionPlans p ON t.PlanId = p.Id
            WHERE t.Status = 0
            ORDER BY t.CreatedAt ASC";

        var pendingRows = await _db.Database.SqlQueryRaw<AdminPaymentRequestRowDto>(pendingSql).ToListAsync(ct);

        // 3. Company Subscriptions Roster
        const string rosterSql = @"
            SELECT 
                c.Id AS CompanyId,
                c.CompanyName,
                ISNULL(c.IndustrySector, N'Technology') AS IndustrySector,
                u.Email AS ContactEmail,
                ISNULL(p.Name, N'No Active Plan') AS PlanName,
                s.Status,
                s.EndDate,
                CASE 
                    WHEN s.EndDate IS NOT NULL AND s.EndDate > SYSDATETIMEOFFSET() 
                    THEN DATEDIFF(day, SYSDATETIMEOFFSET(), s.EndDate)
                    ELSE 0 
                END AS DaysRemaining,
                ISNULL((SELECT COUNT(1) FROM dbo.Jobs j WHERE j.CompanyId = c.Id AND j.IsClosed = 0 AND j.DeadLine >= SYSDATETIMEOFFSET()), 0) AS ActiveJobsCount,
                ISNULL(s.MaxActiveJobsSnapshot, 0) AS MaxActiveJobs,
                ISNULL((SELECT SUM(t.Amount) FROM dbo.PaymentTransactions t WHERE t.CompanyId = c.Id AND t.Status = 1), 0) AS TotalPaidBdt
            FROM dbo.Companies c
            INNER JOIN dbo.AspNetUsers u ON c.UserId = u.Id
            LEFT JOIN dbo.CompanySubscriptions s ON s.CompanyId = c.Id AND s.Status = 1 AND s.EndDate >= SYSDATETIMEOFFSET()
            LEFT JOIN dbo.SubscriptionPlans p ON s.PlanId = p.Id
            ORDER BY DaysRemaining DESC, c.CompanyName ASC";

        var rosterRows = await _db.Database.SqlQueryRaw<AdminCompanySubscriptionRowDto>(rosterSql).ToListAsync(ct);

        // 4. Recent Transactions
        const string trxSql = @"
            SELECT TOP(50)
                t.Id, t.CompanyId, c.CompanyName, t.InvoiceNumber, p.Name AS PlanName, 
                t.PaymentMethod, t.Amount, t.Currency, t.Status, t.BkashPaymentId, 
                t.BkashTrxId, t.PayerMsisdn, t.CustomerReference, t.PaymentExecuteTime, t.CreatedAt
            FROM dbo.PaymentTransactions t
            INNER JOIN dbo.Companies c ON t.CompanyId = c.Id
            INNER JOIN dbo.SubscriptionPlans p ON t.PlanId = p.Id
            ORDER BY t.CreatedAt DESC";

        var trxRows = await _db.Database.SqlQueryRaw<PaymentTransactionRowResult>(trxSql).ToListAsync(ct);

        var plans = await GetActivePlansAsync(ct);

        return new AdminMonetizationDashboardViewModel
        {
            TotalRevenueBdt = kpi.TotalRevenueBdt,
            ActiveSubscriptionsCount = kpi.ActiveSubscriptionsCount,
            ExpiringSoonCount = kpi.ExpiringSoonCount,
            PendingApprovalsCount = kpi.PendingApprovalsCount,
            PendingRequests = pendingRows,
            CompanySubscriptions = rosterRows,
            Transactions = trxRows.Select(MapTransactionDto).ToList(),
            Plans = plans
        };
    }

    public async Task<bool> ApproveManualRequestAsync(Guid transactionId, Guid adminUserId, string? notes = null, CancellationToken ct = default)
    {
        var trx = await _db.PaymentTransactions.FindAsync(new object[] { transactionId }, ct);
        if (trx is null || trx.Status != PaymentTransactionStatus.Initiated) return false;

        var trxRef = string.IsNullOrWhiteSpace(trx.CustomerReference) ? "APPROVED-ADMIN" : trx.CustomerReference;
        var success = await CompletePaymentAndActivateSubscriptionAsync(
            transactionId, 
            trxRef, 
            trx.PayerMsisdn ?? "Bank-Transfer", 
            DateTimeOffset.UtcNow, 
            $"Approved by Admin {adminUserId}. Notes: {notes}", 
            ct);

        if (success)
        {
            // Stamp approved by admin on subscription
            const string stampSql = @"
                UPDATE dbo.CompanySubscriptions
                SET ApprovedByAdminId = @adminId,
                    AdminNotes = @notes
                WHERE Id = (SELECT SubscriptionId FROM dbo.PaymentTransactions WHERE Id = @trxId)";

            await _db.Database.ExecuteSqlRawAsync(stampSql, new object[] {
                new SqlParameter("@adminId", SqlDbType.UniqueIdentifier) { Value = adminUserId },
                new SqlParameter("@notes", SqlDbType.NVarChar, 500) { Value = (object?)notes ?? "Manual Offline Approval" },
                new SqlParameter("@trxId", SqlDbType.UniqueIdentifier) { Value = transactionId }
            }, ct);
        }

        return success;
    }

    public async Task<bool> RejectManualRequestAsync(Guid transactionId, string reason, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.PaymentTransactions
            SET Status = 3, -- Cancelled
                RawGatewayResponse = @reason
            WHERE Id = @id AND Status = 0";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, new object[] {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = transactionId },
            new SqlParameter("@reason", SqlDbType.NVarChar, 500) { Value = $"Rejected by Admin: {reason.Trim()}" }
        }, ct);

        return rows > 0;
    }

    public async Task<bool> AdminManualGrantAsync(Guid companyId, int planId, int days, Guid adminUserId, string? notes, CancellationToken ct = default)
    {
        var plan = await GetPlanByIdAsync(planId, ct);
        if (plan is null) return false;

        var invoiceNo = $"INV-GRANT-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var transactionId = Guid.NewGuid();

        // 1. Create transaction with 0 BDT
        const string insertTrxSql = @"
            INSERT INTO dbo.PaymentTransactions (
                Id, CompanyId, PlanId, InvoiceNumber, PaymentMethod, Amount, Currency, 
                Status, CustomerReference, RawGatewayResponse, CreatedAt
            )
            VALUES (
                @id, @companyId, @planId, @invoiceNo, N'AdminGrant', 0.00, N'BDT', 
                0, N'Complimentary / Admin Grant', @notes, SYSDATETIMEOFFSET()
            )";

        await _db.Database.ExecuteSqlRawAsync(insertTrxSql, new object[] {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = transactionId },
            new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId },
            new SqlParameter("@planId", SqlDbType.Int) { Value = planId },
            new SqlParameter("@invoiceNo", SqlDbType.NVarChar, 50) { Value = invoiceNo },
            new SqlParameter("@notes", SqlDbType.NVarChar, -1) { Value = (object?)notes ?? "Admin complimentary grant" }
        }, ct);

        // 2. Activate with requested custom days
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var activeSub = await GetActiveSubscriptionAsync(companyId, ct);
                DateTimeOffset newStartDate;
                DateTimeOffset newEndDate;

                if (activeSub != null && !activeSub.IsExpired && activeSub.EndDate > DateTimeOffset.UtcNow)
                {
                    newStartDate = activeSub.StartDate;
                    newEndDate = activeSub.EndDate.AddDays(days);
                }
                else
                {
                    newStartDate = DateTimeOffset.UtcNow;
                    newEndDate = DateTimeOffset.UtcNow.AddDays(days);
                }

                // Expire older active subscriptions
                const string expireOldSql = "UPDATE dbo.CompanySubscriptions SET Status = 2 WHERE CompanyId = @companyId AND Status = 1";
                await _db.Database.ExecuteSqlRawAsync(expireOldSql, new object[] {
                    new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId }
                }, ct);

                var newSubId = Guid.NewGuid();
                const string insertSubSql = @"
                    INSERT INTO dbo.CompanySubscriptions (
                        Id, CompanyId, PlanId, Status, StartDate, EndDate, 
                        MaxActiveJobsSnapshot, ApprovedByAdminId, AdminNotes, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        @id, @companyId, @planId, 1, @start, @end, 
                        @maxJobs, @adminId, @notes, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
                    )";

                await _db.Database.ExecuteSqlRawAsync(insertSubSql, new object[] {
                    new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = newSubId },
                    new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId },
                    new SqlParameter("@planId", SqlDbType.Int) { Value = planId },
                    new SqlParameter("@start", SqlDbType.DateTimeOffset) { Value = newStartDate },
                    new SqlParameter("@end", SqlDbType.DateTimeOffset) { Value = newEndDate },
                    new SqlParameter("@maxJobs", SqlDbType.Int) { Value = plan.MaxActiveJobs },
                    new SqlParameter("@adminId", SqlDbType.UniqueIdentifier) { Value = adminUserId },
                    new SqlParameter("@notes", SqlDbType.NVarChar, 500) { Value = (object?)notes ?? "Administrative Plan Grant" }
                }, ct);

                // Update transaction to Completed
                const string updateTrxSql = @"
                    UPDATE dbo.PaymentTransactions
                    SET Status = 1,
                        SubscriptionId = @subId,
                        PaymentExecuteTime = SYSDATETIMEOFFSET()
                    WHERE Id = @id";

                await _db.Database.ExecuteSqlRawAsync(updateTrxSql, new object[] {
                    new SqlParameter("@subId", SqlDbType.UniqueIdentifier) { Value = newSubId },
                    new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = transactionId }
                }, ct);

                await dbTransaction.CommitAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to execute AdminManualGrant for company {CompanyId}", companyId);
                return false;
            }
        });
    }

    private static List<string> ParseFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static PaymentTransactionDto MapTransactionDto(PaymentTransactionRowResult r) => new()
    {
        Id = r.Id,
        CompanyId = r.CompanyId,
        CompanyName = r.CompanyName,
        InvoiceNumber = r.InvoiceNumber,
        PlanName = r.PlanName,
        PaymentMethod = r.PaymentMethod,
        Amount = r.Amount,
        Currency = r.Currency,
        Status = (PaymentTransactionStatus)r.Status,
        BkashPaymentId = r.BkashPaymentId,
        BkashTrxId = r.BkashTrxId,
        PayerMsisdn = r.PayerMsisdn,
        CustomerReference = r.CustomerReference,
        PaymentExecuteTime = r.PaymentExecuteTime,
        CreatedAt = r.CreatedAt
    };
}

public class ActiveSubscriptionRowResult
{
    public Guid Id { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public byte Status { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public int MaxActiveJobs { get; set; }
}

public class PaymentTransactionRowResult
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BDT";
    public byte Status { get; set; }
    public string? BkashPaymentId { get; set; }
    public string? BkashTrxId { get; set; }
    public string? PayerMsisdn { get; set; }
    public string? CustomerReference { get; set; }
    public DateTimeOffset? PaymentExecuteTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AdminKpiRowResult
{
    public decimal TotalRevenueBdt { get; set; }
    public int ActiveSubscriptionsCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int PendingApprovalsCount { get; set; }
}
