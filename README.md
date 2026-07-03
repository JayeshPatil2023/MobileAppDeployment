# MobileAppDeployment

ASP.NET Core MVC app for saving mobile app deployment details. Uses **Entity Framework Core** with the **Service → Repository** pattern.

**Beginner guide:** See [docs/ENTITY-FRAMEWORK-CORE-GUIDE.md](docs/ENTITY-FRAMEWORK-CORE-GUIDE.md) for step-by-step instructions on adding/removing fields, entities, relationships, and applying migrations (with examples from this project).

---

## Entity Framework Core Commands

Run all commands from:

`c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment`

### Add a new migration

Use this after changing model classes or `ApplicationDbContext`.

```powershell
dotnet ef migrations add <MigrationName>
```

Example:

```powershell
dotnet ef migrations add AddAppCategoryField
```

### Apply migrations to database

```powershell
dotnet ef database update
```

### List migrations

```powershell
dotnet ef migrations list
```

### Remove last migration

Use only when you want to undo the latest migration.

```powershell
dotnet ef migrations remove
```

### Generate SQL script from migrations

```powershell
dotnet ef migrations script
```

### First-time setup on a new machine

```powershell
dotnet restore
dotnet ef database update
dotnet run
```

---

## Switching from SQL Server to PostgreSQL

Follow these steps when you want to move the project from SQL Server to PostgreSQL.

### Step 1: Install PostgreSQL

1. Install PostgreSQL on your machine or use a hosted instance.
2. Create a database (for example: `SystenicsAppDeployment`).
3. Note these details:
   - Host (example: `localhost`)
   - Port (default: `5432`)
   - Database name
   - Username
   - Password

### Step 2: Change NuGet packages

In `MobileAppDeployment.csproj`:

1. **Remove:** `Microsoft.EntityFrameworkCore.SqlServer`
2. **Add:** `Npgsql.EntityFrameworkCore.PostgreSQL` (use the same EF version as your project, e.g. `8.0.8`)

```powershell
cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment

dotnet remove package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.8
```

### Step 3: Update connection string

In `appsettings.json` (and `appsettings.Development.json` if you use it), replace the SQL Server connection string with PostgreSQL format.

**SQL Server (current):**

```
Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;
```

**PostgreSQL (new):**

```
Host=localhost;Port=5432;Database=SystenicsAppDeployment;Username=your_user;Password=your_password;
```

Keep the key name the same: `DefaultConnection`.

### Step 4: Update Program.cs

Change EF provider registration from SQL Server to PostgreSQL.

**Current:**

```csharp
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
```

**Change to:**

```csharp
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
```

### Step 5: Fix SQL Server-specific code in ApplicationDbContext

In `Data/ApplicationDbContext.cs`, you may have SQL Server default value SQL:

```csharp
entity.Property(x => x.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
```

`SYSUTCDATETIME()` is SQL Server syntax. For PostgreSQL, change it to:

```csharp
entity.Property(x => x.CreatedDate).HasDefaultValueSql("timezone('utc', now())");
```

If you leave SQL Server SQL here, migration or runtime may fail on PostgreSQL.

### Step 6: Recreate migrations (recommended)

Existing migrations generated for SQL Server use SQL Server types (`SqlServer:Identity`, `nvarchar`, etc.) and are **not compatible** with PostgreSQL as-is.

**Best approach (fresh database on PostgreSQL):**

1. Delete the entire `Migrations` folder in the project.
2. Complete Steps 2–5 first.
3. Create a new initial migration:

```powershell
dotnet ef migrations add InitialCreate
```

4. Apply it to PostgreSQL:

```powershell
dotnet ef database update
```

This creates tables in PostgreSQL using Postgres types (`text`, `identity`, etc.).

### Step 7: No repository/service/controller changes needed

Because CRUD already uses EF Core in:

- `AppDeploymentRepository`
- `AppDeploymentService`
- `AppDeploymentController`

You do **not** need to change CRUD logic. EF handles database differences once the provider is switched.

### Step 8: Run and test

```powershell
dotnet build
dotnet run
```

Test:

1. Open the app
2. Create a record
3. View list and details
4. Edit
5. Delete

Confirm data is saved in PostgreSQL (using pgAdmin or `psql`).

### Step 9: What you do not need anymore

- `Scripts/CreateDatabase.sql` — old SQL Server stored procedure script; not needed with EF Core migrations on PostgreSQL.
- SQL Server stored procedures — the app no longer uses them with EF Core.

### Future model changes on PostgreSQL

Whenever you change `AppDeployment` or `ApplicationDbContext`:

```powershell
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

The workflow is the same on PostgreSQL.

### Common mistakes to avoid

| Mistake | What to do instead |
|--------|---------------------|
| Keeping old SQL Server migrations | Delete `Migrations` folder and recreate for PostgreSQL |
| Leaving `HasDefaultValueSql` as SQL Server syntax | Use PostgreSQL functions (e.g. `timezone('utc', now())`) |
| Mixing package versions | Keep EF Core and Npgsql on matching versions (e.g. both `8.0.8`) |
| Wrong connection string keys | PostgreSQL uses `Host`, `Username` — not `Server`, `User Id` |
| Expecting automatic data migration | Moving existing SQL Server data is a separate export/import task |

### Quick checklist

| File / area | Change |
|-------------|--------|
| `.csproj` | Remove SqlServer package, add Npgsql package |
| `appsettings.json` | PostgreSQL connection string |
| `Program.cs` | `UseNpgsql(...)` instead of `UseSqlServer(...)` |
| `ApplicationDbContext.cs` | Replace SQL Server default SQL |
| `Migrations/` | Delete and recreate for PostgreSQL |
| Repository / Service / Controller | No change |
