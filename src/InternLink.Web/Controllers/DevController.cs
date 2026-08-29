using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternLink.Web.Controllers;

[AllowAnonymous]
[Route("dev")]
public class DevController : Controller
{
    private readonly IWebHostEnvironment _env;

    public DevController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("style-guide")]
    public IActionResult StyleGuide()
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }
        return View();
    }

    // Dummy endpoint for verifying the api.js antiforgery flow from the style guide.
    [HttpPost("echo")]
    public IActionResult Echo([FromBody] EchoRequest? body)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }
        return Json(new { ok = true, received = body?.Message });
    }

    public sealed class EchoRequest
    {
        public string? Message { get; set; }
    }
}
