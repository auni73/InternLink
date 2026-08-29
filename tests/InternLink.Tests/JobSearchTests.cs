using InternLink.Web.Helpers;
using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class JobSearchTests
{
    [Theory]
    [InlineData("c# .net core", "\"c#*\" AND \".net*\" AND \"core*\"")]
    [InlineData("software engineer", "\"software*\" AND \"engineer*\"")]
    [InlineData("  react   developer  ", "\"react*\" AND \"developer*\"")]
    [InlineData("\"asp.net\"", "\"asp.net*\"")]
    public void FtsQueryBuilder_ShouldBuildValidPrefixAndQuery(string rawInput, string expected)
    {
        // Act
        var result = FtsQueryBuilder.BuildPrefixAndQuery(rawInput);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!@#$%^&*()")]
    public void FtsQueryBuilder_ShouldReturnNull_ForEmptyOrInvalidInput(string? rawInput)
    {
        // Act
        var result = FtsQueryBuilder.BuildPrefixAndQuery(rawInput);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ApplicationStatusExtensions_ShouldReturnCorrectLabelsAndBadges()
    {
        // Assert for each status in the enum
        foreach (ApplicationStatus status in Enum.GetValues<ApplicationStatus>())
        {
            var displayName = status.GetDisplayName();
            var badgeClass = status.GetBadgeClass();
            var stepIndex = status.GetStepIndex();

            Assert.False(string.IsNullOrWhiteSpace(displayName));
            Assert.False(string.IsNullOrWhiteSpace(badgeClass));
            Assert.InRange(stepIndex, 0, 3);
        }

        // Test specific mappings
        Assert.Equal("Applied", ApplicationStatus.Applied.GetDisplayName());
        Assert.Equal("Interview Scheduled", ApplicationStatus.Scheduled.GetDisplayName());
        Assert.Equal(0, ApplicationStatus.Applied.GetStepIndex());
        Assert.Equal(1, ApplicationStatus.Screened.GetStepIndex());
        Assert.Equal(2, ApplicationStatus.Scheduled.GetStepIndex());
        Assert.Equal(3, ApplicationStatus.Offered.GetStepIndex());
        Assert.Equal(3, ApplicationStatus.Rejected.GetStepIndex());
    }

    [Fact]
    public void JobListItem_IsUrgent_ShouldBeTrue_WhenDeadlineWithin48Hours()
    {
        // Arrange
        var urgentJob = new JobListItemViewModel
        {
            Deadline = DateTimeOffset.UtcNow.AddHours(24)
        };

        var futureJob = new JobListItemViewModel
        {
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        };

        // Assert
        Assert.True(urgentJob.IsUrgent);
        Assert.False(futureJob.IsUrgent);
    }
}
