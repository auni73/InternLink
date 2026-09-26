# Docker Deployment

InternLink runs as an ASP.NET Core web container with SQL Server 2022. Qdrant stays
hosted in Qdrant Cloud; Gemini and SMTP are external services configured through
environment variables.

## Prerequisites

- Docker Engine or Docker Desktop with the Compose plugin
- A Qdrant Cloud endpoint and API key
- Google Gemini API key(s)
- SMTP credentials for email delivery in Production
- A reverse proxy or load balancer that terminates TLS for Production

## First run

1. Copy `.env.example` to `.env`.
2. Set `SA_PASSWORD` to a strong SQL Server password and set the password in
   `ConnectionStrings__InternLinkDb` to the same value. The connection string must
   use SQL Authentication and `Server=sqlserver`; Windows Trusted/Integrated auth
   cannot authenticate across Linux containers.
3. Set the Gemini, Qdrant, and SMTP values. Keep `.env` out of Git; `.gitignore`
   excludes it.
4. Start the development stack:

   ```powershell
   docker compose up --build
   ```

   The Compose override sets `ASPNETCORE_ENVIRONMENT=Development` and publishes the
   web app at `http://localhost:8080`. Development accounts are seeded after schema
   setup, and OTPs are logged to the app console.

For a Production configuration, use the production overlay and provide the proxy IP
as described below:

```powershell
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

When explicitly supplying `-f` files, Compose does not automatically include
`docker-compose.override.yml`. Production therefore runs with
`ASPNETCORE_ENVIRONMENT` from `.env` (default `Production`).

## Database bootstrap and schema scripts

On every app startup, `DatabaseMigrationRunner` first opens a connection to the
`master` catalog using the configured server and credentials. It runs
`db/scripts/000_create_database.sql`, which creates `InternLink` when needed and
creates its `SchemaVersions` ledger. The app then connects with the normal
`Database=InternLink` connection string and applies any unapplied numbered scripts
from `db/scripts/` in order.

This first master-catalog connection is necessary because a fresh SQL Server does not
yet have the `InternLink` database, so a connection scoped to that database cannot
run the script that creates it. The bootstrap and schema application run in every
environment. Development-only account seeding remains gated to Development. Scripts
are copied into the published image; the numbered SQL files remain the schema source
of truth. SQL Server data and generated resume files persist in named volumes.

## TLS and forwarded headers

Production expects TLS to terminate at a reverse proxy or cloud load balancer. The
proxy forwards `X-Forwarded-For` and `X-Forwarded-Proto`; the app uses the forwarded
HTTPS scheme before HTTPS redirection, HSTS, and secure-cookie handling.

Forwarded headers are accepted only from configured proxy IP addresses. Set one or
more `ForwardedHeaders__KnownProxies__N` entries in `.env` to the stable IP addresses
of the trusted proxy (for example, `ForwardedHeaders__KnownProxies__0=172.20.0.10`).
Configure the proxy/network to keep those addresses stable and prevent untrusted
clients from reaching the app directly. The Compose port binds to `127.0.0.1` by
default. A proxy on another host requires setting `WEB_HTTP_BIND` to a reachable
interface and restricting access with the host firewall. If no proxy IP is
configured, only the framework's default trusted proxies are accepted. The app
container listens on HTTP port 8080; it does not terminate TLS itself.

## Health and persistent data

- SQL Server's Compose healthcheck uses `sqlcmd` before the web service starts.
- The web image and Compose service check `/health`, which reports unhealthy when the
  database cannot be reached.
- `sqlserver-data` persists SQL Server files.
- `resume-data` persists files under `/app/data/resumes`.

To stop the stack while preserving data, run `docker compose down`. Removing named
volumes deletes the persisted database and resume files.
