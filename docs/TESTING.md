# Testing

## Test project

Unit and integration tests live in `MobileAppDeployment.Tests` (xUnit + Moq + `Microsoft.AspNetCore.Mvc.Testing`).

## Run locally

```powershell
cd c:\Applications\systenics\MobileAppDeployment
dotnet build -warnaserror
dotnet test --no-build
```

Or in one step:

```powershell
dotnet test
```

## What is covered

| Area | File | Style |
|------|------|-------|
| Form access token issuance | `FormAccessTokenServiceTests.cs` | Unit (mocked repo + UoW) |
| Draft/save validation | `AppDeploymentValidationTests.cs` | Unit |
| Image header parsing | `AssetImageValidatorTests.cs` | Unit |
| In-memory job store eviction | `WorkflowJobStoreTests.cs` | Unit |
| Token API + API key auth | `FormAccessTokensApiTests.cs` | Integration (`WebApplicationFactory`) |
| Health endpoint | `HealthCheckTests.cs` | Integration |

Integration tests replace PostgreSQL with EF Core InMemory and set `HealthChecks:SkipDatabase=true`.

## CI

GitHub Actions workflow [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) runs `dotnet restore`, `dotnet build -warnaserror`, and `dotnet test` on push/PR to `main` and `master`.
