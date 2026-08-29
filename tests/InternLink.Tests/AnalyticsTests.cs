using System.Text.Json;
using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class AnalyticsTests
{
    [Fact]
    public void AdminAnalyticsViewModel_DefaultStatusDictionary_ContainsAllFiveStatuses()
    {
        var vm = new AdminAnalyticsViewModel();

        Assert.Equal(5, vm.ApplicationsByStatus.Count);
        Assert.True(vm.ApplicationsByStatus.ContainsKey("Applied"));
        Assert.True(vm.ApplicationsByStatus.ContainsKey("Screened"));
        Assert.True(vm.ApplicationsByStatus.ContainsKey("Scheduled"));
        Assert.True(vm.ApplicationsByStatus.ContainsKey("Offered"));
        Assert.True(vm.ApplicationsByStatus.ContainsKey("Rejected"));

        foreach (var kvp in vm.ApplicationsByStatus)
        {
            Assert.Equal(0, kvp.Value);
        }
    }

    [Fact]
    public void AdminAnalyticsViewModel_StatusColors_MatchApplicationStatusConstants()
    {
        // Assert that all enum names match the status dictionary keys
        var enumNames = Enum.GetNames<ApplicationStatus>();
        var vm = new AdminAnalyticsViewModel();

        foreach (var name in enumNames)
        {
            Assert.True(vm.ApplicationsByStatus.ContainsKey(name), $"Missing status key in analytics: {name}");
        }
    }

    [Fact]
    public void DailyApplicationMetric_FormattedDate_MatchesExpectedMonthDayFormat()
    {
        var date = new DateOnly(2026, 8, 29);
        var metric = new DailyApplicationMetric
        {
            Date = date.ToString("yyyy-MM-dd"),
            FormattedDate = date.ToString("MMM dd"),
            Count = 5
        };

        Assert.Equal("2026-08-29", metric.Date);
        Assert.Equal("Aug 29", metric.FormattedDate);
        Assert.Equal(5, metric.Count);
    }

    [Fact]
    public void JsonPayload_SerializesValidCamelCaseStructureForClientConsumption()
    {
        var vm = new AdminAnalyticsViewModel
        {
            ActiveStudentCount = 12,
            ActiveCompanyCount = 3,
            OpenJobCount = 5,
            TotalApplicationsCount = 18,
            TotalInterviewsCount = 4,
            VerifiedSkillsEarnedCount = 9,
            ApplicationsByStatus = new Dictionary<string, int>
            {
                { "Applied", 6 },
                { "Screened", 4 },
                { "Scheduled", 4 },
                { "Offered", 2 },
                { "Rejected", 2 }
            },
            NewApplicationsLast7Days = new List<DailyApplicationMetric>
            {
                new() { Date = "2026-08-23", FormattedDate = "Aug 23", Count = 1 },
                new() { Date = "2026-08-24", FormattedDate = "Aug 24", Count = 0 },
                new() { Date = "2026-08-25", FormattedDate = "Aug 25", Count = 3 },
                new() { Date = "2026-08-26", FormattedDate = "Aug 26", Count = 2 },
                new() { Date = "2026-08-27", FormattedDate = "Aug 27", Count = 5 },
                new() { Date = "2026-08-28", FormattedDate = "Aug 28", Count = 4 },
                new() { Date = "2026-08-29", FormattedDate = "Aug 29", Count = 3 }
            }
        };

        vm.JsonPayload = JsonSerializer.Serialize(new
        {
            kpis = new
            {
                activeStudents = vm.ActiveStudentCount,
                activeCompanies = vm.ActiveCompanyCount,
                openJobs = vm.OpenJobCount,
                totalApplications = vm.TotalApplicationsCount,
                totalInterviews = vm.TotalInterviewsCount,
                verifiedSkills = vm.VerifiedSkillsEarnedCount
            },
            statusBreakdown = vm.ApplicationsByStatus,
            dailyTrend = vm.NewApplicationsLast7Days.Select(d => new
            {
                date = d.Date,
                formattedDate = d.FormattedDate,
                count = d.Count
            })
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.False(string.IsNullOrWhiteSpace(vm.JsonPayload));

        using var doc = JsonDocument.Parse(vm.JsonPayload);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("kpis", out var kpis));
        Assert.Equal(12, kpis.GetProperty("activeStudents").GetInt32());
        Assert.Equal(3, kpis.GetProperty("activeCompanies").GetInt32());
        Assert.Equal(5, kpis.GetProperty("openJobs").GetInt32());
        Assert.Equal(18, kpis.GetProperty("totalApplications").GetInt32());
        Assert.Equal(4, kpis.GetProperty("totalInterviews").GetInt32());
        Assert.Equal(9, kpis.GetProperty("verifiedSkills").GetInt32());

        Assert.True(root.TryGetProperty("statusBreakdown", out var statusBreakdown));
        Assert.Equal(6, statusBreakdown.GetProperty("Applied").GetInt32());

        Assert.True(root.TryGetProperty("dailyTrend", out var dailyTrend));
        Assert.Equal(7, dailyTrend.GetArrayLength());
        Assert.Equal("Aug 23", dailyTrend[0].GetProperty("formattedDate").GetString());
        Assert.Equal(1, dailyTrend[0].GetProperty("count").GetInt32());
    }
}
