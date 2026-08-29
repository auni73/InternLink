using System.Text;
using System.Text.RegularExpressions;

namespace InternLink.Web.Helpers;

public static class FtsQueryBuilder
{
    private static readonly Regex TokenRegex = new(@"[a-zA-Z0-9\+#\.]+", RegexOptions.Compiled);

    /// <summary>
    /// Builds a safe, defensive SQL Server Full-Text Search (CONTAINS/CONTAINSTABLE) query string.
    /// Tokenizes input, escapes double-quotes, and applies prefix matching (*).
    /// Prevents FTS syntax errors and injection of malformed operators.
    /// </summary>
    public static string? BuildPrefixAndQuery(string? rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return null;
        }

        var matches = TokenRegex.Matches(rawQuery);
        if (matches.Count == 0)
        {
            return null;
        }

        var terms = new List<string>();
        foreach (Match match in matches)
        {
            var word = match.Value.Trim().Replace("\"", "");
            if (word.Length > 0 && word.Any(char.IsLetterOrDigit))
            {
                // Escape any single quotes for safety
                word = word.Replace("'", "''");
                terms.Add($"\"{word}*\"");
            }
        }

        if (terms.Count == 0)
        {
            return null;
        }

        return string.Join(" AND ", terms);
    }
}
