# MobileAppDeployment — Refactoring Task List

## Phases 1–7 (behavior) ✅ COMPLETE
See prior checklist entries for security, Identity, health, ViewModels, tests, docs.

## Part 2.2 Structural Alignment ✅ COMPLETE

- [x] `Core/Domain` — Entities, Enums (`WorkflowJobStatus`), ValueObjects (`AssetPaths`)
- [x] `Core/Interfaces` — Repositories (+ `IUnitOfWork`), Services, Infrastructure
- [x] `Core/Models` — Requests, Responses, ViewModels, Jobs
- [x] `Infrastructure/Persistence` — DbContext, UnitOfWork, Configurations, Repositories
- [x] `Infrastructure/Storage` — `LocalAssetStorageService`, WorkflowAssetStorageService, constants
- [x] `Infrastructure/Email` — MailKit + FormAccessEmailComposer
- [x] `Infrastructure/GitHub` — Dispatch + WorkflowJobStore
- [x] `Application/Services` + `Application/Validation` + AssetUpload + BackgroundJobs
- [x] `Web/` — Controllers, Filters, TagHelpers, Helpers, Middleware
- [x] `Options/` — FormAccess, Mailgun, GitHub*, StorageOptions
- [x] Form-submit rate limit aligned to **5/min** (plan Phase 1)
- [x] `docs/TESTING.md` + `.github/workflows/ci.yml`
- [x] `docs/ARCHITECTURE.md` updated to Part 2.2 tree
- [x] Legacy SmtpClient `EmailService` removed

## Manual follow-up

```powershell
cd MobileAppDeployment
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=SystenicsAppDeployment;Username=postgres;Password=YOUR_PASSWORD;"
dotnet user-secrets set "Identity:AdminEmail" "admin@yourdomain.com"
dotnet user-secrets set "Identity:AdminPassword" "YourSecureP@ssw0rd12"
dotnet ef database update
```
