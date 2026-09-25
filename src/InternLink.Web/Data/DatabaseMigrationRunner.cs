using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace InternLink.Web.Data;

public static class DatabaseMigrationRunner
{
    private static readonly Regex GoBatchRegex = new(
        @"^\s*GO\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public static async Task ApplyPendingScriptsAsync(
        ApplicationDbContext db, 
        string contentRootPath, 
        ILogger logger, 
        CancellationToken ct = default)
    {
        try
        {
            var scriptsDir = Path.Combine(contentRootPath, "..", "..", "db", "scripts");
            if (!Directory.Exists(scriptsDir))
            {
                // Fallback search up directory tree
                var current = new DirectoryInfo(contentRootPath);
                while (current != null)
                {
                    var candidate = Path.Combine(current.FullName, "db", "scripts");
                    if (Directory.Exists(candidate))
                    {
                        scriptsDir = candidate;
                        break;
                    }
                    current = current.Parent;
                }
            }

            if (!Directory.Exists(scriptsDir))
            {
                logger.LogWarning("Database scripts directory not found. Skipping auto-migration.");
                return;
            }

            var applied = (await db.Database
                .SqlQueryRaw<string>("SELECT ScriptName AS Value FROM dbo.SchemaVersions")
                .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sqlFiles = Directory.GetFiles(scriptsDir, "*.sql")
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in sqlFiles)
            {
                var scriptName = Path.GetFileName(file);
                if (applied.Contains(scriptName))
                {
                    continue;
                }

                logger.LogInformation("Applying pending SQL schema script: {ScriptName}", scriptName);
                var fullText = await File.ReadAllTextAsync(file, ct);
                var batches = GoBatchRegex.Split(fullText);

                foreach (var batch in batches)
                {
                    var trimmed = batch.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        continue;
                    }

                    // Strip any USE statement because connection string already targets database
                    if (trimmed.StartsWith("USE [", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("USE ", StringComparison.OrdinalIgnoreCase))
                    {
                        var newlineIdx = trimmed.IndexOf('\n');
                        trimmed = newlineIdx >= 0 ? trimmed[(newlineIdx + 1)..].Trim() : "";
                    }

                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        continue;
                    }

                    await db.Database.ExecuteSqlRawAsync(trimmed, ct);
                }

                logger.LogInformation("Successfully executed {ScriptName}.", scriptName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while checking or applying pending SQL schema scripts.");
            throw;
        }
    }
}
