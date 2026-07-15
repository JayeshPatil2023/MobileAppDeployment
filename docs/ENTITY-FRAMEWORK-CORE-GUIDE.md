# Entity Framework Core Guide

This guide covers everything a developer needs to work with the database layer — adding fields, creating migrations, seeding data, and troubleshooting common issues.

---

## Table of Contents

1. [Overview](#overview)
2. [Adding a new field to an existing model](#adding-a-new-field)
3. [Adding a new entity (table)](#adding-a-new-entity)
4. [Removing a field or entity](#removing-a-field-or-entity)
5. [Creating and applying migrations](#creating-and-applying-migrations)
6. [Seeding data](#seeding-data)
7. [Common EF issues and fixes](#common-ef-issues-and-fixes)

---

## Overview

The project uses **EF Core 8** with the **Npgsql** PostgreSQL provider.

| Layer | Location |
|-------|----------|
| Database context | `Data/ApplicationDbContext.cs` |
| Entity models | `Models/` |
| Fluent API configuration | `Infrastructure/Persistence/Configurations/` |
| Repositories | `Repositories/` |
| Migrations | `Migrations/` |

**Key decisions:**
- Entity relationships, indexes, and table-level defaults are configured in `IEntityTypeConfiguration<T>` classes — **not** in `OnModelCreating` directly.
- Data annotations on model classes handle validation (`[Required]`, `[StringLength]`) and are also picked up by EF for column constraints.
- Both approaches are additive: EF merges attribute-based config with fluent API config.

---

## Adding a new field

**Example:** Add a `SupportEmail` column to `AppDeployment`.

### Step 1 - Add the property to the model

```csharp
// Models/AppDeployment.cs
[StringLength(255)]
[EmailAddress]
[Display(Name = "Support Email")]
public string? SupportEmail { get; set; }
```

> **Nullable vs Non-nullable:** Use `string?` (nullable) for optional fields. Use `string` with a default value of `string.Empty` for required fields to avoid NOT NULL constraint violations when saving drafts.

### Step 2 - Add fluent configuration (if needed)

Only add to the configuration file if you need database-level constraints that annotations can't express (e.g., a default value, an index, or a computed column):

```csharp
// Infrastructure/Persistence/Configurations/AppDeploymentConfiguration.cs
builder.HasIndex(x => x.SupportEmail)
    .HasDatabaseName("IX_AppDeployments_SupportEmail");
```

### Step 3 - Create the migration

```powershell
cd MobileAppDeployment
dotnet ef migrations add Add_SupportEmail_To_AppDeployments
```

Review the generated migration file in `Migrations/` before applying. Verify:
- The correct column type is generated
- No unexpected columns were added or removed
- If a NOT NULL column was added, ensure a `defaultValue:` is provided for existing rows

### Step 4 - Apply the migration

```powershell
dotnet ef database update
```

### Step 5 - Update the form (if user-facing)

Add the field to the appropriate Razor view (`Views/AppDeployment/Create.cshtml` and/or `Edit.cshtml`).

---

## Adding a new entity

**Example:** Add a `DeploymentNote` entity.

### Step 1 - Create the model class

```csharp
// Models/DeploymentNote.cs
using System.ComponentModel.DataAnnotations;

public class DeploymentNote
{
    public int Id { get; set; }

    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public int AppDeploymentId { get; set; }
    public AppDeployment AppDeployment { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
```

### Step 2 - Add a DbSet to ApplicationDbContext

```csharp
// Data/ApplicationDbContext.cs
public DbSet<DeploymentNote> DeploymentNotes => Set<DeploymentNote>();
```

### Step 3 - Create a configuration class

```csharp
// Infrastructure/Persistence/Configurations/DeploymentNoteConfiguration.cs
public class DeploymentNoteConfiguration : IEntityTypeConfiguration<DeploymentNote>
{
    public void Configure(EntityTypeBuilder<DeploymentNote> builder)
    {
        builder.ToTable("DeploymentNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("timezone('utc', now())")
            .ValueGeneratedOnAdd();
        builder.HasOne(x => x.AppDeployment)
            .WithMany()
            .HasForeignKey(x => x.AppDeploymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Step 4 - Register the configuration in ApplicationDbContext

```csharp
// Data/ApplicationDbContext.cs  OnModelCreating()
modelBuilder.ApplyConfiguration(new DeploymentNoteConfiguration());
```

### Step 5 - Create the interface and repository

Create `Repositories/IDeploymentNoteRepository.cs` and `Repositories/DeploymentNoteRepository.cs` following the pattern in `IAppDeploymentRepository.cs`.

### Step 6 - Register in DI

```csharp
// Extensions/ServiceCollectionExtensions.cs  AddPersistenceServices()
services.AddScoped<IDeploymentNoteRepository, DeploymentNoteRepository>();
```

### Step 7 - Migrate

```powershell
dotnet ef migrations add Add_DeploymentNotes_Table
dotnet ef database update
```

---

## Removing a field or entity

> [!CAUTION]
> Removing a column drops data permanently. Always take a database backup before running destructive migrations in production.

1. Remove the property from the model (or the class + DbSet for an entity).
2. Remove the configuration from the `IEntityTypeConfiguration` class.
3. Run `dotnet ef migrations add Remove_FieldName` and review the generated `DropColumn` or `DropTable` SQL.
4. Run `dotnet ef database update`.

---

## Creating and applying migrations

### Create a migration (development)

```powershell
cd MobileAppDeployment
dotnet ef migrations add <MigrationName>
```

Use descriptive names that describe what changed:
- `Add_SupportEmail_To_AppDeployments` -- GOOD
- `Migration1` -- BAD

### Apply migrations (development)

```powershell
dotnet ef database update
```

### Apply migrations (production / CI)

> [!IMPORTANT]
> Never run `dotnet ef database update` in production from a developer machine. Generate a SQL script and apply it via a database admin or CI step.

```powershell
dotnet ef migrations script --idempotent --output migrations.sql
```

The `--idempotent` flag wraps each migration in an IF-NOT-EXISTS check so the script is safe to run multiple times.

### Roll back a migration

```powershell
# Roll back to a specific migration name
dotnet ef database update PreviousMigrationName

# Remove the last unapplied migration from code (does NOT touch the database)
dotnet ef migrations remove
```

---

## Seeding data

Add seed data in `OnModelCreating` using the fluent API:

```csharp
// Data/ApplicationDbContext.cs
modelBuilder.Entity<FormAccessToken>().HasData(new FormAccessToken
{
    Id = 1,
    Token = "dev-test-token",
    ClientName = "Test Client",
    ClientAppName = "Test App",
    IsActive = true,
    CreatedUtc = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
});
```

> **When to seed vs. script:** Seed only immutable reference data (lookup tables, initial admin configuration). Use a separate SQL script for environment-specific data.

---

## Common EF issues and fixes

| Problem | Likely cause | Fix |
|---------|-------------|-----|
| `password authentication failed` | Wrong connection string | Check user-secrets: `dotnet user-secrets list` |
| `table does not exist` | Migrations not applied | Run `dotnet ef database update` |
| `A second operation was started on this context` | DbContext used concurrently | Each request gets its own scoped DbContext -- check for Singletons holding a DbContext |
| `Cannot access a disposed object` | DbContext used after request scope | Do not use DbContext in background threads -- use IServiceScopeFactory to create a new scope |
| Migration generated unexpected changes | Model properties don't match DB | Check column mappings in the config file and inspect the generated diff |
| `dotnet ef` not found | EF CLI tools not installed | Run `dotnet tool restore` in the project folder |

---

## See also

- [DEVELOPMENT-SETUP.md](DEVELOPMENT-SETUP.md) -- Initial machine setup including database creation
- [ARCHITECTURE.md](ARCHITECTURE.md) -- System design and layer responsibilities
- [EF Core migrations docs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)