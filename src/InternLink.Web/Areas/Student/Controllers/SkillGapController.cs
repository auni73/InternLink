using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.SkillGap;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

public class SkillGapController : StudentControllerBase
{
    private readonly ISkillGapService _skillGap;
    private readonly IJobRepository _jobs;

    public SkillGapController(ISkillGapService skillGap, IJobRepository jobs)
    {
        _skillGap = skillGap;
        _jobs = jobs;
    }

    /// <summary>Returns the shared panel as markup, so the student and company views cannot drift.</summary>
    [HttpGet]
    [Route("Student/Jobs/{jobId:guid}/SkillGap")]
    public async Task<IActionResult> Index(Guid jobId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var job = await _jobs.GetApprovedJobDetailAsync(jobId, studentId.Value, ct);
        if (job is null)
        {
            return NotFound();
        }

        var result = await _skillGap.AnalyzeAsync(studentId.Value, jobId, SkillGapPerspective.Student, ct);
        result.JobTitle = job.Title;

        return PartialView("_SkillGapPanel", result);
    }
}
