# Local Development Setup Guide

This guide walks a new developer through setting up **MobileAppDeployment** on a fresh machine.
After following it you will have:
- PostgreSQL database created and migrated
- All secrets stored safely in `dotnet user-secrets` (never in committed files)
- The application running locally

---

## Prerequisites

| Tool | Minimum Version | Install |
|------|----------------|---------|
| .NET SDK | 8.0 | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| PostgreSQL | 14 | [postgresql.org](https://www.postgresql.org/download/) |
| Git | Any | [git-scm.com](https://git-scm.com) |
| EF Core tools | 8.x | See Step 3 below |

---

## Step 1 — Clone the repository

```powershell
git clone https://github.com/JayeshPatil2023/MobileAppDeployment.git
cd MobileAppDeployment
```

---

## Step 2 — Create the PostgreSQL database

Start `psql` (or use pgAdmin) and run:

```sql
CREATE DATABASE "SystenicsAppDeployment";
```

Note your PostgreSQL host, port, username, and password.

---

## Step 3 — Install EF Core tools (once per machine)

```powershell
cd MobileAppDeployment   # the folder containing the .csproj
dotnet tool restore      # restores dotnet-ef from .config/dotnet-tools.json
dotnet ef --version      # confirm it works
```

---

## Step 4 — Configure secrets with `dotnet user-secrets`

> **Why user-secrets?**
> Real passwords, API keys, and tokens must never be committed to source control.
> `appsettings.Development.json` is intentionally blank — secrets go in user-secrets instead.
> User-secrets are stored in your OS profile and are never checked into git.

Run each command below, replacing the placeholder values with your own:

```powershell
cd MobileAppDeployment   # must be in the folder with the .csproj

# PostgreSQL connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Host=localhost;Port=5432;Database=SystenicsAppDeployment;Username=postgres;Password=YOUR_PG_PASSWORD;"

# Admin API key — any strong random string you choose (used in X-Api-Key header)
dotnet user-secrets set "FormAccess:ApiKey" "your-local-admin-key-here"

# Optional: public base URL for form links (ngrok or localhost is fine for local dev)
dotnet user-secrets set "FormAccess:PublicBaseUrl" "https://your-ngrok.ngrok-free.dev"

# Mailgun SMTP (optional — email sending is non-critical for local dev)
dotnet user-secrets set "MailgunSMTP:SMTPUsername" "postmaster@mg.yourdomain.com"
dotnet user-secrets set "MailgunSMTP:SMTPPassword" "your-mailgun-smtp-password"
dotnet user-secrets set "MailgunSMTP:FromEmail"    "no-reply@yourdomain.com"

# GitHub Personal Access Token (required to trigger GitHub Actions workflows)
# Create one at: https://github.com/settings/tokens
# Required scopes: repo, workflow
dotnet user-secrets set "GitHub:PersonalAccessToken" "ghp_YOUR_TOKEN_HERE"

# GitHub workflow dispatch public URL (GitHub Actions runners must reach this)
dotnet user-secrets set "GitHub:WorkflowDispatch:PublicBaseUrl" "https://your-ngrok.ngrok-free.dev"

# Admin login (ASP.NET Core Identity — required for /AppDeployment admin UI)
dotnet user-secrets set "Identity:AdminEmail" "admin@yourdomain.com"
dotnet user-secrets set "Identity:AdminPassword" "YourSecureP@ssw0rd12"
```

Verify secrets are stored:

```powershell
dotnet user-secrets list
```

---

## Step 5 — Apply database migrations

```powershell
cd MobileAppDeployment   # folder with .csproj
dotnet ef database update
```

This creates all tables in the PostgreSQL database you configured in Step 4.

---

## Step 6 — Build and run

```powershell
dotnet build
dotnet run
```

The app will be available at `https://localhost:PORT` (check the console for the exact port).

---

## Step 7 — First smoke test

1. Open the app in a browser
2. Call the token API to issue a form link:

```bash
curl -X POST https://localhost:PORT/api/form-access-tokens \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-local-admin-key-here" \
  -d '{"clientName":"Acme Corp","clientAppName":"Acme App"}'
```

3. Copy the `formUrl` from the response and open it in a browser
4. Fill in Organization Name and App Name, then save

---

## Troubleshooting

| Problem | Solution |
|---------|---------|
| `dotnet ef` not found | Run `dotnet tool restore` in the project folder |
| `password authentication failed` | Check your PostgreSQL password in user-secrets |
| App starts but shows blank/error page | Run `dotnet ef database update` — tables may be missing |
| Email fails | SMTP credentials missing or incorrect — email is optional for local dev |
| Workflow dispatch fails | GitHub PAT missing or expired; ensure `GITHUB_TOKEN` env var or user-secret is set |
| `PublicBaseUrl` error on dispatch | Set to your ngrok URL — GitHub runners cannot reach localhost |

---

## Secrets quick-reference

| Secret key | Purpose | Required for |
|-----------|---------|-------------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection | Everything |
| `FormAccess:ApiKey` | Admin token-issuance API auth | Issuing form links |
| `FormAccess:PublicBaseUrl` | Base URL in form link responses | API responses |
| `MailgunSMTP:SMTPUsername` | Mailgun auth | Email delivery |
| `MailgunSMTP:SMTPPassword` | Mailgun auth | Email delivery |
| `MailgunSMTP:FromEmail` | Sender address | Email delivery |
| `GitHub:PersonalAccessToken` | GitHub API auth | Workflow dispatch |
| `GitHub:WorkflowDispatch:PublicBaseUrl` | Asset download URL for GitHub runners | Workflow dispatch |
| `Identity:AdminEmail` | Seeded admin account email | Admin UI login |
| `Identity:AdminPassword` | Seeded admin password (min 12 chars) | Admin UI login |

> **Data Protection keys:** OneSignal REST API keys are encrypted at rest. The key ring is stored in `DataProtection-Keys/` (git-ignored). Do not delete this folder between restarts or existing ciphertext cannot be decrypted.

---

## Asset storage

By default (`Storage:StorageType` = `Local`) uploaded assets are stored under `wwwroot/uploads` via `LocalAssetStorageService`.

To swap to Azure Blob or S3 later:

1. Implement `IAssetStorageService` for the cloud provider.
2. Register it in `AddStorageServices` when `Storage:StorageType` matches your backend.
3. Keep controllers and application services unchanged — they depend only on the interface.

---

## Admin login

After migrations and first run, sign in at `/Account/Login` with the `Identity:AdminEmail` and `Identity:AdminPassword` values from user-secrets. Client form links (`/AppDeployment/Form/{token}`) remain public — no login required.


Once the app is running, verify it is healthy:

```powershell
curl https://localhost:PORT/health
```

Expected response: `Healthy`

---

## See also

- [ENTITY-FRAMEWORK-CORE-GUIDE.md](ENTITY-FRAMEWORK-CORE-GUIDE.md) — Adding/removing fields and migrations
- [ARCHITECTURE.md](ARCHITECTURE.md) — System design and layer responsibilities *(created in Phase 7)*
- [API.md](API.md) — Full API reference *(created in Phase 7)*
