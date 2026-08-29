# AGENTS.md — Standing Context for AI Coding Agents

## 1. Project Summary
- **Project**: InternLink — AI-powered university career & internship portal (CSE 3200 & CSE 3224, AUST).
- **User Roles**: Student, Company, Admin, Counselor.

## 2. Architecture Overview
Single ASP.NET Core MVC app (.NET 8, Razor Views, Bootstrap 5, vanilla JS fetch — NO jQuery, NO SPA framework, NO Node toolchain). SQL Server with hand-authored schema (`db/scripts/`). EF Core is a MAPPER, never schema owner (NO migrations). Qdrant holds job vectors (768-d, cosine); SQL Server FTS is lexical search and semantic fallback. Gemini Flash for generation via multi-key rotating gateway with Polly resilience; `text-embedding-004` for vectors. Identity cookie auth + email OTP second factor.

```
                ┌──────────────────────────────────────────────────┐
                │        ASP.NET Core MVC (.NET 8) — ONE app       │
                │  Razor Views + Bootstrap 5 + vanilla JS (fetch)  │
                │  Areas: Student / Company / Admin / Counselor    │
                │  ASP.NET Core Identity (cookie auth) + email OTP │
                └───────┬───────────────┬──────────────┬───────────┘
        manual param SQL│      Qdrant   │              │ HTTPS/JSON
        via EF mapping  │      .NET SDK │              │ (typed HttpClients)
                        ▼               ▼              ▼
              ┌───────────────┐ ┌──────────────┐ ┌──────────────────────────┐
              │ SQL Server    │ │ Qdrant Cloud │ │ Google AI                │
              │ hand-authored │ │ (job vectors,│ │  text-embedding-004      │
              │ schema (SSMS) │ │  768-d,      │ │  Gemini Flash (N keys,   │
              │ + FTS catalog │ │  cosine)     │ │  rotation + Polly)       │
              └───────────────┘ └──────────────┘ └──────────────────────────┘
```

## 3. Data-Access Conventions (CRITICAL)
- **Schema Truth**: Lives ONLY in `db/scripts/*.sql`, numbered (`000_`, `001_`...), run once, recorded in `SchemaVersions`. NEVER run `dotnet ef migrations`.
- **Hand-Written Parameterized T-SQL**: Repositories (`Repositories/Implementation`) execute hand-written T-SQL:
  - Reads: `db.Jobs.FromSql($"SELECT j.* FROM Jobs j WHERE j.Id = {id}")` (auto-parameterized) or `FromSqlRaw` with explicit `SqlParameter`.
  - Writes: `ExecuteSql` / `ExecuteSqlRaw`. NEVER string-concatenate unparameterized user input into SQL.
  - LINQ-to-Entities is allowed ONLY for trivial by-id lookups and Identity store.
- **Layering**: Controllers never touch `DbContext` (`Controller` $\rightarrow$ `Service` $\rightarrow$ `Repository`). Views bind `ViewModel` objects only, never domain entities.

## 4. Frontend Conventions
- **Server-Rendered MVC**: Razor Views + partials. Bootstrap 5 customized theme (`site.css`).
- **Fetch Wrapper**: `wwwroot/js/api.js` is the ONLY place `fetch()` is called directly. Non-GET requests attach `X-CSRF-TOKEN` header.
- **Interactions**: Server-rendered pages by default; fetch+JSON strictly for in-page interactions (wizard steps, kanban, chat, notification bell).

## 5. Relational Schema Summary
- `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims` (Identity)
- `Students` (Student profiles, CGPA, department)
- `Companies` (Company profiles, industry, `VerificationStatus`)
- `Jobs` (Title, description, location type, deadline, approval/closed state)
- `Applications` (Job application status, resume link, cover letter text, UNIQUE(JobId, StudentId))
- `Interviews` (Scheduled interview dates, meeting links, status)
- `Resumes` (Document path, `DynamicJsonData` JSON blob)
- `Skills` & `StudentSkills` & `JobSkills` (Relational skills with proficiency/weights)
- `Notifications` (User notifications with read state & routing URL)
- `Assessments` (Timed skill assessment scores)
- `CounselorFeedback` (Sanitized markdown feedback notes)
- `AIHistory` (Token ledger: tokens, feature, estimated cost)
- `OtpCodes` (Email 2FA hashes, expiration, consumption state)
- `SchemaVersions` (Idempotent script migration ledger)
- `MockInterviewSessions` (Persistent AI interview session transcripts & reports)

## 6. Non-Functional Constraints
- Page loads < 2s; PBKDF2 default hasher via Identity (do not alter hasher).
- All SQL parameterized; all AI/embedding calls `async` with `CancellationToken`, wrapped in try/catch with graceful fallback.
- Every Gemini call logged to `AIHistory` with token counts; monetary costs decimal, never float.
- Global antiforgery on every POST.

## 7. Git & Development Workflow
- Conventional Commits (`feat:`, `chore:`, `fix:`, `docs:`, `test:`).
- One feature per branch; PR title matches commit message.
- Gates: `dotnet build` + `dotnet test` + prompt checklist pass before merge.

## 8. Strictly Prohibited ("Do Not" List)
- Do NOT add Docker or CI/CD pipeline files.
- Do NOT add EF Core migrations or alter `DbContext` expecting schema to auto-create.
- Do NOT introduce jQuery, React, npm, or external JS build toolchains.
- Do NOT call LLM/embedding APIs synchronously (`.Result` or `.Wait()`).
- Do NOT store secrets outside `dotnet user-secrets`.
- Do NOT bypass repositories from controllers or expose domain entities in views.
- Do NOT swallow exceptions silently.

## 9. Conflict Resolution Rule
If a prompt conflicts with this file, flag the conflict to the user rather than guessing. `AGENTS.md` is the project source of truth.
