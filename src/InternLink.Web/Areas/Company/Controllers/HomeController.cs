using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Areas.Company.Controllers;

public class HomeController : CompanyControllerBase
{
    public IActionResult Index()
    {
        return View();
    }
}
