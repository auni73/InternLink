using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Areas.Counselor.Controllers;

[Area("Counselor")]
[Authorize(Policy = "CounselorOnly")]
public abstract class CounselorControllerBase : Controller
{
    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
