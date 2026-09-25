using System.Security.Claims;
using InternLink.Web.Areas.Student.Controllers;
using InternLink.Web.Helpers;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.Jobs;
using InternLink.Web.Services.Recommendation;
using InternLink.Web.Services.Resume;
using InternLink.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InternLink.Tests;

public class ExternalJobTests
{
    [Fact]
    public async Task ParseCircular_WhenGeminiFails_FallsBackToHeuristicExtraction()
    {
        // Arrange
        var mockGemini = new FailingGeminiClient();
        var mockSkills = new FakeSkillRepository(
        [
            new Skill { Id = Guid.NewGuid(), SkillName = "C#" },
            new Skill { Id = Guid.NewGuid(), SkillName = "React" },
            new Skill { Id = Guid.NewGuid(), SkillName = "Docker" }
        ]);

        var service = new ExternalJobParseService(
            mockGemini,
            mockSkills,
            NullLogger<ExternalJobParseService>.Instance);

        var rawCircular = @"Senior Backend Engineer
Awesome Tech Ltd.
We are looking for a remote C# and Docker specialist to join our expanding engineering unit.
Full-time remote work.";

        // Act
        var result = await service.ParseCircularAsync(rawCircular, Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Senior Backend Engineer", result.Title);
        Assert.Equal("Awesome Tech Ltd.", result.CompanyNameSnapshot);
        Assert.Equal(LocationType.Remote, result.LocationType);
        Assert.Contains("C#", result.SuggestedSkillNames);
        Assert.Contains("Docker", result.SuggestedSkillNames);
        Assert.DoesNotContain("React", result.SuggestedSkillNames);
    }

    [Fact]
    public async Task Apply_ToExternalJob_ReturnsBadRequest()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var jobRepo = new FakeJobRepoForExternalApply(new JobDetailViewModel
        {
            Id = jobId,
            Title = "External Software Engineer",
            CompanyName = "External Org",
            Source = JobSource.External,
            ExternalSourceName = "BDJobs",
            ExternalApplyUrl = "https://bdjobs.example.com/job/123"
        });

        var studentRepo = new FakeStudentRepoWithId(studentId);
        var controller = new JobsController(
            jobRepo,
            studentRepo,
            new StubApplicationRepository(),
            new FakeResumeRepository(),
            new StubResumeService(),
            new FakeFtsCapabilityService(),
            new StubRecommendationService(),
            NullLogger<JobsController>.Instance);

        // Setup HttpContext with student identity
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new FakeServiceProvider(studentRepo);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Student")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new ApplyJobRequestDto
        {
            ResumeId = Guid.NewGuid()
        };

        // Act
        var result = await controller.Apply(jobId, request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var errorProp = badRequest.Value.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        var errorMessage = errorProp.GetValue(badRequest.Value) as string;
        Assert.Contains("external", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers & Fakes ---

    private sealed class FailingGeminiClient : IGeminiClient
    {
        public Task<GeminiResponse> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            IntegrationFeature feature,
            Guid userId,
            bool jsonMode,
            CancellationToken ct = default)
        {
            throw new HttpRequestException("Gemini API rate limit or network error");
        }

        public Task<GeminiResponse> GenerateChatAsync(
            string systemPrompt,
            IReadOnlyList<ChatMessage> history,
            IntegrationFeature feature,
            Guid userId,
            CancellationToken ct = default)
        {
            throw new HttpRequestException("Gemini API rate limit or network error");
        }
    }

    private sealed class FakeSkillRepository : ISkillRepository
    {
        private readonly IReadOnlyList<Skill> _skills;
        public FakeSkillRepository(IReadOnlyList<Skill> skills) => _skills = skills;

        public Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_skills);
        public Task<Skill?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Skill>> GetSkillsByStudentIdAsync(Guid studentId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Skill>>([]);
    }

    private sealed class FakeJobRepoForExternalApply : StubJobRepository
    {
        private readonly JobDetailViewModel _detail;
        public FakeJobRepoForExternalApply(JobDetailViewModel detail) => _detail = detail;

        public override Task<JobDetailViewModel?> GetApprovedJobDetailAsync(Guid id, Guid? studentId, CancellationToken ct = default) =>
            Task.FromResult<JobDetailViewModel?>(_detail);
    }

    private sealed class FakeStudentRepoWithId : StubStudentRepository
    {
        private readonly Guid _studentId;
        public FakeStudentRepoWithId(Guid studentId) => _studentId = studentId;

        public override Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Student?>(new Student { Id = _studentId, UserId = Guid.NewGuid(), FirstName = "Test", LastName = "Student" });

        public override Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<Student?>(new Student { Id = _studentId, UserId = userId, FirstName = "Test", LastName = "Student" });
    }

    private sealed class FakeResumeRepository : IResumeRepository
    {
        public Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Resume>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Resume> CreateAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateDynamicJsonDataAsync(Guid id, string dynamicJsonData, DateTimeOffset lastModified, CancellationToken ct = default) => throw new NotSupportedException();
        public Task FinalizeAsync(Guid id, string documentPath, DateTimeOffset lastModified, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> GetFinalizedCountByStudentIdAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeFtsCapabilityService : IFtsCapabilityService
    {
        public Task<bool> IsFtsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class StubRecommendationService : IRecommendationService
    {
        public Task<RecommendationResultViewModel> GetRecommendedJobsAsync(Guid studentId, CancellationToken ct = default) =>
            Task.FromResult(new RecommendationResultViewModel());
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly IStudentRepository _studentRepo;
        public FakeServiceProvider(IStudentRepository studentRepo) => _studentRepo = studentRepo;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IStudentRepository))
            {
                return _studentRepo;
            }
            return null;
        }
    }
}
