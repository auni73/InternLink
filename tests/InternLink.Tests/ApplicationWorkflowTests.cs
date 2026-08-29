using InternLink.Web.Helpers;
using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class ApplicationWorkflowTests
{
    [Fact]
    public void DbExceptionMapper_NonSqlException_ReturnsFalse()
    {
        // Arrange
        var genericEx = new InvalidOperationException("Some generic error");

        // Act & Assert
        Assert.False(DbExceptionMapper.IsUniqueConstraintViolation(genericEx));
        Assert.False(DbExceptionMapper.IsForeignKeyOrCheckViolation(genericEx));
    }

    [Fact]
    public void JobDetailViewModel_DeadlineFormatted_RendersDaysRemainingCorrectly()
    {
        // Arrange
        var deadline = DateTimeOffset.UtcNow.AddDays(5);
        var model = new JobDetailViewModel
        {
            Deadline = deadline
        };

        // Act
        var formatted = model.DeadlineFormatted;

        // Assert
        Assert.Contains("Closes in", formatted);
        Assert.Contains("days", formatted);
    }

    [Theory]
    [InlineData(LocationType.Remote, "Remote")]
    [InlineData(LocationType.OnSite, "OnSite")]
    [InlineData(LocationType.Hybrid, "Hybrid")]
    public void LocationType_EnumValues_MapCorrectly(LocationType type, string expectedName)
    {
        Assert.Equal(expectedName, type.ToString());
    }

    [Fact]
    public void ApplicationStatus_StepIndex_EnforcesOrderedWorkflow()
    {
        // Verify workflow steps are ordered 0 -> 1 -> 2 -> 3
        Assert.Equal(0, ApplicationStatus.Applied.GetStepIndex());
        Assert.Equal(1, ApplicationStatus.Screened.GetStepIndex());
        Assert.Equal(2, ApplicationStatus.Scheduled.GetStepIndex());
        Assert.Equal(3, ApplicationStatus.Offered.GetStepIndex());
        Assert.Equal(3, ApplicationStatus.Rejected.GetStepIndex());
    }
}
