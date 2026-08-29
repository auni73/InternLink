using System.ComponentModel.DataAnnotations;
using InternLink.Web.Services;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class CounselorAdvisingTests
{
    private readonly IMarkdownService _markdownService = new MarkdownService();

    [Fact]
    public void MarkdownService_ScriptTag_RendersInertAsEscapedText_NeverExecutable()
    {
        // Arrange
        const string inputWithScript = "Here is an advising note with <script>alert('xss')</script> payload.";

        // Act
        var renderedHtml = _markdownService.RenderToHtml(inputWithScript);

        // Assert
        Assert.DoesNotContain("<script>", renderedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("</script>", renderedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert('xss')&lt;/script&gt;", renderedHtml);
    }

    [Fact]
    public void MarkdownService_ImgOnErrorTag_RendersInertAsEscapedText()
    {
        // Arrange
        const string inputWithImg = "Student resume review: <img src=x onerror=alert(1)> Check work.";

        // Act
        var renderedHtml = _markdownService.RenderToHtml(inputWithImg);

        // Assert
        Assert.DoesNotContain("<img", renderedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", renderedHtml);
    }

    [Fact]
    public void MarkdownService_ValidMarkdownSyntax_RendersExpectedSemanticHtml()
    {
        // Arrange
        const string validMarkdown = @"## Action Items
- Improve **ASP.NET Core** project
- Practice *mock interview* questions";

        // Act
        var renderedHtml = _markdownService.RenderToHtml(validMarkdown);

        // Assert
        Assert.Contains("<h2>Action Items</h2>", renderedHtml);
        Assert.Contains("<strong>ASP.NET Core</strong>", renderedHtml);
        Assert.Contains("<em>mock interview</em>", renderedHtml);
        Assert.Contains("<li>", renderedHtml);
    }

    [Fact]
    public void MarkdownService_NullOrEmptyInput_ReturnsEmptyString()
    {
        // Act & Assert
        Assert.Equal(string.Empty, _markdownService.RenderToHtml(null));
        Assert.Equal(string.Empty, _markdownService.RenderToHtml(""));
        Assert.Equal(string.Empty, _markdownService.RenderToHtml("   "));
    }

    [Fact]
    public void CounselorFeedbackCreateViewModel_Validation_EnforcesMax5000Characters()
    {
        // Arrange: 5001 characters
        var modelOverLimit = new CounselorFeedbackCreateViewModel
        {
            StudentId = Guid.NewGuid(),
            MeetingDate = DateTimeOffset.UtcNow,
            NarrativeMarkdown = new string('A', 5001)
        };

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(modelOverLimit);

        // Act
        var isValid = Validator.TryValidateObject(modelOverLimit, context, validationResults, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(CounselorFeedbackCreateViewModel.NarrativeMarkdown)));
    }

    [Fact]
    public void CounselorFeedbackCreateViewModel_Validation_AllowsPastAndFutureDates()
    {
        // Arrange: past date (counselors log past meetings)
        var pastMeeting = new CounselorFeedbackCreateViewModel
        {
            StudentId = Guid.NewGuid(),
            MeetingDate = DateTimeOffset.UtcNow.AddDays(-7),
            NarrativeMarkdown = "Completed in-depth resume critique and SQL indexing discussion."
        };

        // Arrange: future date (scheduled session)
        var futureMeeting = new CounselorFeedbackCreateViewModel
        {
            StudentId = Guid.NewGuid(),
            MeetingDate = DateTimeOffset.UtcNow.AddDays(5),
            NarrativeMarkdown = "Scheduled mock interview."
        };

        var pastResults = new List<ValidationResult>();
        var futureResults = new List<ValidationResult>();

        // Act
        var isPastValid = Validator.TryValidateObject(pastMeeting, new ValidationContext(pastMeeting), pastResults, validateAllProperties: true);
        var isFutureValid = Validator.TryValidateObject(futureMeeting, new ValidationContext(futureMeeting), futureResults, validateAllProperties: true);

        // Assert
        Assert.True(isPastValid);
        Assert.True(isFutureValid);
    }

    [Theory]
    [InlineData(0, 15, 1, 1, false, false)]
    [InlineData(10, 15, 1, 1, false, false)]
    [InlineData(15, 15, 1, 1, false, false)]
    [InlineData(16, 15, 1, 2, false, true)]
    [InlineData(35, 15, 2, 3, true, true)]
    [InlineData(45, 15, 3, 3, true, false)]
    public void CounselorStudentDirectoryViewModel_PaginationCalculations_AccurateAcrossPages(
        int totalCount,
        int pageSize,
        int currentPage,
        int expectedTotalPages,
        bool expectedHasPrev,
        bool expectedHasNext)
    {
        // Arrange & Act
        var vm = new CounselorStudentDirectoryViewModel
        {
            TotalCount = totalCount,
            PageSize = pageSize,
            CurrentPage = currentPage
        };

        // Assert
        Assert.Equal(expectedTotalPages, vm.TotalPages);
        Assert.Equal(expectedHasPrev, vm.HasPreviousPage);
        Assert.Equal(expectedHasNext, vm.HasNextPage);
    }
}
