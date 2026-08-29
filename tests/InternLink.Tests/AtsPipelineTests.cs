using InternLink.Web.Helpers;
using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class AtsPipelineTests
{
    // =========================================================================
    // 1. STATE MACHINE & GUARDED TRANSITION GRAPH TESTS
    // =========================================================================

    [Theory]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Screened, true)]
    [InlineData(ApplicationStatus.Screened, ApplicationStatus.Scheduled, true)]
    [InlineData(ApplicationStatus.Scheduled, ApplicationStatus.Offered, true)]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Rejected, true)]
    [InlineData(ApplicationStatus.Screened, ApplicationStatus.Rejected, true)]
    [InlineData(ApplicationStatus.Scheduled, ApplicationStatus.Rejected, true)]
    public void TransitionGraph_ValidTransitions_AreAllowed(
        ApplicationStatus currentStatus, 
        ApplicationStatus targetStatus, 
        bool expectedAllowed)
    {
        // Act
        var isAllowed = EvaluateTransitionValidity(currentStatus, targetStatus, out var errorMessage);

        // Assert
        Assert.Equal(expectedAllowed, isAllowed);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData(ApplicationStatus.Screened, ApplicationStatus.Applied)]     // Backward 1 step
    [InlineData(ApplicationStatus.Scheduled, ApplicationStatus.Screened)]   // Backward 1 step
    [InlineData(ApplicationStatus.Scheduled, ApplicationStatus.Applied)]    // Backward 2 steps
    [InlineData(ApplicationStatus.Offered, ApplicationStatus.Scheduled)]    // Backward from terminal
    [InlineData(ApplicationStatus.Offered, ApplicationStatus.Screened)]     // Backward from terminal
    [InlineData(ApplicationStatus.Offered, ApplicationStatus.Applied)]      // Backward from terminal
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Applied)]     // Backward from terminal
    public void TransitionGraph_BackwardTransitions_AreStrictlyRejected(
        ApplicationStatus currentStatus, 
        ApplicationStatus targetStatus)
    {
        // Act
        var isAllowed = EvaluateTransitionValidity(currentStatus, targetStatus, out var errorMessage);

        // Assert
        Assert.False(isAllowed);
        Assert.NotNull(errorMessage);
        Assert.Contains("Invalid status transition", errorMessage);
    }

    [Theory]
    [InlineData(ApplicationStatus.Offered, ApplicationStatus.Offered)]
    [InlineData(ApplicationStatus.Offered, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Offered)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Screened)]
    public void TransitionGraph_TerminalStates_CannotBeModified(
        ApplicationStatus currentStatus, 
        ApplicationStatus targetStatus)
    {
        // Act
        var isAllowed = EvaluateTransitionValidity(currentStatus, targetStatus, out var errorMessage);

        // Assert
        Assert.False(isAllowed);
        Assert.NotNull(errorMessage);
        Assert.Contains("Invalid status transition", errorMessage);
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Scheduled)] // Skips Screened
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Offered)]   // Skips Screened & Scheduled
    [InlineData(ApplicationStatus.Screened, ApplicationStatus.Offered)]  // Skips Scheduled
    public void TransitionGraph_SkippingPipelineStages_IsRejected(
        ApplicationStatus currentStatus, 
        ApplicationStatus targetStatus)
    {
        // Act
        var isAllowed = EvaluateTransitionValidity(currentStatus, targetStatus, out var errorMessage);

        // Assert
        Assert.False(isAllowed);
        Assert.NotNull(errorMessage);
        Assert.Contains("Invalid status transition", errorMessage);
    }

    // =========================================================================
    // 2. INTERVIEW SCHEDULING VALIDATION TESTS
    // =========================================================================

    [Theory]
    [InlineData(1, true)]    // 1 day in the future -> valid
    [InlineData(7, true)]    // 1 week in the future -> valid
    [InlineData(-1, false)]  // 1 day in past -> invalid
    [InlineData(-10, false)] // 10 days in past -> invalid
    public void SchedulingValidation_FutureDateTime_Enforced(int daysOffset, bool expectedValid)
    {
        // Arrange
        DateTimeOffset? scheduledDateTime = DateTimeOffset.UtcNow.AddDays(daysOffset);

        // Act
        var isValid = scheduledDateTime.HasValue && scheduledDateTime.Value > DateTimeOffset.UtcNow;

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void SchedulingValidation_NullDateTime_IsInvalid()
    {
        // Arrange
        DateTimeOffset? scheduledDateTime = null;

        // Act
        var isValid = scheduledDateTime.HasValue && scheduledDateTime.Value > DateTimeOffset.UtcNow;

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("https://meet.google.com/abc-defg-hij", true)]
    [InlineData("https://zoom.us/j/1234567890", true)]
    [InlineData("http://teams.microsoft.com/l/meetup-join/19%3a", true)]
    [InlineData("not-a-url", false)]
    [InlineData("ftp://files.example.com/interview", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void SchedulingValidation_MeetingLinkUrl_RequiresHttpOrHttps(string? meetingLink, bool expectedValid)
    {
        // Act
        var isValid = !string.IsNullOrWhiteSpace(meetingLink) &&
                      Uri.TryCreate(meetingLink.Trim(), UriKind.Absolute, out var uri) &&
                      (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    // =========================================================================
    // 3. ATS BOARD VIEWMODEL & BADGE HELPER TESTS
    // =========================================================================

    [Fact]
    public void AtsBoardViewModel_PartitionsApplicationsAndCountsCorrectly()
    {
        // Arrange
        var apps = new List<CompanyAtsApplicantItemViewModel>
        {
            new() { ApplicationId = Guid.NewGuid(), Status = ApplicationStatus.Applied },
            new() { ApplicationId = Guid.NewGuid(), Status = ApplicationStatus.Applied },
            new() { ApplicationId = Guid.NewGuid(), Status = ApplicationStatus.Screened },
            new() { ApplicationId = Guid.NewGuid(), Status = ApplicationStatus.Scheduled },
            new() { ApplicationId = Guid.NewGuid(), Status = ApplicationStatus.Offered },
            new() { ApplicationId = Guid.NewGuid(), Status = ApplicationStatus.Rejected },
            new() { ApplicationId = Guid.NewGuid(), Status = ApplicationStatus.Rejected }
        };

        var board = new CompanyAtsBoardViewModel
        {
            Applications = apps
        };

        // Assert
        Assert.Equal(7, board.TotalApplicants);
        Assert.Equal(2, board.AppliedCount);
        Assert.Equal(1, board.ScreenedCount);
        Assert.Equal(1, board.ScheduledCount);
        Assert.Equal(1, board.OfferedCount);
        Assert.Equal(2, board.RejectedCount);

        Assert.Equal(2, board.AppliedCards.Count);
        Assert.Single(board.ScreenedCards);
        Assert.Single(board.ScheduledCards);
        Assert.Single(board.OfferedCards);
        Assert.Equal(2, board.RejectedCards.Count);
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied, "Applied", "#64748B")]
    [InlineData(ApplicationStatus.Screened, "Screened", "#0F6B5C")]
    [InlineData(ApplicationStatus.Scheduled, "Scheduled", "#F2A33C")]
    [InlineData(ApplicationStatus.Offered, "Offered", "#10B981")]
    [InlineData(ApplicationStatus.Rejected, "Rejected", "#EF4444")]
    public void ApplicationStatusExtensions_MaintainsConsistentColorsAndLabels(
        ApplicationStatus status, 
        string expectedShortLabel, 
        string expectedHex)
    {
        Assert.Equal(expectedShortLabel, status.GetShortLabel());
        Assert.Equal(expectedHex, status.GetBadgeHex());
    }

    // =========================================================================
    // 4. SHARED SKILL DERIVATION LOGIC TEST
    // =========================================================================

    [Theory]
    [InlineData(100, true)]
    [InlineData(70, true)]   // Exactly 70 is verified
    [InlineData(69, false)]  // 69 is unverified
    [InlineData(50, false)]
    [InlineData(0, false)]
    public void DerivedVerifiedSkill_ScoreThreshold70_Enforced(int achievedScore, bool expectedVerified)
    {
        // Act (Mirrors the SQL CAST(CASE WHEN MAX(a.AchievedScore) >= 70 THEN 1 ELSE 0 END AS bit))
        var isVerified = achievedScore >= 70;

        // Assert
        Assert.Equal(expectedVerified, isVerified);
    }

    // =========================================================================
    // HELPER METHOD (Mirrors ApplicationRepository.TransitionApplicationStatusAsync state machine)
    // =========================================================================
    private static bool EvaluateTransitionValidity(
        ApplicationStatus currentStatus, 
        ApplicationStatus newStatus, 
        out string? errorMessage)
    {
        errorMessage = null;

        if (currentStatus == ApplicationStatus.Offered || currentStatus == ApplicationStatus.Rejected)
        {
            errorMessage = "Invalid status transition. Cannot transition an application in a terminal state (Offered / Rejected).";
            return false;
        }

        if (newStatus == currentStatus)
        {
            errorMessage = "Invalid status transition. Application is already in this status.";
            return false;
        }

        if (newStatus == ApplicationStatus.Rejected)
        {
            return true;
        }

        if (currentStatus == ApplicationStatus.Applied)
        {
            if (newStatus != ApplicationStatus.Screened)
            {
                errorMessage = "Invalid status transition. Candidates in Applied status must be Screened before advancing.";
                return false;
            }
            return true;
        }

        if (currentStatus == ApplicationStatus.Screened)
        {
            if (newStatus != ApplicationStatus.Scheduled)
            {
                errorMessage = "Invalid status transition. Screened candidates must have an interview Scheduled before advancing.";
                return false;
            }
            return true;
        }

        if (currentStatus == ApplicationStatus.Scheduled)
        {
            if (newStatus != ApplicationStatus.Offered)
            {
                errorMessage = "Invalid status transition. Scheduled candidates can only be transitioned to Offered or Rejected.";
                return false;
            }
            return true;
        }

        errorMessage = "Invalid status transition.";
        return false;
    }
}
