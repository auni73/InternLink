using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Areas.Company.Controllers;

[Area("Company")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
