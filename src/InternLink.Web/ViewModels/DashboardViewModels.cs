namespace InternLink.Web.ViewModels;

// Property names must match the column aliases in DashboardRepository SQL (EF SqlQueryRaw mapping).
public class StudentDashboardViewModel
{
    public int ApplicationsCount { get; set; }
    public int FinalizedResumesCount { get; set; }
    public int VerifiedSkillsCount { get; set; }
}

public class CompanyDashboardViewModel
{
    public int OpenJobsCount { get; set; }
    public int TotalApplicantsCount { get; set; }
}

public class AdminDashboardViewModel
{
    public int PendingCompaniesCount { get; set; }
    public int PendingJobsCount { get; set; }
}

public class CounselorDashboardViewModel
{
    public int StudentCount { get; set; }
}
