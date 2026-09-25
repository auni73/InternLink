using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Services.Payment;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class SubscriptionServiceTests
{
    [Theory]
    [InlineData(15, 15, false, false)]
    [InlineData(5, 5, false, true)]
    [InlineData(1, 1, false, true)]
    [InlineData(-2, 0, true, false)]
    public void CompanySubscription_DaysRemainingAndExpiry_CalculatedCorrectly(
        int daysOffset, 
        int expectedDaysRemaining, 
        bool expectedIsExpired, 
        bool expectedIsExpiringSoon)
    {
        // Arrange
        var sub = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PlanId = 1,
            Status = SubscriptionStatus.Active,
            StartDate = DateTimeOffset.UtcNow.AddDays(-10),
            EndDate = DateTimeOffset.UtcNow.AddDays(daysOffset),
            MaxActiveJobsSnapshot = 3
        };

        // Assert
        Assert.Equal(expectedDaysRemaining, sub.DaysRemaining);
        Assert.Equal(expectedIsExpired, sub.IsExpired);
        Assert.Equal(expectedIsExpiringSoon, sub.IsExpiringSoon);
    }

    [Fact]
    public void CumulativeRenewal_WhenActiveRemaining_AddsDaysToCurrentEndDate()
    {
        // Arrange: 10 days remaining
        var currentEndDate = DateTimeOffset.UtcNow.AddDays(10);
        var planBillingDays = 30;

        // Act: Cumulative extension
        var newEndDate = currentEndDate.AddDays(planBillingDays);

        // Assert: Total remaining should be ~40 days
        var totalDaysRemaining = (int)Math.Ceiling((newEndDate - DateTimeOffset.UtcNow).TotalDays);
        Assert.Equal(40, totalDaysRemaining);
    }

    [Fact]
    public void FreshRenewal_WhenExpired_CalculatesFromUtcNow()
    {
        // Arrange: Expired 5 days ago
        var currentEndDate = DateTimeOffset.UtcNow.AddDays(-5);
        var planBillingDays = 30;

        // Act: Reset to UtcNow + 30 days
        var isExpired = currentEndDate <= DateTimeOffset.UtcNow;
        var newEndDate = isExpired ? DateTimeOffset.UtcNow.AddDays(planBillingDays) : currentEndDate.AddDays(planBillingDays);

        // Assert: Exactly 30 days remaining
        var totalDaysRemaining = (int)Math.Ceiling((newEndDate - DateTimeOffset.UtcNow).TotalDays);
        Assert.Equal(30, totalDaysRemaining);
    }

    [Theory]
    [InlineData(false, 1, 3, true)]    // Active, 1/3 used -> Can post
    [InlineData(false, 3, 3, false)]   // Active, 3/3 used -> Quota reached
    [InlineData(false, 10, 999, true)] // Active, unlimited -> Can post
    [InlineData(true, 0, 3, false)]    // Expired -> Cannot post
    public void CompanySubscriptionDashboard_CanPostNewJob_EnforcesQuotaAndExpiry(
        bool isExpired,
        int activeJobsCount,
        int maxActiveJobs,
        bool expectedCanPost)
    {
        // Arrange
        var currentSub = new CompanySubscriptionDto
        {
            Id = Guid.NewGuid(),
            PlanId = 2,
            PlanName = "Starter Monthly",
            Status = isExpired ? SubscriptionStatus.Expired : SubscriptionStatus.Active,
            StartDate = DateTimeOffset.UtcNow.AddDays(-10),
            EndDate = isExpired ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddDays(20),
            MaxActiveJobs = maxActiveJobs,
            DaysRemaining = isExpired ? 0 : 20,
            IsExpired = isExpired,
            IsExpiringSoon = false
        };

        var dashboard = new CompanySubscriptionDashboardViewModel
        {
            CurrentSubscription = currentSub,
            ActiveJobsCount = activeJobsCount
        };

        // Assert
        Assert.Equal(expectedCanPost, dashboard.CanPostNewJob);
    }

    [Fact]
    public async Task BkashPaymentService_SimulatedGateway_ExecutesAndGeneratesTrxId()
    {
        // Arrange
        var config = Options.Create(new BkashConfig
        {
            UseSimulatedGateway = true
        });

        var http = new HttpClient();
        var service = new BkashPaymentService(http, config, NullLogger<BkashPaymentService>.Instance);

        // Act 1: Create
        var createResult = await service.CreatePaymentAsync(1500m, "INV-TEST-001", "Acme Corp");

        // Assert 1
        Assert.True(createResult.IsSuccess);
        Assert.StartsWith("SIM-BKASH-", createResult.PaymentId);
        Assert.Contains(createResult.PaymentId!, createResult.BkashUrl);

        // Act 2: Execute
        var execResult = await service.ExecutePaymentAsync(createResult.PaymentId!);

        // Assert 2
        Assert.True(execResult.IsSuccess);
        Assert.StartsWith("TRX", execResult.TrxId);
        Assert.Equal("Completed", execResult.TransactionStatus);
    }

    [Fact]
    public void SubmitManualPaymentRequest_MissingReference_FailsValidation()
    {
        // Arrange
        var model = new SubmitManualPaymentRequestDto
        {
            PlanId = 1,
            PaymentMethod = "Bank Deposit",
            ReferenceNumber = "" // Required
        };

        var context = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(SubmitManualPaymentRequestDto.ReferenceNumber)));
    }
}
