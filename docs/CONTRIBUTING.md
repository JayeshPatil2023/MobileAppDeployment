# Contributing

Guidelines for contributing to **MobileAppDeployment**.

---

## Coding standards

- **Async I/O**: All database, file, and HTTP operations use `async`/`await` with `CancellationToken` propagation in repositories.
- **No secrets in commits**: Use `dotnet user-secrets` locally; document new keys in `docs/DEVELOPMENT-SETUP.md`.
- **XML documentation**: Every public class and public method must have a `<summary>` comment.
- **Minimal scope**: Prefer focused diffs; match existing naming and folder conventions.
- **EF sensitive logging**: Never enable `EnableSensitiveDataLogging(true)`.

---

## Naming conventions

### Services

| Pattern | Example | Use for |
|---------|---------|---------|
| `GetXxxAsync` | `GetByIdAsync` | Queries |
| `CreateXxxAsync` / `CreateAsync` | `CreateAsync` | Inserts |
| `UpdateXxxAsync` / `UpdateAsync` | `UpdateAsync` | Updates |
| `DeleteXxxAsync` / `DeleteAsync` | `DeleteAsync` | Deletes |

### Migrations

Use descriptive names:

```
Add_FieldName_To_TableName
Add_OneSignalRestApiKey_Encrypted
Add_AspNetCoreIdentity
```

Commands:

```powershell
dotnet ef migrations add Add_MyField_To_AppDeployments
dotnet ef database update
```

### Folders

| Folder | Contents |
|--------|----------|
| `Core/Models/ViewModels/` | MVC view models |
| `Application/` | Services, background jobs, asset upload strategies |
| `Infrastructure/` | EF, email, health checks, security |
| `Web/` | Middleware, filters |

---

## Pull request checklist

- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] `dotnet test` — all tests pass
- [ ] No secrets in committed files
- [ ] New public APIs have XML `<summary>` docs
- [ ] EF migration included for schema changes (if applicable)
- [ ] `docs/DEVELOPMENT-SETUP.md` updated for new user-secrets keys
- [ ] Public URLs unchanged (form links, API routes)
- [ ] Manual smoke test notes in PR description (token issue → form save → admin login)

---

## Running tests

```powershell
cd c:\Applications\systenics\MobileAppDeployment
dotnet test --configuration Release
```

Test project: `MobileAppDeployment.Tests` (xUnit + Moq + `WebApplicationFactory`).

---

## Architecture decisions

See [ARCHITECTURE.md](ARCHITECTURE.md) and [implementation_plan.md](../implementation_plan.md) Part 5 for approved decisions (Identity Option B, MailKit, no MediatR, no AutoMapper, Data Protection for OneSignal key).
