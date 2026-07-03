# Entity Framework Core — Beginner Guide (This Project)

This guide explains how database changes work in **MobileAppDeployment** using **Entity Framework Core (EF Core)** with **PostgreSQL**.

It uses real examples from this project:

| What we did | Migration name | What changed |
|-------------|----------------|--------------|
| Added App Identifiers, OneSignal, Firebase fields | `AddIntegrationFields` | New columns on `AppDeployments` table |
| Removed Website logo | `RemoveWebsiteLogo` | Dropped `WebsiteLogoPath` column |

After reading this, you should be able to add/remove fields, add/remove tables, set up relationships, and apply migrations on your own.

---

## Table of contents

1. [How EF Core fits in this app](#1-how-ef-core-fits-in-this-app)
2. [The 5 files you must know](#2-the-5-files-you-must-know)
3. [The standard workflow (always follow this order)](#3-the-standard-workflow-always-follow-this-order)
4. [How to add a new field to an existing table](#4-how-to-add-a-new-field-to-an-existing-table)
5. [How to remove a field from an existing table](#5-how-to-remove-a-field-from-an-existing-table)
6. [How to add a new entity (new database table)](#6-how-to-add-a-new-entity-new-database-table)
7. [How to remove an entity (delete a table)](#7-how-to-remove-an-entity-delete-a-table)
8. [How to create relationships between tables](#8-how-to-create-relationships-between-tables)
9. [Migrations — commands and meaning](#9-migrations--commands-and-meaning)
10. [What happens inside a migration file](#10-what-happens-inside-a-migration-file)
11. [Common mistakes and how to fix them](#11-common-mistakes-and-how-to-fix-them)
12. [Quick checklists](#12-quick-checklists)

---

## 1. How EF Core fits in this app

Think of EF Core as a **translator** between your C# classes and PostgreSQL tables.

```
┌─────────────────┐     ┌──────────────────────┐     ┌─────────────────┐
│  C# Model       │     │  ApplicationDbContext │     │  PostgreSQL     │
│  AppDeployment  │ ──► │  (maps model → table) │ ──► │  AppDeployments │
└─────────────────┘     └──────────────────────┘     └─────────────────┘
         ▲                          ▲
         │                          │
   You edit this              Migrations apply
   when adding fields         SQL to the database
```

**Request flow in this project:**

1. User submits the form → **Controller** (`AppDeploymentController`)
2. Controller calls → **Service** (`AppDeploymentService`)
3. Service calls → **Repository** (`AppDeploymentRepository`)
4. Repository uses → **DbContext** (`ApplicationDbContext`) to read/write PostgreSQL

You **do not** write `ALTER TABLE` SQL by hand for normal changes. You change the C# model, create a migration, and EF generates the SQL.

---

## 2. The 5 files you must know

| File | Purpose |
|------|---------|
| `Models/AppDeployment.cs` | C# class = one row in the database. Each property = one column. |
| `Data/ApplicationDbContext.cs` | Tells EF which models exist and how they map to tables. |
| `Migrations/*.cs` | History of database changes (auto-generated, do not edit casually). |
| `Migrations/ApplicationDbContextModelSnapshot.cs` | Current "picture" of the whole database schema. |
| `Program.cs` | Registers DbContext and connection string at startup. |

**Connection string** lives in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=SystenicsAppDeployment;Username=postgres;Password=YOUR_PASSWORD;"
}
```

**DbContext registration** in `Program.cs`:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

---

## 3. The standard workflow (always follow this order)

Whenever you change the database structure:

```
Step 1 → Edit the Model (and DbContext if needed)
Step 2 → Update Controller / Views / Repository (if the app should use the new data)
Step 3 → Create a migration
Step 4 → Review the migration file (optional but recommended)
Step 5 → Apply migration to database
Step 6 → Build and test the app
```

**Always run commands from the project folder** (where the `.csproj` file is):

```powershell
cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment
```

**Prerequisite:** EF Core tools must be installed (already in this project via `dotnet-tools.json`):

```powershell
dotnet ef --version
```

If that fails:

```powershell
dotnet tool install --global dotnet-ef
```

---

## 4. How to add a new field to an existing table

This is exactly what we did for **App Identifiers**, **OneSignal**, and **Firebase** (`AddIntegrationFields` migration).

### Step 1 — Add property to the model

Open `Models/AppDeployment.cs` and add a new property.

**Example — we added iOS Bundle ID:**

```csharp
// App Identifiers
[Required(ErrorMessage = "iOS Bundle ID is required.")]
[Display(Name = "iOS Bundle ID")]
[StringLength(255)]                                    // Max length in database = varchar(255)
public string IosBundleId { get; set; } = string.Empty; // Required text → NOT NULL column
```

**Example — optional file path (nullable):**

```csharp
[StringLength(500)]
public string? FirebaseIosConfigPath { get; set; }     // string? = column can be NULL
```

#### Property type → database column (PostgreSQL)

| C# type | Database column | Notes |
|---------|-----------------|-------|
| `string` | `varchar` / `text` | Use `[StringLength(n)]` for max size |
| `string?` | nullable `varchar` | `?` means NULL allowed |
| `int` | `integer` | |
| `bool` | `boolean` | |
| `DateTime` | `timestamp` | |
| `DateTime?` | nullable `timestamp` | |
| `decimal` | `numeric` | |

#### Useful data annotations

```csharp
[Required]           // NOT NULL + form validation
[StringLength(100)]  // Max 100 characters in DB
[MaxLength(500)]     // Same idea as StringLength
[EmailAddress]       // Form validation only (not a DB type)
```

> **Tip:** `[Required]` on the model affects **form validation**. For the database, EF uses `string` vs `string?` and whether you set a default value in migrations.

### Step 2 — DbContext (usually no change for simple fields)

For a new property on `AppDeployment`, you **do not** need to change `ApplicationDbContext.cs`. EF discovers properties automatically.

You only edit DbContext when you need special rules:

```csharp
entity.Property(x => x.CreatedDate).HasDefaultValueSql("timezone('utc', now())");
//                              ↑ PostgreSQL-specific default when row is inserted
```

### Step 3 — Update the rest of the app

For user-facing fields, also update:

- **Views** — form fields (`Views/AppDeployment/_FormFields.cshtml`, partials)
- **Controller** — file uploads, validation (`AppDeploymentController.cs`)
- **Details view** — show the new data

Skipping this step means the column exists in the database but the user cannot enter or see the value.

### Step 4 — Create migration

```powershell
dotnet ef migrations add AddIntegrationFields
```

- `AddIntegrationFields` = descriptive name (use PascalCase, no spaces)
- EF compares your model to the last snapshot and generates a migration file

### Step 5 — Review generated migration

Open `Migrations/20260703104729_AddIntegrationFields.cs`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Up = apply changes (forward)
    migrationBuilder.AddColumn<string>(
        name: "IosBundleId",
        table: "AppDeployments",
        type: "character varying(255)",
        maxLength: 255,
        nullable: false,
        defaultValue: "");   // Existing rows get "" so NOT NULL does not fail
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Down = undo this migration (rollback)
    migrationBuilder.DropColumn(name: "IosBundleId", table: "AppDeployments");
}
```

### Step 6 — Apply to database

```powershell
dotnet ef database update
```

This runs all pending `Up()` methods against PostgreSQL.

### Step 7 — Test

1. Run the app: `dotnet run`
2. Create or edit a deployment
3. Confirm data saves and appears on Details page
4. Optional: check in pgAdmin — `SELECT "IosBundleId" FROM "AppDeployments";`

---

## 5. How to remove a field from an existing table

This is what we did for **Website logo** (`RemoveWebsiteLogo` migration).

### Step 1 — Remove from model

Delete the property from `Models/AppDeployment.cs`:

```csharp
// REMOVED:
// public string? WebsiteLogoPath { get; set; }
```

### Step 2 — Remove from app code

Remove everywhere the field is used:

- Form upload (`_AssetFields.cshtml`)
- Controller parameters and save logic
- Details view display

If you skip this, the app will not compile (references to a property that no longer exists).

### Step 3 — Create migration

```powershell
dotnet ef migrations add RemoveWebsiteLogo
```

EF detects the property is gone and generates:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "WebsiteLogoPath",
        table: "AppDeployments");
}
```

> **Warning:** `DropColumn` **permanently deletes** that column and all data in it. Back up production data before applying.

### Step 4 — Apply migration

```powershell
dotnet ef database update
```

### Step 5 — Clean up uploaded files (optional)

Removing a DB column does **not** delete files in `wwwroot/uploads/`. Delete old files manually if needed.

---

## 6. How to add a new entity (new database table)

An **entity** = a new C# class + a new database table.

**Example scenario:** Add a `DeploymentComment` table so users can leave notes on each deployment.

### Step 1 — Create the model class

Create `Models/DeploymentComment.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace MobileAppDeployment.Models;

public class DeploymentComment
{
    // Primary key — EF convention: property named "Id" or "{ClassName}Id"
    public int Id { get; set; }

    // Foreign key — links to AppDeployments.Id
    public int AppDeploymentId { get; set; }

    [Required]
    [StringLength(1000)]
    public string CommentText { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    // Navigation property — access the parent deployment from a comment
    public AppDeployment AppDeployment { get; set; } = null!;
}
```

### Step 2 — Register in DbContext

Edit `Data/ApplicationDbContext.cs`:

```csharp
// Add a DbSet — one DbSet = one table
public DbSet<DeploymentComment> DeploymentComments => Set<DeploymentComment>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<AppDeployment>(entity =>
    {
        entity.ToTable("AppDeployments");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.CreatedDate).HasDefaultValueSql("timezone('utc', now())");
    });

    // Configure the new entity
    modelBuilder.Entity<DeploymentComment>(entity =>
    {
        entity.ToTable("DeploymentComments");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.CreatedDate)
              .HasDefaultValueSql("timezone('utc', now())");
    });
}
```

### Step 3 — Add reverse navigation (optional but useful)

On `AppDeployment.cs`:

```csharp
// Collection of comments for this deployment
public ICollection<DeploymentComment> Comments { get; set; } = new List<DeploymentComment>();
```

### Step 4 — Migration and update

```powershell
dotnet ef migrations add AddDeploymentComments
dotnet ef database update
```

### Step 5 — Add repository / service / controller

EF only creates the table. You still write CRUD code (or extend existing repository) to use the new entity.

---

## 7. How to remove an entity (delete a table)

### Step 1 — Remove all code that uses the entity

- Delete or stop using the model class
- Remove `DbSet<>` from DbContext
- Remove `modelBuilder.Entity<>` configuration
- Remove repository/service/controller code

### Step 2 — Create migration

```powershell
dotnet ef migrations add RemoveDeploymentComments
```

Generated `Up()` will contain something like:

```csharp
migrationBuilder.DropTable(name: "DeploymentComments");
```

### Step 3 — Apply

```powershell
dotnet ef database update
```

> **Warning:** `DropTable` deletes the entire table and all its data.

---

## 8. How to create relationships between tables

Relationships connect two entities (e.g. one deployment has many comments).

### One-to-many (most common)

**One** `AppDeployment` has **many** `DeploymentComment` rows.

```
AppDeployments (1) ──────< (many) DeploymentComments
       Id                    AppDeploymentId (FK)
```

**Child entity** (many side) — holds the foreign key:

```csharp
public class DeploymentComment
{
    public int Id { get; set; }
    public int AppDeploymentId { get; set; }  // FK column in database

    public AppDeployment AppDeployment { get; set; } = null!;  // Navigation to parent
}
```

**Parent entity** (one side):

```csharp
public class AppDeployment
{
    public int Id { get; set; }
    public ICollection<DeploymentComment> Comments { get; set; } = new List<DeploymentComment>();
}
```

**Configure in DbContext** (explicit — recommended for clarity):

```csharp
modelBuilder.Entity<DeploymentComment>(entity =>
{
    entity.HasOne(c => c.AppDeployment)       // Comment has one Deployment
          .WithMany(d => d.Comments)          // Deployment has many Comments
          .HasForeignKey(c => c.AppDeploymentId)  // FK column name
          .OnDelete(DeleteBehavior.Cascade);  // Delete comments when deployment deleted
});
```

| `DeleteBehavior` | Meaning |
|------------------|---------|
| `Cascade` | Delete children when parent is deleted |
| `Restrict` | Prevent deleting parent if children exist |
| `SetNull` | Set FK to NULL when parent deleted (FK must be nullable) |

### One-to-one

Example: each deployment has one extended settings record.

```csharp
public class AppDeploymentSettings
{
    public int Id { get; set; }
    public int AppDeploymentId { get; set; }  // FK + unique = one-to-one
    public AppDeployment AppDeployment { get; set; } = null!;
    public bool EnablePushNotifications { get; set; }
}

// In DbContext:
entity.HasOne(s => s.AppDeployment)
      .WithOne(d => d.Settings)
      .HasForeignKey<AppDeploymentSettings>(s => s.AppDeploymentId);
```

### Many-to-many

Example: deployments and tags (a deployment has many tags, a tag applies to many deployments).

EF Core 5+ uses a join entity automatically, or you define one explicitly:

```csharp
public class DeploymentTag
{
    public int AppDeploymentId { get; set; }
    public int TagId { get; set; }
    public AppDeployment AppDeployment { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```

For beginners, start with **one-to-many** — it covers most cases.

---

## 9. Migrations — commands and meaning

Run from: `c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment`

| Command | What it does |
|---------|----------------|
| `dotnet ef migrations add <Name>` | Creates a new migration from model changes |
| `dotnet ef database update` | Applies all pending migrations to PostgreSQL |
| `dotnet ef database update <MigrationName>` | Updates DB up to a specific migration |
| `dotnet ef migrations list` | Shows which migrations are applied |
| `dotnet ef migrations remove` | Removes the **last** migration (only if not applied yet, or you know what you're doing) |
| `dotnet ef migrations script` | Generates SQL script instead of applying directly |

### Migration naming tips

Use clear, action-based names:

- `AddIntegrationFields` ✅
- `RemoveWebsiteLogo` ✅
- `AddDeploymentComments` ✅
- `Update1` ❌ (too vague)

### This project's migration history (example)

```
InitialCreate          → Created AppDeployments table
AddAssetFields         → Added asset path columns
FixAssetColumns        → Fixed missing columns on PostgreSQL
AddIntegrationFields   → Added IosBundleId, OneSignal*, Firebase* columns
RemoveWebsiteLogo      → Dropped WebsiteLogoPath column
```

Check applied migrations:

```powershell
dotnet ef migrations list
```

Output example:

```
20260703045830_InitialCreate
20260703104729_AddIntegrationFields
20260703130032_RemoveWebsiteLogo
```

---

## 10. What happens inside a migration file

Each migration has two methods:

| Method | Direction | When it runs |
|--------|-----------|--------------|
| `Up()` | Forward | `dotnet ef database update` |
| `Down()` | Backward | Rolling back to previous migration |

**Files created per migration:**

```
Migrations/
  20260703104729_AddIntegrationFields.cs          ← SQL operations
  20260703104729_AddIntegrationFields.Designer.cs ← Metadata for EF
  ApplicationDbContextModelSnapshot.cs            ← Updated to latest schema
```

**Rule:** Do not manually edit old migrations after they are applied to production. If you need to fix something, create a **new** migration.

---

## 11. Common mistakes and how to fix them

### Error: Column does not exist (e.g. `WebsiteLogoPath` / `FeatureGraphicPath`)

**Cause:** Model has the property but migration was not applied (or migration was empty).

**Fix:**

```powershell
dotnet ef migrations add FixMissingColumns
dotnet ef database update
```

### Error: Build failed when running `dotnet ef`

**Cause:** App does not compile (syntax errors).

**Fix:** Run `dotnet build` first and fix errors.

### Error: File is locked / cannot copy DLL

**Cause:** App is still running in Visual Studio or IIS Express.

**Fix:** Stop debugging, then run EF commands again.

### Added property but migration is empty

**Cause:** Model and snapshot already match, or wrong project folder.

**Fix:** Ensure you edited the correct model and run commands from the `.csproj` folder.

### `dotnet ef` command not found

```powershell
dotnet tool install --global dotnet-ef
```

### Changed model on a shared/production database

1. Back up the database first
2. Test migration on a copy or development DB
3. Review generated SQL: `dotnet ef migrations script`

### Removed required column from model but forgot migration

App may still work until EF tries to map the column. Always run `migrations add` + `database update`.

---

## 12. Quick checklists

### Adding a new form field (full checklist)

- [ ] Add property to `Models/AppDeployment.cs`
- [ ] Add form input in the appropriate `.cshtml` partial
- [ ] Update controller if file upload or special validation
- [ ] Update `Details.cshtml` to display the value
- [ ] `dotnet ef migrations add <DescriptiveName>`
- [ ] Review migration `Up()` method
- [ ] `dotnet ef database update`
- [ ] `dotnet build` and test Create / Edit / Details

### Removing a field (full checklist)

- [ ] Remove property from model
- [ ] Remove from views and controller
- [ ] `dotnet ef migrations add Remove<FieldName>`
- [ ] `dotnet ef database update`
- [ ] Test that Create/Edit still work

### Adding a new table (full checklist)

- [ ] Create model class in `Models/`
- [ ] Add `DbSet<>` to `ApplicationDbContext`
- [ ] Configure entity in `OnModelCreating` (keys, relationships)
- [ ] Create repository/service if needed
- [ ] `dotnet ef migrations add <Name>`
- [ ] `dotnet ef database update`

---

## Real example walkthrough — Adding `AppVersion` field

Suppose you want a new optional text field **App Version** on the form.

### 1. Model

```csharp
// In AppDeployment.cs
[Display(Name = "App Version")]
[StringLength(50)]
public string? AppVersion { get; set; }
```

### 2. View (in `_FormFields.cshtml` under General app details)

```html
<div class="field spacer">
    <label asp-for="AppVersion" class="field-label">App version</label>
    <input asp-for="AppVersion" class="form-input" maxlength="50" placeholder="e.g. 1.0.0" />
    <span asp-validation-for="AppVersion" class="field-error"></span>
</div>
```

### 3. Details view

```html
<div class="detail-item">
    <div class="detail-label">App version</div>
    <div class="detail-value">@(Model.AppVersion ?? "—")</div>
</div>
```

### 4. Migration

```powershell
cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment
dotnet ef migrations add AddAppVersion
dotnet ef database update
```

Done. PostgreSQL now has an `AppVersion` column on `AppDeployments`.

---

## Summary

| Task | Edit model? | Edit DbContext? | Migration command |
|------|-------------|-----------------|-------------------|
| Add field | Yes | Rarely | `dotnet ef migrations add ...` |
| Remove field | Yes (delete property) | No | `dotnet ef migrations add ...` |
| Add table | Yes (new class) | Yes (`DbSet` + config) | `dotnet ef migrations add ...` |
| Remove table | Delete class | Remove `DbSet` | `dotnet ef migrations add ...` |
| Add relationship | Both entities | Yes (`HasOne`/`WithMany`) | `dotnet ef migrations add ...` |
| Apply to DB | — | — | `dotnet ef database update` |

**Remember:** The model is the source of truth. Migrations are the bridge. The database is updated only when you run `dotnet ef database update`.

For PostgreSQL-specific setup, see the main [README.md](../README.md).

# The pattern to remember

1. Change Models/AppDeployment.cs (or add a new model class)
2. Update DbContext if it's a new table or relationship
3. Update Views + Controller (so users can enter/see data)
4. dotnet ef migrations add YourMigrationName
5. dotnet ef database update
6. Test the app

All PowerShell commands in the doc use your project path:

cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment