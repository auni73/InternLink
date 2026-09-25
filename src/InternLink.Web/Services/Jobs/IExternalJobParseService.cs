using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Jobs;

public interface IExternalJobParseService
{
    Task<ParseExternalJobResponseDto> ParseCircularAsync(
        string rawContent, 
        Guid adminUserId, 
        string? sourceName = null, 
        string? sourceUrl = null, 
        CancellationToken ct = default);
}
