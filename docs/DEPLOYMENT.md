# Production Deployment Guide

This guide covers deploying **MobileAppDeployment** to a production environment.

---

## Prerequisites

- .NET 8 ASP.NET Core runtime (or publish self-contained)
- PostgreSQL 14+
- HTTPS reverse proxy (nginx or Caddy recommended)
- Writable `wwwroot/uploads/` directory
- Persistent volume for `DataProtection-Keys/` (required for OneSignal key decryption across restarts)

---

## Required environment variables

Set these in your hosting platform (never commit real values):

| Variable | Example key | Description |
|----------|-------------|-------------|
| Connection string | `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| Form API key | `FormAccess__ApiKey` | Admin API key for `X-Api-Key` header |
| Public form URL base | `FormAccess__PublicBaseUrl` | e.g. `https://deploy.example.com` |
| Admin email | `Identity__AdminEmail` | Seeded admin account email |
| Admin password | `Identity__AdminPassword` | Seeded admin account password (min 12 chars, non-alphanumeric required) |
| GitHub PAT | `GitHub__PersonalAccessToken` | `repo` + `workflow` scopes |
| GitHub dispatch URL | `GitHub__WorkflowDispatch__PublicBaseUrl` | Public URL GitHub Actions runners can reach |
| Mailgun SMTP | `MailgunSMTP__SMTPUsername`, `MailgunSMTP__SMTPPassword`, `MailgunSMTP__FromEmail` | Optional email delivery |

---

## Build and publish

```powershell
cd MobileAppDeployment
dotnet publish -c Release -o ./publish
```

Copy `publish/` to the server. Ensure `wwwroot/uploads/` exists and is writable by the app process.

---

## Database migrations in CI/CD

Generate an idempotent SQL script (safe to run multiple times):

```powershell
dotnet ef migrations script --idempotent -o deploy.sql
```

Apply in your pipeline **before** starting the app:

```bash
psql "$DATABASE_URL" -f deploy.sql
```

Or on the server after deploy:

```powershell
dotnet ef database update
```

Migrations to expect after the enterprise refactor:

- `Add_OneSignalRestApiKey_Encrypted` — encrypted secret column
- `Add_AspNetCoreIdentity` — admin authentication tables

---

## Health check

The app exposes a public endpoint (no authentication):

```
GET /health
```

Configure your load balancer or uptime monitor to poll this URL. A healthy instance returns **HTTP 200**.

Checks performed:

| Check | Name | Description |
|-------|------|-------------|
| PostgreSQL | `npgsql` | Database connectivity |
| File system | `filesystem` | `wwwroot/uploads/` writable |

---

## Reverse proxy (HTTPS)

### nginx snippet

```nginx
server {
    listen 443 ssl http2;
    server_name deploy.example.com;

    ssl_certificate     /etc/ssl/certs/deploy.example.com.crt;
    ssl_certificate_key /etc/ssl/private/deploy.example.com.key;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        client_max_body_size 52M;
    }

    location /health {
        proxy_pass http://127.0.0.1:5000/health;
        access_log off;
    }
}
```

### Caddy snippet

```caddy
deploy.example.com {
    reverse_proxy localhost:5000
    request_body {
        max_size 52MB
    }
}
```

---

## Post-deploy smoke test

1. `GET /health` → 200
2. `POST /api/form-access-tokens` with valid `X-Api-Key` → 200 + `formUrl`
3. Sign in at `/Account/Login` with seeded admin credentials
4. Open `GET /AppDeployment` → deployment list (requires login)

See [DEVELOPMENT-SETUP.md](DEVELOPMENT-SETUP.md) for local equivalents.
