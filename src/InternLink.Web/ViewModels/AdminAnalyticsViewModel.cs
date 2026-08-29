namespace InternLink.Web.ViewModels;

public class AdminAnalyticsViewModel
{
    // Platform High-Level KPIs
    public int ActiveStudentCount { get; set; }
    public int ActiveCompanyCount { get; set; }
    public int OpenJobCount { get; set; }
    public int TotalApplicationsCount { get; set; }
    public int TotalInterviewsCount { get; set; }
    public int VerifiedSkillsEarnedCount { get; set; }

    // Pipeline Breakdown (Applied, Screened, Scheduled, Offered, Rejected)
    public Dictionary<string, int> ApplicationsByStatus { get; set; } = new()
    {
        { "Applied", 0 },
        { "Screened", 0 },
        { "Scheduled", 0 },
        { "Offered", 0 },
        { "Rejected", 0 }
    };

    // 7-Day Continuous Trend (Zero-filled via SQL CTE)
    public List<DailyApplicationMetric> NewApplicationsLast7Days { get; set; } = new();

    // Serialized JSON for <script type="application/json"> data island
    public string JsonPayload { get; set; } = "{}";
}

public class DailyApplicationMetric
{
    public string Date { get; set; } = string.Empty;
    public string FormattedDate { get; set; } = string.Empty;
    public int Count { get; set; }
}
