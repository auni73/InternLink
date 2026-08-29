using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Areas.Admin.Controllers;

public class HomeController : AdminControllerBase
{
    public IActionResult Index()
    {
        return View();
    }
}
