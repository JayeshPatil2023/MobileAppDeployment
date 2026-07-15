# Enterprise Refactoring Walkthrough

## Status

**Part 2.2 folder architecture is aligned** with `implementation_plan.md`. Behavioral Phases 1–7 (security, MailKit, Channel jobs, ViewModels, Identity, health checks, encryption, tests, docs) are in place.

Verify:

```
dotnet build -warnaserror
dotnet test
```

## What was completed

### Security & infrastructure (Phases 1–2)
- Secrets out of committed config; user-secrets + `UserSecretsId`
- Security headers, global exception middleware, rate limiting (token 10/min, form **5/min**, global 200/min)
- Path traversal guards; MailKit email; `AssetStorageConstants`; EF `IEntityTypeConfiguration`
- **`IUnitOfWork`** + repositories stage changes and commit via UoW

### Application (Phase 3)
- ViewModels + mapping; `IAssetUploadStrategy`; `ApiKeyAuthorizationFilter`
- `Channel<T>` + `WorkflowDispatchBackgroundService`; job-store eviction

### Controllers & ops (Phase 4)
- Slimmed `AppDeploymentController` (logic pushed to `AppDeploymentService.SaveDraftWithAssetsAsync`)
- `WorkflowController`; health checks; OneSignal Data Protection encryption

### Auth (Phase 5)
- ASP.NET Core Identity, admin seeder, cookie auth, protected admin routes

### Tests & docs (Phases 6–7)
- `MobileAppDeployment.Tests` (13 tests)
- `docs/API.md`, `DEPLOYMENT.md`, `SECURITY.md`, `CONTRIBUTING.md`, `TESTING.md`, updated `ARCHITECTURE.md` / README
- CI: `.github/workflows/ci.yml`

### Part 2.2 layout (structural)
```
Core/ · Application/ · Infrastructure/ · Web/ · Options/ · Extensions/
```
Old flat folders (`Controllers/`, `Services/`, `Models/`, `Repositories/`, `Helpers/`, `Data/`, `TagHelpers/`) removed.

## Intentionally kept / deferred
| Item | Rationale |
|------|-----------|
| `Migrations/` at project root | Safer for EF tooling (plan allowed Persistence placement; root is pragmatic) |
| `AssetPaths` as helper VO (no EF owned type) | Avoids extra migration |
| `FormAccessLinkTemplate` extract | Composer already under Infrastructure/Email |

## Local secrets (required)

See [docs/DEVELOPMENT-SETUP.md](docs/DEVELOPMENT-SETUP.md).
