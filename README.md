# InternLink

InternLink is an AI-powered university career & internship portal built with ASP.NET Core MVC (.NET 8), Microsoft SQL Server, Qdrant Vector DB, Google Gemini Flash, and Bootstrap 5.

---

## Environment Setup

### Configuration Contract

| Config Key | Required For | Where to Get the Value | Prompt First Consuming |
| :--- | :--- | :--- | :--- |
| `ConnectionStrings:InternLinkDb` | Database access & EF Core mapping | Local SQL Server 2022 instance (e.g. `Server=localhost\MSSQLSERVER01;Database=InternLink;Trusted_Connection=True;TrustServerCertificate=True;`) | **Prompt 4** |
| `Gemini:ApiKeys` | AI generation & multi-key rotation pool | Google AI Studio (create 2–3 API keys across Google accounts/projects) | **Prompt 19** |
| `Gemini:Model` | Gemini generation model selection | Google AI Studio docs (e.g. `gemini-2.5-flash`) | **Prompt 19** |
| `Gemini:EmbeddingModel` | Text embeddings for vector search | Google AI Studio docs (e.g. `text-embedding-004`) | **Prompt 20** |
| `Qdrant:Endpoint` | Vector DB connection | Qdrant Cloud cluster overview URL (e.g. `https://xxx.qdrant.tech:6334`) | **Prompt 20** |
| `Qdrant:ApiKey` | Vector DB authentication | Qdrant Cloud API key dashboard | **Prompt 20** |
| `Qdrant:CollectionName` | Job vector collection | Default: `internlink-jobs` | **Prompt 20** |
| `Smtp:Host` / `Port` / `User` / `Pass` / `FromAddress` | Email 2FA OTP codes | SMTP provider credentials (optional in Development; dev logs OTPs to console) | **Prompt 8** |
| `Storage:ResumeRoot` | Generated resume PDF storage | Absolute local directory on host machine (e.g. `C:\InternLinkData\resumes`) | **Prompt 11** |

---

## Secret Handling Rules (Strict)

> [!CAUTION]
> **NEVER commit real API keys, secrets, or passwords to `appsettings.json` or Git.**

All real secrets and credentials must be stored locally per developer using `dotnet user-secrets`.

### Setting Up Local User Secrets

Once the `src/InternLink.Web` project is scaffolded, initialize and set secrets locally:

```powershell
# Navigate to the web project directory
cd src/InternLink.Web

# Initialize user-secrets for the project
dotnet user-secrets init

# Set Gemini rotation keys (comma-separated, no spaces)
dotnet user-secrets set "Gemini:ApiKeys" "AIzaSyKeyOne...,AIzaSyKeyTwo..."

# Set Qdrant credentials
dotnet user-secrets set "Qdrant:Endpoint" "https://your-cluster-url.qdrant.tech:6334"
dotnet user-secrets set "Qdrant:ApiKey" "your-qdrant-api-key"

# Set local database connection string (if different from appsettings default)
dotnet user-secrets set "ConnectionStrings:InternLinkDb" "Server=localhost\MSSQLSERVER01;Database=InternLink;Trusted_Connection=True;TrustServerCertificate=True"
```

---

## Toolchain Prerequisites

- **.NET 8 SDK** (`dotnet --version` $\ge$ `8.0.x`)
- **SQL Server 2022 Developer Edition** + **SSMS** (Full-Text Search enabled)
- **Git**
- **Qdrant Cloud Free Tier** (1GB cluster)
- **Google AI Studio API Keys** (2–3 keys for rotation pool)
