# MobileAppDeployment

Internal **mobile app store-submission portal** built with ASP.NET Core MVC (.NET 8). Admins issue clients a magic-link token; clients complete a deployment configuration form; admins trigger a GitHub Actions workflow to automate build and store submission.

## Quick start

```powershell
git clone https://github.com/JayeshPatil2023/MobileAppDeployment.git
cd MobileAppDeployment/MobileAppDeployment
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=SystenicsAppDeployment;Username=postgres;Password=YOUR_PASSWORD;"
dotnet user-secrets set "FormAccess:ApiKey" "your-local-admin-key"
dotnet user-secrets set "Identity:AdminEmail" "admin@localhost.dev"
dotnet user-secrets set "Identity:AdminPassword" "ChangeMeNow!123"
dotnet ef database update
dotnet build
```

Full setup (PostgreSQL, all secrets, smoke test): **[docs/DEVELOPMENT-SETUP.md](docs/DEVELOPMENT-SETUP.md)**

## Documentation

| Document | Description |
|----------|-------------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System design, layers, request flows |
| [docs/DEVELOPMENT-SETUP.md](docs/DEVELOPMENT-SETUP.md) | Local dev, user-secrets, migrations |
| [docs/API.md](docs/API.md) | `POST /api/form-access-tokens` reference |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Production deploy, health checks, reverse proxy |
| [docs/SECURITY.md](docs/SECURITY.md) | Threat model, secrets, encryption |
| [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) | Coding standards, PR checklist |
| [docs/ENTITY-FRAMEWORK-CORE-GUIDE.md](docs/ENTITY-FRAMEWORK-CORE-GUIDE.md) | EF Core migrations and field changes |

## Tests

```powershell
dotnet test
```

## Key endpoints

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `POST /api/form-access-tokens` | `X-Api-Key` | Issue client form link |
| `GET /AppDeployment/Form/{token}` | Public | Client form entry |
| `GET /AppDeployment` | Admin cookie | Deployment list |
| `GET /Account/Login` | Public | Admin sign-in |
| `GET /health` | Public | Health check |

## Entity Framework commands

Run from `MobileAppDeployment/` (folder containing the `.csproj`):

```powershell
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

See [docs/ENTITY-FRAMEWORK-CORE-GUIDE.md](docs/ENTITY-FRAMEWORK-CORE-GUIDE.md) for detailed EF workflows.
