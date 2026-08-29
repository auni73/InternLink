using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class AdminModerationTests
{
    [Theory]
    [InlineData(0, 15, 1, false, false)]
    [InlineData(10, 15, 1, false, false)]
    [InlineData(15, 15, 1, false, false)]
    [InlineData(16, 15, 2, false, true)]
    [InlineData(30, 15, 2, false, true)]
    [InlineData(45, 15, 3, false, true)]
    public void AdminUserListViewModel_PaginationCalculations_ShouldBeAccurate(
        int totalCount, 
        int pageSize, 
        int expectedTotalPages, 
        bool hasPrevOnPage1, 
        bool hasNextOnPage1)
    {
        var vm = new AdminUserListViewModel
        {
            TotalCount = totalCount,
            PageSize = pageSize,
            Page = 1
        };

        Assert.Equal(expectedTotalPages, vm.TotalPages);
        Assert.Equal(hasPrevOnPage1, vm.HasPreviousPage);
        Assert.Equal(hasNextOnPage1, vm.HasNextPage);
    }

    [Fact]
    public void AdminUserListViewModel_MiddlePage_ShouldHaveBothPreviousAndNext()
    {
        var vm = new AdminUserListViewModel
        {
            TotalCount = 50,
            PageSize = 15,
            Page = 2
        };

        Assert.Equal(4, vm.TotalPages);
        Assert.True(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);
    }

    [Fact]
    public void AdminCompanyQueueViewModel_TotalCount_SumsAllStatuses()
    {
        var vm = new AdminCompanyQueueViewModel
        {
            PendingCount = 3,
            VerifiedCount = 12,
            RejectedCount = 2
        };

        Assert.Equal(17, vm.TotalCount);
    }

    [Fact]
    public void AdminJobQueueViewModel_TotalCount_SumsPendingAndApproved()
    {
        var vm = new AdminJobQueueViewModel
        {
            PendingCount = 5,
            ApprovedCount = 20
        };

        Assert.Equal(25, vm.TotalCount);
    }

    [Theory]
    [InlineData(VerificationStatus.Pending, 0)]
    [InlineData(VerificationStatus.Verified, 1)]
    [InlineData(VerificationStatus.Rejected, 2)]
    public void VerificationStatus_EnumByteValues_MatchDatabaseConvention(VerificationStatus status, byte expectedValue)
    {
        Assert.Equal(expectedValue, (byte)status);
    }

    [Fact]
    public void CompanyRejection_PreservesReasonText()
    {
        var dto = new CompanyRejectRequestDto
        {
            Reason = "Unverifiable domain registration"
        };

        var companyItem = new AdminCompanyQueueItemViewModel
        {
            CompanyId = Guid.NewGuid(),
            CompanyName = "Acme Fake Corp",
            VerificationStatus = VerificationStatus.Rejected,
            AdminRejectionReason = dto.Reason
        };

        Assert.Equal(VerificationStatus.Rejected, companyItem.VerificationStatus);
        Assert.Equal("Unverifiable domain registration", companyItem.AdminRejectionReason);
    }

    [Fact]
    public void CompanyApproval_ClearsRejectionReason()
    {
        var companyItem = new AdminCompanyQueueItemViewModel
        {
            CompanyId = Guid.NewGuid(),
            CompanyName = "Tech Corp",
            VerificationStatus = VerificationStatus.Verified,
            AdminRejectionReason = null
        };

        Assert.Equal(VerificationStatus.Verified, companyItem.VerificationStatus);
        Assert.Null(companyItem.AdminRejectionReason);
    }

    [Fact]
    public void JobVisibilityPredicate_OnlyApprovedAndOpenJobsAreVisibleToStudents()
    {
        var now = DateTimeOffset.UtcNow;

        var pendingJob = new AdminJobQueueItemViewModel
        {
            Title = "Backend Intern",
            IsApproved = false,
            IsClosed = false,
            DeadLine = now.AddDays(10)
        };

        var approvedJob = new AdminJobQueueItemViewModel
        {
            Title = "Frontend Intern",
            IsApproved = true,
            IsClosed = false,
            DeadLine = now.AddDays(10)
        };

        var closedJob = new AdminJobQueueItemViewModel
        {
            Title = "DevOps Intern",
            IsApproved = true,
            IsClosed = true,
            DeadLine = now.AddDays(10)
        };

        var expiredJob = new AdminJobQueueItemViewModel
        {
            Title = "ML Intern",
            IsApproved = true,
            IsClosed = false,
            DeadLine = now.AddDays(-1)
        };

        bool IsVisibleToStudents(AdminJobQueueItemViewModel j) =>
            j.IsApproved && !j.IsClosed && j.DeadLine >= now;

        Assert.False(IsVisibleToStudents(pendingJob), "Pending job must not be visible to students.");
        Assert.True(IsVisibleToStudents(approvedJob), "Approved, open, future job must be visible to students.");
        Assert.False(IsVisibleToStudents(closedJob), "Closed job must not be visible to students.");
        Assert.False(IsVisibleToStudents(expiredJob), "Expired job must not be visible to students.");
    }

    [Fact]
    public void UserModeration_DisplayNames_ProperlyFallbackToEmailOrUserName()
    {
        var studentUser = new AdminUserItemViewModel
        {
            UserId = Guid.NewGuid(),
            Email = "student@internlink.test",
            DisplayName = "Tanvir Ahmed",
            Role = "Student",
            IsActive = true
        };

        var unassignedUser = new AdminUserItemViewModel
        {
            UserId = Guid.NewGuid(),
            Email = "guest@internlink.test",
            DisplayName = "guest@internlink.test",
            Role = "Unassigned",
            IsActive = false
        };

        Assert.Equal("Tanvir Ahmed", studentUser.DisplayName);
        Assert.Equal("guest@internlink.test", unassignedUser.DisplayName);
        Assert.True(studentUser.IsActive);
        Assert.False(unassignedUser.IsActive);
    }
}
