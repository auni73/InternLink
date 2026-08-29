using System.Text.Json;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class ResumeBuilderSerializationTests
{
    // The resume builder hydrates its education/experience/skills rows from a JSON literal rendered
    // into the page. The wizard reads camelCase keys, and anything it cannot find renders as empty and
    // is then persisted by the per-step autosave, destroying stored data. Serializing with the default
    // options emits PascalCase and reintroduces exactly that data loss, so pin the casing here.
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("personalInfo")]
    [InlineData("education")]
    [InlineData("experience")]
    [InlineData("skills")]
    [InlineData("projects")]
    public void ResumeDataSerializesWithCamelCaseKeys_TheCasingTheWizardReads(string expectedKey)
    {
        var json = JsonSerializer.Serialize(BuildPopulatedResume(), WebOptions);

        Assert.Contains($"\"{expectedKey}\"", json);
    }

    [Fact]
    public void DefaultOptionsWouldEmitPascalCase_WhichIsWhyOptionsMustBePassed()
    {
        var json = JsonSerializer.Serialize(BuildPopulatedResume());

        Assert.Contains("\"Education\"", json);
        Assert.DoesNotContain("\"education\"", json);
    }

    [Fact]
    public void PopulatedCollectionsSurviveARoundTrip()
    {
        var json = JsonSerializer.Serialize(BuildPopulatedResume(), WebOptions);
        var restored = JsonSerializer.Deserialize<ResumeDataDto>(json, WebOptions);

        Assert.NotNull(restored);
        Assert.Single(restored!.Education);
        Assert.Single(restored.Experience);
        Assert.Single(restored.Projects);
        Assert.Equal(2, restored.Skills.Count);
        Assert.Equal("Tanvir Ahmed", restored.PersonalInfo.FullName);
        Assert.Equal("Local Software House", restored.Experience[0].Company);
    }

    private static ResumeDataDto BuildPopulatedResume() => new()
    {
        PersonalInfo = new PersonalInfoStepDto { FullName = "Tanvir Ahmed", Email = "student@internlink.test" },
        Education = [new EducationEntryDto { Institution = "AUST", Degree = "BSc", FieldOfStudy = "CSE" }],
        Experience = [new ExperienceEntryDto { Company = "Local Software House", Role = "Intern" }],
        Skills =
        [
            new SkillEntryDto { SkillName = "C#", ProficiencyLevel = 4 },
            new SkillEntryDto { SkillName = "SQL Server", ProficiencyLevel = 3 }
        ],
        Projects = [new ProjectEntryDto { Title = "Library System", TechStack = "C#, SQL Server" }]
    };
}
