using System.Text.Json;
using System.Text.Json.Nodes;
using InternLink.Web.Services.Resume;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class ResumeServiceTests
{
    [Fact]
    public void QuestPdfRenderer_ShouldGenerateNonEmptyPdfBytes_ForCompleteResumeData()
    {
        // Arrange
        var renderer = new QuestPdfResumeRenderer();
        var data = new ResumeDataDto
        {
            PersonalInfo = new PersonalInfoStepDto
            {
                FullName = "Alice Johnson",
                Email = "alice.johnson@example.com",
                Phone = "+880 1800 000000",
                Location = "Dhaka, Bangladesh",
                LinkedIn = "linkedin.com/in/alicej",
                GitHub = "github.com/alicej",
                Summary = "Motivated Software Engineering student passionate about backend distributed systems."
            },
            Education = new List<EducationEntryDto>
            {
                new EducationEntryDto
                {
                    Institution = "Ahsanullah University of Science and Technology",
                    Degree = "B.Sc. in Computer Science & Engineering",
                    StartDate = "2021",
                    EndDate = "2025",
                    Gpa = "3.92",
                    Highlights = "Top 5% in department, Academic Excellence Award"
                }
            },
            Experience = new List<ExperienceEntryDto>
            {
                new ExperienceEntryDto
                {
                    Company = "DataSoft Systems",
                    Role = "Backend Engineering Intern",
                    StartDate = "Jun 2024",
                    EndDate = "Aug 2024",
                    Description = "Implemented high-throughput data processing pipelines using ASP.NET Core and SQL Server.",
                    Highlights = "Reduced query latency by 40%\nDesigned automated integration test suites"
                }
            },
            Skills = new List<SkillEntryDto>
            {
                new SkillEntryDto { SkillId = Guid.NewGuid(), SkillName = "C# / .NET", ProficiencyLevel = 5 },
                new SkillEntryDto { SkillId = Guid.NewGuid(), SkillName = "SQL Server", ProficiencyLevel = 4 },
                new SkillEntryDto { SkillId = Guid.NewGuid(), SkillName = "Docker", ProficiencyLevel = 3 }
            },
            Projects = new List<ProjectEntryDto>
            {
                new ProjectEntryDto
                {
                    Title = "InternLink Career Portal",
                    TechStack = "ASP.NET Core, SQL Server, Qdrant",
                    Description = "AI-powered university internship portal with vector semantic search."
                }
            }
        };

        // Act
        var pdfBytes = renderer.RenderResumePdf(data);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000); // Valid PDF header and content
        // Standard PDF signature is %PDF-
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }

    [Fact]
    public void QuestPdfRenderer_ShouldGenerateNonEmptyPdfBytes_ForCoverLetter()
    {
        // Arrange
        var renderer = new QuestPdfResumeRenderer();

        // Act
        var pdfBytes = renderer.RenderCoverLetterPdf(
            applicantName: "Alice Johnson",
            jobTitle: "Software Engineering Intern",
            companyName: "Google",
            coverLetterText: "I am excited to apply for the Software Engineering Internship.\n\nMy technical foundation in C# and systems engineering aligns directly with your team's mission.");

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }

    [Fact]
    public void StepMerging_ShouldPreserveExistingKeys_WhenUpdatingOneStep()
    {
        // Arrange
        var initialJson = "{\"personalInfo\":{\"fullName\":\"Alice Johnson\",\"email\":\"alice@test.com\"},\"education\":[{\"institution\":\"AUST\"}]}";
        var rootNode = JsonNode.Parse(initialJson)!.AsObject();

        var newExperienceJson = "[{\"company\":\"TechCorp\",\"role\":\"Intern\"}]";
        var incomingNode = JsonNode.Parse(newExperienceJson);

        // Act
        rootNode["experience"] = incomingNode;
        var resultJson = rootNode.ToJsonString();

        // Assert
        var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.TryGetProperty("personalInfo", out var pi));
        Assert.Equal("Alice Johnson", pi.GetProperty("fullName").GetString());
        Assert.True(doc.RootElement.TryGetProperty("education", out var edu));
        Assert.Equal(1, edu.GetArrayLength());
        Assert.True(doc.RootElement.TryGetProperty("experience", out var exp));
        Assert.Equal(1, exp.GetArrayLength());
    }
}
