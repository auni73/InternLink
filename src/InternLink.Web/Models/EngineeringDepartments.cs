using System.Text.RegularExpressions;

namespace InternLink.Web.Models;

public record EngineeringProgram(
    string Code,
    string Name,
    string ShortName,
    string AustDigitCode,
    string IconClass,
    string ColorBadgeClass,
    string Description);

public static class EngineeringDepartments
{
    public const string CSE = "Computer Science & Engineering";
    public const string EEE = "Electrical & Electronic Engineering";
    public const string CE = "Civil Engineering";
    public const string ME = "Mechanical Engineering";
    public const string IPE = "Industrial & Production Engineering";
    public const string TE = "Textile Engineering";
    public const string General = "General Engineering";

    public static readonly IReadOnlyList<string> All = new[] { CSE, EEE, CE, ME, IPE, TE };

    public static readonly IReadOnlyList<EngineeringProgram> AllPrograms = new List<EngineeringProgram>
    {
        new("CSE", CSE, "CSE", "04", "bi-laptop", "bg-primary-subtle text-primary border-primary-subtle", "Software, Systems, AI & Networks"),
        new("EEE", EEE, "EEE", "03", "bi-lightning-charge", "bg-warning-subtle text-warning-emphasis border-warning-subtle", "Power, Electronics, Telecommunications & Embedded"),
        new("CE", CE, "Civil", "02", "bi-buildings", "bg-info-subtle text-info-emphasis border-info-subtle", "Structures, Geotechnical, Transportation & Water Resources"),
        new("ME", ME, "Mechanical", "06", "bi-gear-wide-connected", "bg-danger-subtle text-danger-emphasis border-danger-subtle", "Thermal, Solid Mechanics, Manufacturing & Robotics"),
        new("IPE", IPE, "IPE", "07", "bi-boxes", "bg-success-subtle text-success-emphasis border-success-subtle", "Supply Chain, Quality Engineering & Operations"),
        new("TE", TE, "Textile", "05", "bi-palette", "bg-purple-subtle text-purple border-purple-subtle", "Yarn, Fabric, Wet Processing & Apparel Merchandising")
    };

    public static readonly IReadOnlyDictionary<string, string> AustDigitCodeToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "Architecture",
        ["02"] = CE,
        ["03"] = EEE,
        ["04"] = CSE,
        ["05"] = TE,
        ["06"] = ME,
        ["07"] = IPE,
        ["08"] = "Business Administration"
    };

    private static readonly Regex AustIdPattern = new(
        @"^\s*(\d{2})[\.\-\/]?(\d{2})[\.\-\/]?(\d{2})[\.\-\/]?(\d{3,4})\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse AUST institutional student ID pattern (e.g. 21.01.04.100 or 21-01-04-100 or 210104100).
    /// Extracts the 3rd token (Department code).
    /// </summary>
    public static bool TryParseFromAustId(string? institutionalId, out string? departmentName, out string? departmentCode)
    {
        departmentName = null;
        departmentCode = null;

        if (string.IsNullOrWhiteSpace(institutionalId))
        {
            return false;
        }

        var match = AustIdPattern.Match(institutionalId.Trim());
        if (!match.Success)
        {
            return false;
        }

        var deptDigits = match.Groups[3].Value;
        if (AustDigitCodeToName.TryGetValue(deptDigits, out var matchedName))
        {
            departmentName = matchedName;
            departmentCode = AllPrograms.FirstOrDefault(p => p.Name == matchedName)?.Code ?? deptDigits;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes common department nicknames/abbreviations into their canonical name.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var trimmed = raw.Trim();
        var upper = trimmed.ToUpperInvariant();

        if (upper is "CSE" or "COMPUTER SCIENCE" or "COMPUTER SCIENCE & ENGINEERING" or "COMPUTER SCIENCE AND ENGINEERING")
            return CSE;
        if (upper is "EEE" or "ELECTRICAL" or "ELECTRICAL & ELECTRONIC ENGINEERING" or "ELECTRICAL AND ELECTRONIC ENGINEERING")
            return EEE;
        if (upper is "CE" or "CIVIL" or "CIVIL ENGINEERING")
            return CE;
        if (upper is "ME" or "MECHANICAL" or "MECHANICAL ENGINEERING" or "MECHA")
            return ME;
        if (upper is "IPE" or "INDUSTRIAL" or "INDUSTRIAL & PRODUCTION ENGINEERING" or "INDUSTRIAL AND PRODUCTION ENGINEERING")
            return IPE;
        if (upper is "TE" or "TEXTILE" or "TEXTILE ENGINEERING")
            return TE;

        return trimmed;
    }

    public static string GetDepartmentCode(string? departmentName)
    {
        var normalized = Normalize(departmentName);
        var prog = AllPrograms.FirstOrDefault(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase));
        return prog?.Code ?? "GEN";
    }

    public static string GetShortCode(string? departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName)) return "GEN";
        var normalized = Normalize(departmentName);
        var prog = AllPrograms.FirstOrDefault(p => 
            string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase) || 
            string.Equals(p.Code, departmentName.Trim(), StringComparison.OrdinalIgnoreCase));
        return prog?.ShortName ?? prog?.Code ?? "GEN";
    }

    public static string GetDisplayName(string? codeOrName)
    {
        if (string.IsNullOrWhiteSpace(codeOrName)) return General;
        var trimmed = codeOrName.Trim();
        var prog = AllPrograms.FirstOrDefault(p => 
            string.Equals(p.Code, trimmed, StringComparison.OrdinalIgnoreCase) || 
            string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (prog != null) return prog.Name;
        if (string.Equals(trimmed, "GEN", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "General", StringComparison.OrdinalIgnoreCase)) return General;
        return Normalize(trimmed);
    }

    public static string GetDepartmentIcon(string? departmentName)
    {
        var normalized = Normalize(departmentName);
        var prog = AllPrograms.FirstOrDefault(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase));
        return prog?.IconClass ?? "bi-mortarboard";
    }
}
