using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InternLink.Web.Data;

public static class DatabaseMigrationRunner
{
    private static readonly Regex GoBatchRegex = new(
        @"^\s*GO\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public static async Task BootstrapDatabaseAsync(
        string connectionString,
        string contentRootPath,
        ILogger logger,
        CancellationToken ct = default)
    {
        var scriptsDir = FindScriptsDirectory(contentRootPath);
        if (scriptsDir is null)
        {
            throw new DirectoryNotFoundException("Database scripts directory was not found.");
        }

        var bootstrapPath = Path.Combine(scriptsDir, "000_create_database.sql");
        if (!File.Exists(bootstrapPath))
        {
            throw new FileNotFoundException("Database bootstrap script was not found.", bootstrapPath);
        }

        var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        logger.LogInformation("Ensuring the InternLink database exists using the master catalog.");
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(ct);

        var script = await File.ReadAllTextAsync(bootstrapPath, ct);
        foreach (var batch in GoBatchRegex.Split(script))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    public static async Task ApplyPendingScriptsAsync(
        ApplicationDbContext db, 
        string contentRootPath, 
        ILogger logger, 
        CancellationToken ct = default)
    {
        try
        {
            var scriptsDir = FindScriptsDirectory(contentRootPath);
            if (scriptsDir is null)
            {
                throw new DirectoryNotFoundException("Database scripts directory was not found.");
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

                    var connection = db.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync(ct);
                    }

                    await using var command = connection.CreateCommand();
                    command.CommandText = trimmed;
                    command.CommandTimeout = 120;
                    await command.ExecuteNonQueryAsync(ct);
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

    private static string? FindScriptsDirectory(string contentRootPath)
    {
        var current = new DirectoryInfo(contentRootPath);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "db", "scripts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
