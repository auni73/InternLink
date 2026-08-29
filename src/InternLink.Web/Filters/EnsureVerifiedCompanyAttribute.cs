using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class EnsureVerifiedCompanyAttribute : TypeFilterAttribute
{
    public EnsureVerifiedCompanyAttribute() : base(typeof(EnsureVerifiedCompanyFilter))
    {
    }

    private class EnsureVerifiedCompanyFilter : IAsyncActionFilter
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly IModelMetadataProvider _modelMetadataProvider;

        public EnsureVerifiedCompanyFilter(ICompanyRepository companyRepository, IModelMetadataProvider modelMetadataProvider)
        {
            _companyRepository = companyRepository;
            _modelMetadataProvider = modelMetadataProvider;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                context.Result = new ChallengeResult();
                return;
            }

            var company = await _companyRepository.GetByUserIdAsync(userId, context.HttpContext.RequestAborted);
            if (company is null)
            {
                context.Result = new NotFoundObjectResult("Company profile not found.");
                return;
            }

            if (company.VerificationStatus != VerificationStatus.Verified)
            {
                var isJsonRequest = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                    context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json") ||
                                    (context.HttpContext.Request.ContentType?.Contains("application/json") ?? false);

                if (isJsonRequest)
                {
                    context.Result = new ObjectResult(new
                    {
                        success = false,
                        message = "Your company profile is pending administrator verification. Job posting modifications are disabled until verified.",
                        verificationStatus = company.VerificationStatus.ToString()
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    return;
                }

                var viewResult = new ViewResult
                {
                    ViewName = "CompanyNotVerified",
                    ViewData = new ViewDataDictionary(_modelMetadataProvider, context.ModelState)
                    {
                        Model = company
                    },
                    StatusCode = StatusCodes.Status403Forbidden
                };

                context.Result = viewResult;
                return;
            }

            await next();
        }
    }
}
