using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Areas.Student.Controllers;

public class HomeController : StudentControllerBase
{
    public IActionResult Index()
    {
        return View();
    }
}
