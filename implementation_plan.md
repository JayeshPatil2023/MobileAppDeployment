# Enterprise Refactoring Plan — MobileAppDeployment

## Summary

This document is the **complete technical blueprint** for refactoring the existing `MobileAppDeployment` ASP.NET Core MVC application into a production-ready, enterprise-grade system. The plan covers architecture, code quality, security, observability, testability, and documentation — with a fully justified rationale for every decision.

---

## Part 1 — Current State Analysis

### 1.1 Project Overview

**Technology Stack**

- ASP.NET Core MVC (.NET 8)
- PostgreSQL via EF Core 8 (Npgsql)
- Bootstrap + vanilla CSS + jQuery
- Mailgun SMTP for email
- GitHub Actions REST API for workflow dispatch

**Domain Purpose**
The application is an internal **mobile-app store-submission portal**. An admin issues a client a unique magic-link token. The client fills in a large deployment form (SSL certificate details, Apple/Google store metadata, app assets/icons, push notification credentials, Firebase config files). Once complete, the admin triggers a **GitHub Actions workflow** that automates the app build & store submission pipeline.

**Existing Layers (flat, single-project)**

```
MobileAppDeployment/
├── Controllers/            ← MVC + API controllers
├── Data/                   ← DbContext only
├── Helpers/                ← Validation, image parsing, UI helpers
├── Migrations/             ← EF Core migrations (9 migrations)
├── Models/                 ← Domain entities + DTOs mixed together
├── Options/                ← Configuration POCO classes
├── Repositories/           ← EF repository implementations + interfaces
├── Services/               ← Service implementations + interfaces + GitHub subfolder
│   └── GitHub/             ← GitHub dispatch, job store, orchestration
├── TagHelpers/             ← Field-help Razor tag helper
├── Views/                  ← Razor views (14 partials for AppDeployment alone)
├── wwwroot/                ← Static files, uploaded assets
└── Scripts/                ← PowerShell, YAML, SQL scripts (DevOps tooling)
```

---



### 1.2 What the Project Does Well

Before listing problems, it is important to acknowledge what is already done correctly — these decisions will be **preserved**:


| Area                     | Strength                                                                    |
| ------------------------ | --------------------------------------------------------------------------- |
| Repository Pattern       | Interfaces exist for all repositories; DI-registered correctly              |
| Service Layer            | Services delegate to repositories cleanly; no business logic in controllers |
| Async/Await              | All I/O operations are properly `async`/`await`                             |
| XML docs                 | All public members have `<summary>` comments                                |
| Constant-time comparison | API key check uses `CryptographicOperations.FixedTimeEquals`                |
| Token entropy            | 32-byte `RandomNumberGenerator` tokens (256 bits)                           |
| Image validation         | Binary header parsing (no third-party imaging library)                      |
| Options pattern          | All config bound via `IOptions<T>` — no raw `IConfiguration` in services    |
| Fire-and-forget          | `IServiceScopeFactory` used correctly for background dispatch               |
| HTML encoding            | `WebUtility.HtmlEncode` on all user values in email bodies                  |


---



### 1.3 Identified Problems — Detailed Analysis



#### 🔴 CRITICAL — Security


| #   | Problem                                                                                                                                                               | Location                             | Risk                |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------ | ------------------- |
| S1  | **Real secrets committed to** `appsettings.Development.json` — GitHub PAT (`ghp_...`), SMTP password (`JupiteR123@`), API key (`mad-admin-form-key-7f3c9e2a1b8d4f6e`) | `appsettings.Development.json`       | Credential exposure |
| S2  | **No authentication whatsoever** — Index, Details, Delete, Edit (admin path) are fully public                                                                         | `AppDeploymentController`            | Data breach         |
| S3  | `OneSignalRestApiKey` **stored in plaintext** in PostgreSQL `text` column and passed through form                                                                     | `AppDeployment.cs`, DB               | Secret leakage      |
| S4  | **Path traversal risk** in `AssetStorageService` — extension is extracted from user-supplied filename without canonicalization                                        | `AssetStorageService.cs:L19`         | Directory traversal |
| S5  | **Legacy** `SmtpClient` is deprecated, not suitable for modern async usage, and does not pool connections                                                             | `EmailService.cs`                    | Reliability         |
| S6  | **No rate limiting** on token issuance or form submission endpoints                                                                                                   | All controllers                      | Abuse               |
| S7  | **Connection string contains credentials** in `appsettings.json` committed to git                                                                                     | `appsettings.json:L3`                | DB credential leak  |
| S8  | **API key header check exposes timing via length mismatch short-circuit** (partially mitigated but length still leaks)                                                | `FormAccessTokensController.cs:L171` | Timing attack       |
| S9  | **No Content Security Policy, HSTS headers, X-Frame-Options** etc.                                                                                                    | `Program.cs`                         | Clickjacking, XSS   |
| S10 | `OneSignalRestApiKey` **is in ModelState** during full validation, risking exposure in error responses or logging                                                     | `AppDeploymentValidation.cs`         | Secret leakage      |




#### 🟠 HIGH — Architecture & Design


| #   | Problem                                                                                                                                                                                                                                      | Location                                   | Impact              |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ | ------------------- |
| A1  | `AppDeploymentController` **is a God Controller** — 597 lines, 9 injected services, 8-parameter file upload methods, business logic (path preservation, `TruncateForAppName`), validation orchestration, token resolution, workflow dispatch | `AppDeploymentController.cs`               | Untestable, fragile |
| A2  | **Domain entity used as MVC model, DTO, and persistence entity** — `AppDeployment` is bound directly from form posts, stored in DB, and returned to views with no separation                                                                 | `AppDeployment.cs`                         | Tight coupling      |
| A3  | `AppDeploymentService` **is a pass-through** — zero business logic, only delegates to repository. The service abstraction exists but adds no value                                                                                           | `AppDeploymentService.cs`                  | False abstraction   |
| A4  | `WorkflowOrchestrationService` **directly references** `AppDeployment` **domain model** — GitHub integration layer is coupled to the top-level domain model                                                                                  | `WorkflowOrchestrationService.cs:L100`     | Circular coupling   |
| A5  | **No Unit of Work** — each repository calls `SaveChangesAsync()` independently, making multi-repository operations non-atomic                                                                                                                | Repositories                               | Data inconsistency  |
| A6  | `WorkflowJobStore` **is in-memory Singleton** — jobs are lost on server restart; no cleanup/eviction of old jobs causing unbounded memory growth                                                                                             | `WorkflowJobStore.cs`                      | Memory leak in prod |
| A7  | **Mixed concerns in** `Helpers/` — image validation, HTML extensions, field help texts, upload specs, model validation — completely different responsibilities in one flat folder                                                            | `Helpers/`                                 | Confusion           |
| A8  | `Task.Run` **fire-and-forget** with no structured supervision, no cancellation, no failure escalation path beyond in-memory job state                                                                                                        | `WorkflowOrchestrationService.cs:L88`      | Silent failure      |
| A9  | **Static classes for validation** (`AppDeploymentValidation`, `AssetImageValidator`, `AssetUploadSpec`) — cannot be injected, mocked, or replaced                                                                                            | `Helpers/`                                 | Untestable          |
| A10 | **Options classes in wrong namespaces** — `GitHubOptions` and `GitHubWorkflowDispatchOptions` are in `Services.GitHub` namespace instead of `Options`                                                                                        | Multiple                                   | Leaky abstraction   |
| A11 | `Program.cs` **is a flat registration dump** — 20+ service registrations with no grouping or extension methods                                                                                                                               | `Program.cs`                               | Onboarding friction |
| A12 | **No middleware pipeline configuration** — exception handling, security headers, request logging all missing                                                                                                                                 | `Program.cs`                               | Prod unreadiness    |
| A13 | `WorkflowOrchestrationService` **resolves its own dependencies from DI scope manually** — anti-pattern in an already-scoped service                                                                                                          | `WorkflowOrchestrationService.cs:L169-175` | Fragile             |




#### 🟡 MEDIUM — Maintainability & Code Quality


| #   | Problem                                                                                                                                                             | Location                                                        | Impact            |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- | ----------------- |
| M1  | **No global exception handler** — unhandled exceptions in production expose stack traces                                                                            | `Program.cs`                                                    | Data exposure     |
| M2  | **No centralized error response format** — `BadRequest()`, `NotFound()`, `Unauthorized()` return inconsistent response bodies                                       | Controllers                                                     | API inconsistency |
| M3  | **No input model validation centralization** — `[ValidateAntiForgeryToken]` + manual ModelState checks + custom validators scattered across controller actions      | Multiple                                                        | DRY violation     |
| M4  | **Hard-coded file paths** — `"mobile-app-icon"`, `"launch-image"`, `"logo"`, `"splash"` etc. are string literals spread across controller and orchestration service | `AppDeploymentController.cs`, `WorkflowOrchestrationService.cs` | Magic strings     |
| M5  | `AppDeploymentController.PreserveExistingPaths` — a 10-property assignment block that will silently break when new asset fields are added                           | `AppDeploymentController.cs:L524`                               | Fragile           |
| M6  | **No mapping layer** — business model → view model mapping done by `deployment.OneSignalRestApiKey = string.Empty;` in-place mutation on the loaded entity          | Controller                                                      | Data mutation     |
| M7  | `SaveUploadedFilesAsync` — 8-parameter method that grows linearly with each new asset field                                                                         | `AppDeploymentController.cs:L544`                               | Shotgun surgery   |
| M8  | `NormalizeNonNullableStringsForPartialSave` — 25-line null-coalescing block that duplicates all non-nullable property names from the model                          | `AppDeploymentValidation.cs:L111`                               | DRY violation     |
| M9  | **No fluent EF configuration** — all entity configuration is in `OnModelCreating` inline lambdas rather than `IEntityTypeConfiguration<T>` classes                  | `ApplicationDbContext.cs`                                       | Scalability       |
| M10 | `FormAccessTokensController.BuildFormUrl` — URL building directly in controller; hard to test                                                                       | `FormAccessTokensController.cs:L149`                            | Untestable        |
| M11 | `SmtpClient` **created per-send** — new TCP connection for every email                                                                                              | `EmailService.cs:L51`                                           | Performance       |
| M12 | **GitHub dispatch options** have defaults hard-coded to specific client values (`BidMaster`, `systenics`, `AuctionPro_05`)                                          | `GitHubWorkflowDispatchOptions.cs`                              | Environment bleed |
| M13 | **No** `CancellationToken` **propagation** in repository methods                                                                                                    | Repositories                                                    | Graceful shutdown |
| M14 | **No health checks**                                                                                                                                                | Entire project                                                  | Ops blind spot    |
| M15 | **No test project**                                                                                                                                                 | Solution                                                        | Quality           |




#### 🟢 LOW — Future-Proofing


| #   | Problem                                                                          | Location                          | Impact           |
| --- | -------------------------------------------------------------------------------- | --------------------------------- | ---------------- |
| F1  | Single-project, flat structure — difficult to extract into microservices         | Entire project                    | Scalability      |
| F2  | No versioned API routes — `api/form-access-tokens` has no version prefix         | `FormAccessTokensController`      | Breaking changes |
| F3  | File storage is local filesystem (`wwwroot/uploads`) — cannot scale horizontally | `AssetStorageService.cs`          | Scale limit      |
| F4  | No background job infrastructure — `Task.Run` is not a job queue                 | `WorkflowOrchestrationService.cs` | Reliability      |
| F5  | No caching layer                                                                 | Services                          | Performance      |
| F6  | No structured logging format                                                     | `Program.cs`                      | Observability    |


---



## Part 2 — Proposed Architecture



### 2.1 Architectural Decisions & Justifications



#### Decision 1: Stay Single-Project, Adopt Layered Folder Architecture

**Why not Clean Architecture with multiple projects?**
The domain is compact (2 entities, ~5 services). Splitting into `Domain`, `Application`, `Infrastructure`, and `Web` projects adds significant ceremony (cross-project `IFormFile` sharing issues, interface duplication) for minimal gain. The single-project approach with **strict folder-based layering** gives the same separation of concerns with far less friction for a small team.

**Decision:** Use a well-structured single-project with clearly named root folders enforcing layered discipline by convention.

#### Decision 2: Introduce Thin Application Services (CQRS-lite) — No MediatR

**Why no MediatR?**
MediatR would force every action into a handler + request + response triple. With only ~10 operations, this triples the file count with no architectural benefit. The overhead-to-value ratio is negative for this codebase.

**Decision:** Keep the existing service pattern but make services genuinely behavioral (not pass-through), introduce explicit **command** and **query** separation within service methods by naming convention (`GetXxxAsync` for queries, `CreateXxx`, `UpdateXxx`, `DeleteXxx` for commands).

#### Decision 3: Add ViewModel / DTO Layer (no AutoMapper)

**Problem:** Domain entity `AppDeployment` is mutated in-place (`deployment.OneSignalRestApiKey = string.Empty`) before being passed to views.  
**Solution:** Introduce explicit `AppDeploymentFormViewModel` for form binding, `AppDeploymentListItemDto` for index, and `AppDeploymentDetailsDto` for detail view. Mapping is done via explicit static factory methods — no AutoMapper (adds complexity and hides mapping bugs).

#### Decision 4: Repository Pattern + Unit of Work (scoped)

**Justification:** The token issuance flow needs to link a token to a deployment atomically. Currently two repositories each call `SaveChangesAsync()` independently. Introducing a lightweight `IUnitOfWork` that wraps the shared `DbContext.SaveChangesAsync()` allows atomic multi-repository operations.

#### Decision 5: No CQRS, No Event Sourcing

Overkill for this domain. The system is a data-entry form with a GitHub API call — not an event-driven order processing system. Introducing event sourcing would dramatically increase complexity with no benefit.

#### Decision 6: Extract Asset Upload into a dedicated Strategy

The 8-parameter `SaveUploadedFilesAsync` will be replaced by an `IAssetUploadStrategy` that maps upload field names to storage keys using the existing `AssetUploadSpec` catalog. This eliminates the shotgun surgery problem.

#### Decision 7: Replace `Task.Run` with `IHostedService` / Channel

The fire-and-forget GitHub dispatch will be moved to a proper `BackgroundService` that consumes a `Channel<DispatchWorkItem>`. This provides graceful shutdown, structured error handling, and eliminates the manual `IServiceScopeFactory` scope hack.

#### Decision 8: Replace `SmtpClient` with `MailKit`

`System.Net.Mail.SmtpClient` is officially deprecated. `MailKit` is the recommended replacement: async-native, connection pooling, proper cancellation token support.

#### Decision 9: Add ASP.NET Core Identity (lightweight) for Admin area

Currently any unauthenticated request can access Index, Details, Delete, Edit. This will be secured with cookie-based ASP.NET Core Identity (no external providers required — single admin user is sufficient). Client token-gated routes remain public.

#### Decision 10: Add Proper Secret Management

Secrets will be moved to:

- `dotnet user-secrets` for local development
- Environment variables for production (documented)
- `.gitignore` updated to prevent `appsettings.Development.json` commits with secrets

---



### 2.2 Target Folder Structure

```
MobileAppDeployment/
│
├── Core/                                   ← Domain + Application contracts
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── AppDeployment.cs            (pure domain entity, no annotations)
│   │   │   └── FormAccessToken.cs
│   │   ├── Enums/
│   │   │   └── WorkflowJobStatus.cs
│   │   └── ValueObjects/
│   │       └── AssetPaths.cs               (groups all 8 asset paths)
│   │
│   ├── Interfaces/
│   │   ├── Repositories/
│   │   │   ├── IAppDeploymentRepository.cs
│   │   │   ├── IFormAccessTokenRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   ├── Services/
│   │   │   ├── IAppDeploymentService.cs
│   │   │   ├── IFormAccessTokenService.cs
│   │   │   ├── IAssetStorageService.cs
│   │   │   ├── IEmailService.cs
│   │   │   └── IWorkflowOrchestrationService.cs
│   │   └── Infrastructure/
│   │       ├── IGitHubWorkflowDispatchService.cs
│   │       └── IWorkflowJobStore.cs
│   │
│   └── Models/                             ← DTOs, view models, request/response
│       ├── Requests/
│       │   ├── CreateFormAccessTokenRequest.cs
│       │   └── WorkflowDispatchRequest.cs
│       ├── Responses/
│       │   ├── FormAccessTokenResponse.cs
│       │   ├── EmailSendResult.cs
│       │   └── GitHubWorkflowDispatchResult.cs
│       ├── ViewModels/
│       │   ├── AppDeploymentFormViewModel.cs   ← form binding
│       │   ├── AppDeploymentListItemViewModel.cs
│       │   ├── AppDeploymentDetailsViewModel.cs
│       │   └── WorkflowProgressViewModel.cs
│       └── Jobs/
│           └── WorkflowJobState.cs
│
├── Infrastructure/                          ← EF, file storage, email, GitHub
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs
│   │   ├── UnitOfWork.cs
│   │   ├── Configurations/
│   │   │   ├── AppDeploymentConfiguration.cs  ← IEntityTypeConfiguration<T>
│   │   │   └── FormAccessTokenConfiguration.cs
│   │   ├── Repositories/
│   │   │   ├── AppDeploymentRepository.cs
│   │   │   └── FormAccessTokenRepository.cs
│   │   └── Migrations/                     (unchanged)
│   │
│   ├── Storage/
│   │   ├── LocalAssetStorageService.cs     ← replaces AssetStorageService
│   │   ├── WorkflowAssetStorageService.cs
│   │   └── AssetStorageConstants.cs        ← replaces magic strings
│   │
│   ├── Email/
│   │   ├── MailKitEmailService.cs          ← replaces SmtpClient
│   │   ├── FormAccessEmailComposer.cs
│   │   └── Templates/
│   │       └── FormAccessLinkTemplate.cs
│   │
│   └── GitHub/
│       ├── GitHubWorkflowDispatchService.cs
│       ├── WorkflowJobStore.cs
│       └── Options/
│           ├── GitHubOptions.cs
│           └── GitHubWorkflowDispatchOptions.cs
│
├── Application/                             ← Service implementations
│   ├── Services/
│   │   ├── AppDeploymentService.cs
│   │   ├── FormAccessTokenService.cs
│   │   └── WorkflowOrchestrationService.cs
│   │
│   ├── BackgroundJobs/
│   │   ├── WorkflowDispatchBackgroundService.cs ← replaces Task.Run
│   │   └── WorkflowDispatchChannel.cs
│   │
│   └── Validation/
│       ├── AppDeploymentSaveValidator.cs   ← FluentValidation or custom
│       ├── AppDeploymentDeployValidator.cs
│       ├── AssetImageValidator.cs
│       └── AssetUploadSpec.cs
│
├── Web/                                     ← MVC presentation layer
│   ├── Controllers/
│   │   ├── AppDeploymentController.cs      ← refactored, thin
│   │   ├── FormAccessTokensController.cs
│   │   └── HomeController.cs
│   ├── Filters/
│   │   ├── ApiKeyAuthorizationFilter.cs    ← extracts key auth from controller
│   │   └── ValidateAntiForgeryTokenFilter.cs
│   ├── TagHelpers/
│   │   └── FieldHelpTagHelper.cs
│   ├── Helpers/
│   │   ├── FieldHelpTexts.cs
│   │   └── UploadZoneHtmlExtensions.cs
│   └── Middleware/
│       ├── GlobalExceptionMiddleware.cs
│       └── SecurityHeadersMiddleware.cs
│
├── Options/                                 ← All configuration POCOs
│   ├── FormAccessOptions.cs
│   ├── MailgunSmtpOptions.cs
│   ├── GitHubOptions.cs
│   └── GitHubWorkflowDispatchOptions.cs
│
├── Extensions/                              ← IServiceCollection extensions
│   ├── ServiceCollectionExtensions.cs      ← groups DI registrations
│   └── WebApplicationExtensions.cs         ← middleware pipeline helpers
│
├── Views/                                   (unchanged structure)
├── wwwroot/                                 (unchanged)
├── Scripts/                                 (unchanged)
├── Program.cs                              ← thin: only calls extension methods
└── appsettings.json                        ← no secrets
```

---



### 2.3 Layer Responsibilities


| Layer               | Responsibility                                       | Must NOT                                 |
| ------------------- | ---------------------------------------------------- | ---------------------------------------- |
| **Core/Domain**     | Entities, enums, value objects                       | Reference EF, ASP.NET, or infrastructure |
| **Core/Interfaces** | Contracts for services, repos, infrastructure        | Contain implementations                  |
| **Core/Models**     | DTOs, ViewModels, request/response objects           | Contain business logic                   |
| **Infrastructure**  | EF, file I/O, email, GitHub API                      | Reference Web layer                      |
| **Application**     | Service implementations, background jobs, validators | Reference Web/MVC types                  |
| **Web**             | Controllers, filters, tag helpers, middleware        | Contain business logic                   |
| **Options**         | Config POCOs                                         | Depend on any other layer                |
| **Extensions**      | DI registration, pipeline configuration              | Contain business logic                   |


---



## Part 3 — Incremental Implementation Plan

The refactoring will be done **incrementally, one module at a time**, ensuring the app remains buildable and testable at each step.

---



### Phase 1 — Security Hardening (Immediate, No Architecture Changes)

*Estimated scope: 5–8 files changed*

1. **[S1, S7] Remove secrets from config files**
  - Delete real values from `appsettings.Development.json`
  - Add `.gitignore` entry for `appsettings.Development.json`
  - Document `dotnet user-secrets` setup in README
2. **[S3] OneSignalRestApiKey — document as plaintext limitation, add comment**
  - Full encryption at rest is a Phase 4 enhancement
  - Add `[DataType(DataType.Password)]` already exists ✓
  - Ensure logging never logs this field — add structured log suppression
3. **[S9] Add Security Headers Middleware**
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Content-Security-Policy` (scoped to Bootstrap/jQuery CDNs)
4. **[S6] Add Rate Limiting**
  - .NET 8 `RateLimiter` middleware
  - Token issuance endpoint: 10 req/min per IP
  - Form submit: 5 req/min per IP
5. **[S4] Fix path traversal in AssetStorageService**
  - Use only the extension (already done) — but canonicalize with `Path.GetFullPath` and verify it stays within upload directory

---



### Phase 2 — Infrastructure Layer Extraction

*Estimated scope: 15–20 files*

1. **Extract EF Entity Configuration** — one `IEntityTypeConfiguration<T>` class per entity
2. **Introduce** `IUnitOfWork` — wrap `DbContext.SaveChangesAsync()`
3. **Add** `CancellationToken` **to all repository methods**
4. **Move Options classes** to top-level `Options/` folder
5. **Centralize service registration** via `IServiceCollection` extension methods
6. **Replace** `SmtpClient` **with** `MailKit`
7. **Create** `AssetStorageConstants` — eliminate all magic string literals

---



### Phase 3 — Application Layer Refactoring

*Estimated scope: 20–25 files*

1. **Introduce ViewModels** — separate form binding models from domain entities
2. **Refactor** `AppDeploymentService` — add real business logic (currently pass-through)
3. **Introduce** `IAssetUploadStrategy` — Strategy Pattern to replace 8-parameter method
4. **Extract** `ApiKeyAuthorizationFilter` — remove auth logic from `FormAccessTokensController`
5. **Replace** `Task.Run` **with** `Channel<T>` **+** `BackgroundService`
6. **Fix** `WorkflowJobStore` **memory leak** — add time-based eviction

---



### Phase 4 — Controller Slimming & ViewModel Mapping

*Estimated scope: 10–15 files*

1. **Split** `AppDeploymentController` — reduce from 597 lines to ~200
2. **Extract** `WorkflowController` — progress polling actions into own controller
3. **Add explicit ViewModel → Entity mapping** via factory methods
4. **Add** `GlobalExceptionMiddleware` — consistent error responses
5. **Add Health Checks** — DB ping, file system writability check

---



### Phase 5 — Authentication & Authorization

*Estimated scope: 10–12 files*

1. **Add ASP.NET Core Identity** (admin user only, cookie auth)
2. **Protect admin routes** — Index, Details, Delete, Edit (non-token path)
3. **Separate admin and client controller paths** — `[Authorize]` on admin actions
4. **Document auth setup** in README

---



### Phase 6 — Testing Infrastructure

*Estimated scope: New test project, 20–30 test files*

1. **Add** `MobileAppDeployment.Tests` **xUnit project**
2. **Unit tests** — validators, service logic, token generation, URL building
3. **Integration tests** — using `WebApplicationFactory<Program>`, in-memory SQLite
4. **Add CI test step** to GitHub Actions

---



### Phase 7 — Comprehensive Documentation

*Estimated scope: 10–15 markdown files in* `docs/`

1. `ARCHITECTURE.md` — diagrams, layer responsibilities, decision log
2. `API.md` — full API reference with examples
3. `DEVELOPMENT-SETUP.md` — local setup, secrets, first run
4. `DEPLOYMENT.md` — environment configuration, secrets, health checks
5. `TESTING.md` — running tests, test strategy, coverage
6. `SECURITY.md` — threat model, secrets management, auth flow
7. `CONTRIBUTING.md` — coding standards, naming conventions, PR checklist
8. Update existing `ENTITY-FRAMEWORK-CORE-GUIDE.md` with new paths

---



## Part 4 — Key Design Decisions per File



### `AppDeploymentController.cs` (597 → ~200 lines)

**Current problems:**

- 4 injected services (can be reduced)
- `SaveUploadedFilesAsync` — 8 `IFormFile` parameters
- `PreserveExistingPaths` — 10 property assignments
- `TruncateForAppName` — business logic in controller
- Business-rule: `deployment.OneSignalRestApiKey = string.Empty` — mutation on entity

**Proposed:**

- Inject only `IAppDeploymentService`, `IFormAccessTokenService`, `IWorkflowOrchestrationService`
- `IAssetStorageService` absorbed into `IAppDeploymentService.SaveAsync(command)`
- Upload asset handling delegated to `AssetUploadCommand` object processed by service
- ViewModels used for binding — no entity mutation in controller
- `TruncateForAppName` moved to `AppDeploymentFormViewModel` factory method



### `WorkflowOrchestrationService.cs` (336 → ~150 lines)

**Current problems:**

- Accepts `AppDeployment` entity (cross-layer coupling)
- Manual `IServiceScopeFactory` scope in fire-and-forget
- Duplicate `ValidateDeployment`/`ValidateRequest` logic

**Proposed:**

- Accepts `WorkflowStartCommand` (not the entity)
- Publishes to `Channel<DispatchWorkItem>` instead of `Task.Run`
- `BackgroundService` consumes the channel with proper cancellation



### `AppDeployment.cs` (domain entity)

**Current problems:**

- `[Required]`, `[Display]`, `[StringLength]` data annotations — MVC concerns on domain entity
- Used directly as MVC model binding target — mixes persistence and presentation

**Proposed approach:**

- Keep data annotations **on the entity** for now — EF Core uses `[StringLength]` for column definition, and removing them would require Fluent API replacements for all 30+ properties (not worth the disruption)
- Add `AppDeploymentFormViewModel` for form binding (annotations stay on VM)
- Entity becomes read-only output; VMs carry input validation
- This is a pragmatic trade-off: domain purity vs. migration cost

---



## Part 5 — Decisions (Approved)


| Question                     | Decision                                                                                   |
| ---------------------------- | ------------------------------------------------------------------------------------------ |
| Q1 Authentication            | **Option B** — ASP.NET Core Identity with a seeded single admin user (cookie auth)         |
| Q2 Email                     | **MailKit** replaces deprecated `SmtpClient` in Phase 2                                    |
| Q3 Tests                     | **Deferred** — after architecture changes are stable (Phase 6)                             |
| Q4 OneSignal Key encryption  | **In scope** — Data Protection API encryption at rest                                      |
| Q5 Cloud storage abstraction | **In scope** — `IAssetStorageService` abstraction designed for cloud swap, local impl kept |
| Q6 Execution                 | **Execute immediately** — no per-phase approval required                                   |
| Verification                 | **Manual testing only** — do not run the project automatically                             |


---



## Part 6 — Verification Plan



### After Each Phase

- `dotnet build` — must pass with zero errors and zero warnings
- `dotnet run` — app must start and respond to requests
- Manual smoke test: create token → open form link → save deployment → start deployment workflow



### After Phase 6 (Tests)

```powershell
dotnet test --configuration Release --logger "console;verbosity=normal"
```



### Manual Regression Checklist

- [ ] `POST /api/form-access-tokens` — token issued, email optional
- [ ] `GET /AppDeployment/Form/{token}` — Create mode (unused token)
- [ ] `POST /AppDeployment/Create` — save partial draft, redirect to Edit
- [ ] `GET /AppDeployment/Form/{token}` — Edit mode (used token)
- [ ] `POST /AppDeployment/Edit` — save changes, assets preserved
- [ ] `POST /AppDeployment/StartDeployment` — validation, workflow dispatch, progress page
- [ ] `GET /AppDeployment/WorkflowStatus?jobId=xxx` — JSON polling
- [ ] Image validation — wrong dimensions rejected with field error
- [ ] Invalid token → `InvalidToken` view
- [ ] Revoked/inactive token → `InvalidToken` view

---

