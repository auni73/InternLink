using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Policy = "CompanyOnly")]
public abstract class CompanyControllerBase : Controller
{
    private const string CompanyIdItemKey = "CompanyId";

    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Domain tables key on Companies.Id, not the user id. Resolved once per request and cached.
    protected async Task<Guid?> GetCompanyIdAsync(CancellationToken ct = default)
    {
        if (HttpContext.Items.TryGetValue(CompanyIdItemKey, out var cached) && cached is Guid cachedId)
        {
            return cachedId;
        }

        var repository = HttpContext.RequestServices.GetRequiredService<ICompanyRepository>();
        var company = await repository.GetByUserIdAsync(CurrentUserId, ct);
        if (company is null)
        {
            return null;
        }

        HttpContext.Items[CompanyIdItemKey] = company.Id;
        return company.Id;
    }
}
