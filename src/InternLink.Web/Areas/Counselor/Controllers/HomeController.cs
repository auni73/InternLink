using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Areas.Counselor.Controllers;

public class HomeController : CounselorControllerBase
{
    public IActionResult Index()
    {
        return View();
    }
}
