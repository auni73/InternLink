using System.ComponentModel.DataAnnotations;
using InternLink.Web.Services.Storage;
using InternLink.Web.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InternLink.Tests;

public class Prompt11Tests
{
    [Theory]
    [InlineData("3.85", true)]
    [InlineData("0.00", true)]
    [InlineData("4.00", true)]
    [InlineData("-0.01", false)]
    [InlineData("4.01", false)]
    [InlineData("5.00", false)]
    public void StudentProfileViewModel_CgpaValidation_EnforcesRangeBetweenZeroAndFour(string cgpaStr, bool isValidExpected)
    {
        // Arrange
        var model = new StudentProfileViewModel
        {
            FirstName = "John",
            LastName = "Doe",
            Department = "Computer Science & Engineering",
            CGPA = decimal.Parse(cgpaStr, System.Globalization.CultureInfo.InvariantCulture)
        };

        var context = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        // Assert
        Assert.Equal(isValidExpected, isValid);
        if (!isValidExpected)
        {
            Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(StudentProfileViewModel.CGPA)));
        }
    }

    [Fact]
    public void StudentProfileViewModel_MissingRequiredFields_FailsValidation()
    {
        // Arrange
        var model = new StudentProfileViewModel
        {
            FirstName = "",
            LastName = "",
            Department = "",
            CGPA = 3.50m
        };

        var context = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(StudentProfileViewModel.FirstName)));
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(StudentProfileViewModel.LastName)));
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(StudentProfileViewModel.Department)));
    }

    [Fact]
    public async Task DiskFileStorage_ShouldSaveAndReadStream_Successfully()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), "InternLinkTestStorage_" + Guid.NewGuid());
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Storage:ResumeRoot", tempFolder }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var storage = new DiskFileStorage(config, NullLogger<DiskFileStorage>.Instance);

        var studentId = Guid.NewGuid();
        var resumeId = Guid.NewGuid();
        var sampleBytes = "%PDF-1.4 Mock PDF Content For Testing"u8.ToArray();

        try
        {
            // Act
            var filePath = await storage.SaveResumePdfAsync(studentId, resumeId, sampleBytes);

            // Assert
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));
            Assert.True(storage.Exists(filePath));

            await using var stream = await storage.OpenReadAsync(filePath);
            Assert.NotNull(stream);

            using var memStream = new MemoryStream();
            await stream.CopyToAsync(memStream);
            Assert.Equal(sampleBytes, memStream.ToArray());
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }
}
