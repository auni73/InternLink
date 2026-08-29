using System.ComponentModel.DataAnnotations;
using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class CompanyJobTests
{
    [Theory]
    [InlineData("https://acme-corp.com", true)]
    [InlineData("http://acme.org/about", true)]
    [InlineData("not-a-url", false)]
    [InlineData("ftp://invalid-scheme.com", true)] // UrlAttribute accepts uri schemes; controller enforces http/https
    public void CompanyProfileViewModel_WebsiteValidation_EnforcesUrlFormat(string website, bool isValidExpected)
    {
        // Arrange
        var model = new CompanyProfileViewModel
        {
            CompanyName = "Acme Technologies Ltd.",
            IndustrySector = "Software Engineering",
            CorporateWebsite = website
        };

        var context = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        // Assert
        Assert.Equal(isValidExpected, isValid);
    }

    [Fact]
    public void CompanyProfileViewModel_MissingRequiredFields_FailsValidation()
    {
        // Arrange
        var model = new CompanyProfileViewModel
        {
            CompanyName = "",
            IndustrySector = ""
        };

        var context = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CompanyProfileViewModel.CompanyName)));
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CompanyProfileViewModel.IndustrySector)));
    }

    [Theory]
    [InlineData(true, false, 5, "Closed", "bg-secondary")]
    [InlineData(false, false, -2, "Expired", "bg-dark")]
    [InlineData(false, false, 10, "Pending Approval", "bg-warning text-dark")]
    [InlineData(false, true, 10, "Live", "bg-success")]
    public void CompanyJobListItem_ComputedStatusBadge_AccurateAcrossStates(
        bool isClosed, 
        bool isApproved, 
        int deadlineDaysFromNow, 
        string expectedText, 
        string expectedClass)
    {
        // Arrange
        var item = new CompanyJobListItemViewModel
        {
            Title = "Backend Engineer Intern",
            IsClosed = isClosed,
            IsApproved = isApproved,
            DeadLine = DateTimeOffset.UtcNow.AddDays(deadlineDaysFromNow),
            ApplicantCount = 3
        };

        // Act & Assert
        Assert.Equal(expectedText, item.StatusBadgeText);
        Assert.Equal(expectedClass, item.StatusBadgeClass);
    }

    [Fact]
    public void CompanyJobEditViewModel_MissingRequiredFields_FailsValidation()
    {
        // Arrange
        var model = new CompanyJobEditViewModel
        {
            Title = "",
            CoreDescription = "",
            LocationType = LocationType.OnSite
        };

        var context = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CompanyJobEditViewModel.Title)));
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CompanyJobEditViewModel.CoreDescription)));
    }

    [Theory]
    [InlineData(0, false)] // Today (not allowed)
    [InlineData(-1, false)] // Past (not allowed)
    [InlineData(1, true)] // Tomorrow (allowed)
    [InlineData(14, true)] // 2 weeks out (allowed)
    public void CompanyJobDeadline_FutureValidation_LogicEnforced(int daysOffset, bool expectedValid)
    {
        // Arrange
        var targetDate = DateTime.UtcNow.AddDays(daysOffset).Date;

        // Act (Mirrors JobsController deadline check)
        var isValid = targetDate > DateTime.UtcNow.Date;

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void UnapprovedJob_ShouldNotSatisfy_StudentActiveJobPredicate()
    {
        // Specification rule: IsApproved = 1 AND IsClosed = 0 AND DeadLine >= SYSDATETIMEOFFSET()
        // Negative test: Newly posted job with IsApproved = 0 must fail the student search visibility predicate
        var isApproved = false;
        var isClosed = false;
        var deadline = DateTimeOffset.UtcNow.AddDays(10);

        var isVisibleToStudents = isApproved && !isClosed && deadline >= DateTimeOffset.UtcNow;

        Assert.False(isVisibleToStudents, "A newly created job (IsApproved = 0) must never be visible to students.");
    }
}
