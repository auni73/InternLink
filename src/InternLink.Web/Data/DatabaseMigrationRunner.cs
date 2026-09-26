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
        logger.LogInformation(
            "Connecting directly to the existing InternLink database.");

        const int maxRetries = 5;
        int delayMs = 2000;

        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(ct);

                const string createSchemaVersionsSql = """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.tables
                    WHERE name = N'SchemaVersions'
                      AND schema_id = SCHEMA_ID(N'dbo')
                )
                BEGIN
                    CREATE TABLE dbo.SchemaVersions (
                        ScriptName NVARCHAR(200) NOT NULL,
                        AppliedAt DATETIMEOFFSET NOT NULL
                            CONSTRAINT DF_SchemaVersions_AppliedAt
                            DEFAULT SYSDATETIMEOFFSET(),
                        CONSTRAINT PK_SchemaVersions
                            PRIMARY KEY CLUSTERED (ScriptName)
                    );
                END
                """;

                await using var command = new SqlCommand(
                    createSchemaVersionsSql,
                    connection);

                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(ct);

                logger.LogInformation(
                    "InternLink database is ready. SchemaVersions table verified.");
                
                return;
            }
            catch (SqlException ex) when (i < maxRetries)
            {
                logger.LogWarning(ex, "Transient SQL error {Number} connecting to database. Retrying {RetryCount}/{MaxRetries} in {DelayMs}ms...", ex.Number, i + 1, maxRetries, delayMs);
                await Task.Delay(delayMs, ct);
                delayMs *= 2;
            }
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
                .Where(f => !string.Equals(
                    Path.GetFileName(f),
                    "000_create_database.sql",
                    StringComparison.OrdinalIgnoreCase))
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
                    
                    const int maxRetries = 3;
                    int delayMs = 1000;
                    for (int i = 0; i <= maxRetries; i++)
                    {
                        try
                        {
                            if (connection.State != System.Data.ConnectionState.Open)
                            {
                                await connection.OpenAsync(ct);
                            }

                            await using var command = connection.CreateCommand();
                            command.CommandText = trimmed;
                            command.CommandTimeout = 120;
                            await command.ExecuteNonQueryAsync(ct);
                            
                            break;
                        }
                        catch (SqlException ex) when (i < maxRetries)
                        {
                            logger.LogWarning(ex, "Transient SQL error {Number} during script execution. Retrying {RetryCount}/{MaxRetries} in {DelayMs}ms...", ex.Number, i + 1, maxRetries, delayMs);
                            await Task.Delay(delayMs, ct);
                            delayMs *= 2;
                        }
                    }
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
