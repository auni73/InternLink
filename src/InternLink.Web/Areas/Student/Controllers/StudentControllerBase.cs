using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Policy = "StudentOnly")]
public abstract class StudentControllerBase : Controller
{
    private const string StudentIdItemKey = "StudentId";

    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Domain tables key on Students.Id, not the user id. Resolved once per request and cached.
    protected async Task<Guid?> GetStudentIdAsync(CancellationToken ct = default)
    {
        if (HttpContext.Items.TryGetValue(StudentIdItemKey, out var cached) && cached is Guid cachedId)
        {
            return cachedId;
        }

        var repository = HttpContext.RequestServices.GetRequiredService<IStudentRepository>();
        var student = await repository.GetByUserIdAsync(CurrentUserId, ct);
        if (student is null)
        {
            return null;
        }

        HttpContext.Items[StudentIdItemKey] = student.Id;
        return student.Id;
    }
}
