# Architecture Overview

This document describes the system design, folder structure, and key technical decisions for **MobileAppDeployment** — an ASP.NET Core MVC application that lets internal administrators issue client deployment form links and trigger GitHub Actions workflows to build and publish mobile apps.

---

## Table of Contents

1. [System purpose](#system-purpose)
2. [Architecture style](#architecture-style)
3. [Folder structure](#folder-structure)
4. [Layers and responsibilities](#layers-and-responsibilities)
5. [Key flows](#key-flows)
6. [Background processing](#background-processing)
7. [Security model](#security-model)
8. [External dependencies](#external-dependencies)
9. [Configuration reference](#configuration-reference)
10. [Future roadmap](#future-roadmap)

---

## System purpose

The application has two distinct user roles:

| Role | Who | What they do |
|------|-----|-------------|
| **Admin** | Systenics internal team | Issues magic-link tokens via API; monitors deployments |
| **Client** | App owner (external) | Fills in the deployment form (app name, bundle IDs, Firebase files, assets) |

When a client submits the form, the admin can trigger a GitHub Actions workflow that creates a new mobile app repository, builds the app, and deploys it to the App Store and Play Store.

---

## Architecture style

The project uses **folder-based layered architecture** inside a single ASP.NET Core project (Part 2.2 of the enterprise plan) — clear separation without multi-project Clean Architecture ceremony:

```
┌──────────────────────────────────────────────────────────────────────┐
│  Presentation (Web/)                                                 │
│  Controllers · Filters · TagHelpers · Helpers · Middleware · Views/  │
├──────────────────────────────────────────────────────────────────────┤
│  Application/                                                        │
│  Services · Validation · AssetUpload · BackgroundJobs                │
├──────────────────────────────────────────────────────────────────────┤
│  Core/                                                               │
│  Domain (Entities, Enums, ValueObjects) · Interfaces · Models        │
├──────────────────────────────────────────────────────────────────────┤
│  Infrastructure/                                                     │
│  Persistence (+ Repositories, UoW) · Storage · Email · GitHub · …    │
└──────────────────────────────────────────────────────────────────────┘
```

**Dependency direction:** Upper layers depend on lower layers via interfaces. Lower layers never import controllers/views.

---

## Folder structure

```
MobileAppDeployment/
├── Core/
│   ├── Domain/
│   │   ├── Entities/                # AppDeployment, FormAccessToken
│   │   ├── Enums/                   # WorkflowJobStatus
│   │   └── ValueObjects/            # AssetPaths (path grouping helper)
│   ├── Interfaces/
│   │   ├── Repositories/            # IAppDeploymentRepository, IFormAccessTokenRepository, IUnitOfWork
│   │   ├── Services/                # Application service contracts
│   │   └── Infrastructure/        # IGitHubWorkflowDispatchService, IWorkflowJobStore
│   └── Models/
│       ├── Requests/ · Responses/ · ViewModels/ · Jobs/
├── Application/
│   ├── Services/                    # AppDeployment, FormAccessToken, WorkflowOrchestration
│   ├── Validation/                  # Save/deploy validators, AssetImageValidator, AssetUploadSpec
│   ├── AssetUpload/                 # IAssetUploadStrategy
│   └── BackgroundJobs/              # Channel + BackgroundService for workflow dispatch
├── Infrastructure/
│   ├── Persistence/                 # ApplicationDbContext, UnitOfWork, Configurations, Repositories
│   ├── Storage/                     # LocalAssetStorageService, WorkflowAssetStorageService
│   ├── Email/                       # MailKitEmailService, FormAccessEmailComposer
│   ├── GitHub/                      # Dispatch service + WorkflowJobStore
│   ├── Security/ · HealthChecks/ · Identity/
├── Web/
│   ├── Controllers/                 # MVC + API (Account, AppDeployment, Workflow, FormAccessTokens, Home)
│   ├── Filters/                     # ApiKeyAuthorizationFilter, ValidateAntiForgeryTokenFilter
│   ├── TagHelpers/ · Helpers/ · Middleware/
├── Options/                         # FormAccess, Mailgun, GitHub, StorageOptions
├── Extensions/                      # DI + rate limiting + middleware helpers
├── Migrations/                      # Kept at project root (safer for EF tooling)
├── Views/ · wwwroot/ · Scripts/
└── Program.cs
```

`MobileAppDeployment.Tests/` is a sibling project under the solution root.

---

## Layers and responsibilities

### Presentation Layer

**Controllers** are thin — they validate input, call one service method, and return a view or redirect. Business logic never lives in a controller.

**Middleware** (`Web/Middleware/`) handles cross-cutting concerns:
- `SecurityHeadersMiddleware` — adds defensive HTTP headers to every response
- `GlobalExceptionMiddleware` — catches unhandled exceptions; returns RFC 7807 Problem Details for API callers; redirects HTML clients to the Error view

### Application Layer

**Services** implement business logic above the repository layer. They coordinate between repositories, email, and external services.

**BackgroundJobs** (`Application/BackgroundJobs/`) implement the background dispatch pipeline:
- `WorkflowDispatchChannel` — a bounded `Channel<T>` (capacity 200) that decouples the request thread from GitHub API calls
- `WorkflowDispatchBackgroundService` — consumes the channel, calls GitHub API with retry, updates `IWorkflowJobStore`

### Infrastructure Layer

**Repositories** provide data access. All queries pass `CancellationToken` so PostgreSQL commands are cancelled when a client disconnects.

**EF Configuration** (`Infrastructure/Persistence/Configurations/`) — each entity has its own `IEntityTypeConfiguration<T>` class with fully documented design decisions.

**Email** (`Infrastructure/Email/MailKitEmailService.cs`) — MailKit 4.16+ replaces the deprecated `System.Net.Mail.SmtpClient`. Uses STARTTLS (never falls back to plaintext). Sends multipart/alternative (HTML + plain text fallback).

---

## Key flows

### Flow 1: Admin issues a form token

```
Admin (Postman/curl)
  → POST /api/form-access-tokens
    [X-Api-Key header]
  → FormAccessTokensController.Issue()
  → IFormAccessTokenService.IssueAsync()
    → Checks for existing active token (same client+app name)
    → Creates new token if none exists
    → (Optional) IFormAccessEmailComposer.SendFormLinkAsync()
  → Returns { token, formUrl }
```

### Flow 2: Client fills in the deployment form

```
Client (browser)
  → GET /AppDeployment/Create?token={token}
  → FormAccessToken validated
  → Client fills form, uploads files
  → POST /AppDeployment/Create
    [ValidateAntiForgeryToken + rate limiting]
  → AppDeploymentController.Create()
  → IAppDeploymentService.SaveAsync()
    → IAssetStorageService.SaveAssetAsync() [per uploaded file]
    → IAppDeploymentRepository.InsertAsync()
  → Redirect to success / workflow trigger page
```

### Flow 3: Admin triggers workflow

```
Admin clicks "Start Workflow"
  → POST /AppDeployment/TriggerWorkflow/{id}
  → IWorkflowOrchestrationService.StartWorkflowJobFromDeploymentAsync()
    Phase 1 (request thread):
      → IWorkflowAssetStorageService.PublishStoredFileAndGetPublicUrlAsync()
        [copies assets to public workflow-assets/ folder]
      → WorkflowDispatchChannel.WriteAsync(WorkflowDispatchWorkItem)
    Phase 2 (background):
      → WorkflowDispatchBackgroundService reads the channel
      → Creates DI scope
      → IGitHubWorkflowDispatchService.TriggerAsync() [with retry]
      → IWorkflowJobStore.MarkCompleted() or MarkFailed()
  → Admin browser polls GET /AppDeployment/WorkflowStatus/{jobId}
  → Progress bar updates in real time
```

---

## Background processing

The previous implementation used `_ = Task.Run(async () => ...)` which has critical production problems:

| Problem | Task.Run | Channel + BackgroundService |
|---------|----------|---------------------------|
| Graceful shutdown | No — task orphaned on stop | Yes — CancellationToken signalled |
| DI scope | Wrong — DbContext shared across threads | Correct — new scope per work item |
| Back-pressure | None — unlimited Task.Run calls | Bounded channel (capacity 200) |
| Error handling | Silently swallowed | Logged; loop continues |
| Testability | Cannot be unit-tested | Channel + service are injectable |

The channel is bounded at 200 items with `BoundedChannelFullMode.Wait` — producers (HTTP requests) block briefly if the channel is full rather than silently dropping work.

---

## Security model

### Authentication

| Endpoint | Auth mechanism |
|----------|---------------|
| `POST /api/form-access-tokens` | `X-Api-Key` header checked against `FormAccess:ApiKey` config |
| `GET /AppDeployment/Create?token=` | Opaque token (GUID) validated in DB; single-use or time-limited |
| All other admin actions | Currently unprotected — Phase 5 adds ASP.NET Core Identity |

### Security headers (every response)

| Header | Value | Mitigates |
|--------|-------|-----------|
| `X-Frame-Options` | `DENY` | Clickjacking |
| `X-Content-Type-Options` | `nosniff` | MIME sniffing |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Referrer leakage |
| `Permissions-Policy` | camera/mic/geo/payment disabled | Feature abuse |
| `Content-Security-Policy` | Restricted to self + Google Fonts | XSS |

### Rate limiting (per IP, sliding window)

| Policy | Limit | Applied to |
|--------|-------|-----------|
| `token-api` | 10 req/min | Token issuance API |
| `form-submit` | 20 req/min | Create/Edit POST actions |
| `global` | 200 req/min | All endpoints |

### File uploads

- Extension validated against an explicit allow-list (never wildcard)
- Destination path canonicalized with `Path.GetFullPath()` and verified to stay inside the permitted upload directory
- Triggered by a critical log + exception if traversal is detected

### Secrets

All credentials are stored in `dotnet user-secrets` (development) or environment variables (production). The committed `appsettings.json` and `appsettings.Development.json` contain only non-sensitive defaults.

---

## External dependencies

| Service | Purpose | Config section |
|---------|---------|----------------|
| **PostgreSQL** | Primary database | `ConnectionStrings:DefaultConnection` |
| **Mailgun** (SMTP) | Transactional email (form link delivery) | `MailgunSMTP` |
| **GitHub API** | Repository creation + workflow dispatch | `GitHub` |
| **OneSignal** | Push notifications (config passed to workflow) | Via form fields, not config |

---

## Configuration reference

See [DEVELOPMENT-SETUP.md](DEVELOPMENT-SETUP.md) for the full list of required secrets and how to set them.

Key non-secret configuration:

| Key | Default | Purpose |
|-----|---------|---------|
| `GitHub:WorkflowDispatch:MaxRetries` | `3` | Dispatch attempts before marking failed |
| `GitHub:WorkflowDispatch:RetryDelaySeconds` | `5` | Seconds between retries |
| `GitHub:WorkflowDispatch:Owner` | `systenics` | GitHub org for workflow dispatch |
| `GitHub:WorkflowDispatch:Repository` | `SA_BaseMVCProject` | Target repository |
| `GitHub:WorkflowDispatch:Workflow` | `main.yml` | Workflow file name |

---

## Future roadmap

| Phase | Feature | Status |
|-------|---------|--------|
| Phase 5 | ASP.NET Core Identity (single admin user) | Planned |
| Phase 6 | Polly retry + circuit breaker for GitHub API | Planned |
| Phase 7 | Full API documentation (Swagger/OpenAPI) | Planned |
| Phase 8 | Unit + integration tests | Planned |
| Phase 9 | Docker + docker-compose for local dev | Planned |
| Phase 10 | Cloud asset storage (Azure Blob / S3) | Planned |

---

## See also

- [DEVELOPMENT-SETUP.md](DEVELOPMENT-SETUP.md) — Local machine setup
- [ENTITY-FRAMEWORK-CORE-GUIDE.md](ENTITY-FRAMEWORK-CORE-GUIDE.md) — Working with the database
