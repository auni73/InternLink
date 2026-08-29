# InternLink Database Scripts

This folder contains the authoritative, hand-authored T-SQL scripts for the InternLink database.

> [!IMPORTANT]
> **Schema Rules:**
> 1. Scripts must be executed in strict numeric order (`000_`, `001_`, `002_`, etc.).
> 2. Every script is idempotent and protected by the `SchemaVersions` table.
> 3. Once a script is merged into `main` / `default`, it is **never edited** — schema updates require writing a new numbered `.sql` script and recording its execution in `SchemaVersions`.
> 4. EF Core is used strictly as a mapper and query execution layer. Never run `dotnet ef migrations`.

---

## Execution Order

1. **`000_create_database.sql`**: Creates the `InternLink` database and the `SchemaVersions` tracking table.
2. **`001_identity_tables.sql`**: Hand-authored ASP.NET Core Identity tables with `uniqueidentifier` (GUID) primary keys.
3. **`002_domain_tables.sql`**: Domain entity tables with `NEWSEQUENTIALID()`, integrity constraints, and explicit FK cascade behaviors.
4. **`003_indexes.sql`**: Performance indexes optimized for browse, search, and dashboard queries.
5. **`004_fulltext.sql`**: SQL Server Full-Text Search catalog and index for `Jobs` (with graceful fallback if FTS is not installed).
6. **`005_seed_reference_data.sql`**: Reference `Skills` rows shared by students and job postings.
7. **`006_admin_rejection_reason.sql`**: Adds `Companies.AdminRejectionReason` for moderation feedback.
8. **`007_mock_interview_sessions.sql`**: `MockInterviewSessions` table backing the persistent AI mock interview chatbot.

---

## Applying Scripts

Open SQL Server Management Studio (SSMS), connect to your local SQL Server instance (e.g. `.\SQLEXPRESS`), open each script in order, and execute (`F5`).
