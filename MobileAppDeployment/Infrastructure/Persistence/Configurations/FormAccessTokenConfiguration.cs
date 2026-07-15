using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="FormAccessToken"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FormAccessToken"/> has two database-level concerns that cannot be
/// expressed via data annotations and must be configured here:
/// </para>
/// <list type="bullet">
///   <item>
///     <term>Unique index on Token</term>
///     <description>
///       The token string is the primary lookup key for magic-link URL resolution.
///       The unique index also prevents race conditions if two concurrent requests
///       try to insert a token with the same value.
///     </description>
///   </item>
///   <item>
///     <term>SetNull delete behavior</term>
///     <description>
///       When a deployment is deleted, the token's AppDeploymentId FK is set to NULL
///       rather than cascading the delete. This means the token row is preserved and
///       the magic link URL returns "InvalidToken" rather than a 500 error, giving
///       the admin a clear signal that the token no longer has a linked deployment.
///     </description>
///   </item>
/// </list>
/// </remarks>
public class FormAccessTokenConfiguration : IEntityTypeConfiguration<FormAccessToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FormAccessToken> builder)
    {
        // ── Table ─────────────────────────────────────────────────────────
        builder.ToTable("FormAccessTokens");

        // ── Primary key ───────────────────────────────────────────────────
        builder.HasKey(x => x.Id);

        // ── Unique index on Token ─────────────────────────────────────────
        // Token lookups happen on every form page load and form submit.
        // The index makes these O(log n) and enforces uniqueness at the DB level.
        builder.HasIndex(x => x.Token)
            .IsUnique()
            .HasDatabaseName("IX_FormAccessTokens_Token");

        // ── Timestamp ─────────────────────────────────────────────────────
        builder.Property(x => x.CreatedUtc)
            .HasDefaultValueSql("timezone('utc', now())")
            .ValueGeneratedOnAdd();

        // ── Relationship: FormAccessToken → AppDeployment ─────────────────
        // One deployment can be linked to many tokens (re-issuance scenario),
        // but in practice only one active token per client/app pair exists.
        //
        // OnDelete: SetNull — deleting a deployment clears AppDeploymentId on
        // all linked tokens rather than deleting the token rows.
        // This preserves the magic-link token row so it shows "InvalidToken"
        // rather than a 404 crash.
        builder.HasOne(x => x.AppDeployment)
            .WithMany()
            .HasForeignKey(x => x.AppDeploymentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
