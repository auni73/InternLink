using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Resume;

public class QuestPdfResumeRenderer : IPdfRenderer
{
    private static readonly string PrimaryColor = "#0F6B5C"; // InternLink Deep Teal
    private static readonly string TextDark = "#1E293B";     // Slate 800
    private static readonly string TextMuted = "#64748B";    // Slate 500
    private static readonly string BorderColor = "#E2E8F0";  // Slate 200

    static QuestPdfResumeRenderer()
    {
        // Set Community license for QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] RenderResumePdf(ResumeDataDto data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36); // ~0.5 inch margins
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextDark).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, data.PersonalInfo));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] RenderCoverLetterPdf(string applicantName, string jobTitle, string companyName, string coverLetterText)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(TextDark).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text(applicantName).FontSize(18).Bold().FontColor(PrimaryColor);
                    col.Item().PaddingTop(4).Text($"Application for: {jobTitle} at {companyName}").FontSize(11).FontColor(TextMuted);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(BorderColor);
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Item().Text($"Date: {DateTime.UtcNow:MMMM dd, yyyy}").FontSize(10).FontColor(TextMuted);
                    col.Item().PaddingTop(16).Text($"Dear Hiring Manager at {companyName},").FontSize(11).Bold();
                    
                    var paragraphs = coverLetterText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in paragraphs)
                    {
                        col.Item().PaddingTop(10).Text(p.Trim()).FontSize(10.5f).LineHeight(1.4f);
                    }

                    col.Item().PaddingTop(24).Text("Sincerely,").FontSize(11);
                    col.Item().PaddingTop(8).Text(applicantName).FontSize(11).Bold();
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, PersonalInfoStepDto info)
    {
        container.Column(col =>
        {
            col.Item().Text(string.IsNullOrWhiteSpace(info.FullName) ? "Curriculum Vitae" : info.FullName)
                .FontSize(22).Bold().FontColor(PrimaryColor);

            col.Item().PaddingTop(4).Row(row =>
            {
                var contacts = new List<string>();
                if (!string.IsNullOrWhiteSpace(info.Email)) contacts.Add(info.Email);
                if (!string.IsNullOrWhiteSpace(info.Phone)) contacts.Add(info.Phone);
                if (!string.IsNullOrWhiteSpace(info.Location)) contacts.Add(info.Location);

                row.RelativeItem().Text(string.Join("  •  ", contacts)).FontSize(9).FontColor(TextMuted);
            });

            var links = new List<string>();
            if (!string.IsNullOrWhiteSpace(info.LinkedIn)) links.Add($"LinkedIn: {info.LinkedIn}");
            if (!string.IsNullOrWhiteSpace(info.GitHub)) links.Add($"GitHub: {info.GitHub}");
            if (!string.IsNullOrWhiteSpace(info.Portfolio)) links.Add($"Portfolio: {info.Portfolio}");

            if (links.Count > 0)
            {
                col.Item().PaddingTop(2).Text(string.Join("  •  ", links)).FontSize(8.5f).FontColor(PrimaryColor);
            }

            col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(PrimaryColor);
        });
    }

    private static void ComposeContent(IContainer container, ResumeDataDto data)
    {
        container.PaddingTop(12).Column(col =>
        {
            // Professional Summary
            if (!string.IsNullOrWhiteSpace(data.PersonalInfo.Summary))
            {
                ComposeSectionHeader(col, "PROFESSIONAL SUMMARY");
                col.Item().PaddingTop(4).Text(data.PersonalInfo.Summary).FontSize(9.5f).LineHeight(1.35f);
            }

            // Experience
            if (data.Experience != null && data.Experience.Count > 0)
            {
                col.Item().PaddingTop(10);
                ComposeSectionHeader(col, "EXPERIENCE");

                foreach (var exp in data.Experience)
                {
                    col.Item().PaddingTop(6).Column(itemCol =>
                    {
                        itemCol.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span(exp.Role).Bold().FontSize(10);
                                if (!string.IsNullOrWhiteSpace(exp.Company))
                                {
                                    t.Span($" — {exp.Company}").FontSize(10).FontColor(PrimaryColor);
                                }
                            });

                            var dates = $"{exp.StartDate} - {(exp.IsCurrent ? "Present" : exp.EndDate)}";
                            r.AutoItem().Text(dates).FontSize(8.5f).FontColor(TextMuted);
                        });

                        if (!string.IsNullOrWhiteSpace(exp.Location))
                        {
                            itemCol.Item().Text(exp.Location).FontSize(8.5f).FontColor(TextMuted);
                        }

                        if (!string.IsNullOrWhiteSpace(exp.Description))
                        {
                            itemCol.Item().PaddingTop(2).Text(exp.Description).FontSize(9).LineHeight(1.3f);
                        }

                        if (!string.IsNullOrWhiteSpace(exp.Highlights))
                        {
                            var bullets = exp.Highlights.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var b in bullets)
                            {
                                itemCol.Item().PaddingTop(1).Row(br =>
                                {
                                    br.ConstantItem(12).Text("•").FontSize(9).FontColor(PrimaryColor);
                                    br.RelativeItem().Text(b.Trim().TrimStart('-', '*', ' ')).FontSize(9).LineHeight(1.25f);
                                });
                            }
                        }
                    });
                }
            }

            // Education
            if (data.Education != null && data.Education.Count > 0)
            {
                col.Item().PaddingTop(10);
                ComposeSectionHeader(col, "EDUCATION");

                foreach (var edu in data.Education)
                {
                    col.Item().PaddingTop(6).Column(itemCol =>
                    {
                        itemCol.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span(edu.Degree).Bold().FontSize(10);
                                if (!string.IsNullOrWhiteSpace(edu.FieldOfStudy))
                                {
                                    t.Span($" in {edu.FieldOfStudy}").FontSize(10);
                                }
                                if (!string.IsNullOrWhiteSpace(edu.Institution))
                                {
                                    t.Span($" — {edu.Institution}").FontSize(9.5f).FontColor(PrimaryColor);
                                }
                            });

                            var dates = $"{edu.StartDate} - {(edu.IsCurrent ? "Present" : edu.EndDate)}";
                            r.AutoItem().Text(dates).FontSize(8.5f).FontColor(TextMuted);
                        });

                        if (!string.IsNullOrWhiteSpace(edu.Gpa))
                        {
                            itemCol.Item().Text($"CGPA: {edu.Gpa}").FontSize(8.5f).FontColor(TextMuted);
                        }

                        if (!string.IsNullOrWhiteSpace(edu.Highlights))
                        {
                            itemCol.Item().PaddingTop(2).Text(edu.Highlights).FontSize(9).LineHeight(1.25f);
                        }
                    });
                }
            }

            // Skills
            if (data.Skills != null && data.Skills.Count > 0)
            {
                col.Item().PaddingTop(10);
                ComposeSectionHeader(col, "SKILLS & COMPETENCIES");

                col.Item().PaddingTop(6).Row(r =>
                {
                    var skillTexts = data.Skills.Select(s => $"{s.SkillName} (Level {s.ProficiencyLevel}/5)");
                    r.RelativeItem().Text(string.Join("  •  ", skillTexts)).FontSize(9).LineHeight(1.4f);
                });
            }

            // Projects
            if (data.Projects != null && data.Projects.Count > 0)
            {
                col.Item().PaddingTop(10);
                ComposeSectionHeader(col, "PROJECTS");

                foreach (var proj in data.Projects)
                {
                    col.Item().PaddingTop(6).Column(itemCol =>
                    {
                        itemCol.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span(proj.Title).Bold().FontSize(10);
                                if (!string.IsNullOrWhiteSpace(proj.TechStack))
                                {
                                    t.Span($" ({proj.TechStack})").FontSize(8.5f).FontColor(PrimaryColor);
                                }
                            });

                            if (!string.IsNullOrWhiteSpace(proj.Link))
                            {
                                r.AutoItem().Text(proj.Link).FontSize(8.5f).FontColor(PrimaryColor);
                            }
                        });

                        if (!string.IsNullOrWhiteSpace(proj.Description))
                        {
                            itemCol.Item().PaddingTop(2).Text(proj.Description).FontSize(9).LineHeight(1.3f);
                        }
                    });
                }
            }
        });
    }

    private static void ComposeSectionHeader(ColumnDescriptor col, string title)
    {
        col.Item().Column(c =>
        {
            c.Item().Text(title).FontSize(11).Bold().FontColor(PrimaryColor);
            c.Item().PaddingTop(2).LineHorizontal(1).LineColor(BorderColor);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("Generated via InternLink Career Portal").FontSize(8).FontColor(TextMuted);
            row.AutoItem().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
    }
}
