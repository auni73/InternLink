using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Areas.Counselor.Controllers;

[Area("Counselor")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
