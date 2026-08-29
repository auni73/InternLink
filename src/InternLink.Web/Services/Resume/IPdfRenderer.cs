using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Resume;

public interface IPdfRenderer
{
    byte[] RenderResumePdf(ResumeDataDto data);
    byte[] RenderCoverLetterPdf(string applicantName, string jobTitle, string companyName, string coverLetterText);
}
