using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="AppDeployment"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// Using <see cref="IEntityTypeConfiguration{TEntity}"/> keeps all mapping rules
/// for a single entity in one dedicated file. This is far more maintainable than
/// a monolithic <c>OnModelCreating</c> method that grows with every new entity.
/// </para>
/// <para>
/// <strong>Why separate from the model?</strong>
/// Data annotations on the model class serve two purposes: database column
/// constraints (StringLength) and MVC validation (Required, EmailAddress).
/// Fluent API configuration here handles concerns that cannot be expressed via
/// attributes (default SQL expressions, indexes, delete behavior, table names).
/// Both approaches are valid EF Core configuration mechanisms — they are
/// additive and do not conflict.
/// </para>
/// </remarks>
public class AppDeploymentConfiguration : IEntityTypeConfiguration<AppDeployment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AppDeployment> builder)
    {
        // ── Table ─────────────────────────────────────────────────────────
        builder.ToTable("AppDeployments");

        // ── Primary key ───────────────────────────────────────────────────
        builder.HasKey(x => x.Id);

        // ── Timestamps ────────────────────────────────────────────────────
        // CreatedDate is set by the database server on INSERT using UTC time.
        // This removes any risk of timezone differences between the app server
        // and database server.
        builder.Property(x => x.CreatedDate)
            .HasDefaultValueSql("timezone('utc', now())")
            .ValueGeneratedOnAdd();

        // ModifiedDate is set by the repository before every UPDATE.
        builder.Property(x => x.ModifiedDate)
            .IsRequired(false);

        // ── Column mappings (explicit for clarity — EF discovers these by convention) ──
        // String columns: StringLength attribute on the model controls MaxLength.
        // Non-nullable strings: model uses string = string.Empty and repository
        // calls NormalizeNonNullableStringsForPartialSave before any INSERT/UPDATE
        // so PostgreSQL NOT NULL constraints are never violated by partial drafts.

        // ── Indexes ───────────────────────────────────────────────────────
        // No additional indexes on AppDeployments beyond the PK for now.
        // If query patterns change (e.g., listing by AdminEmail or AppName),
        // add composite indexes here without touching the model class.
    }
}
