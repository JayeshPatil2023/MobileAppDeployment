using Microsoft.EntityFrameworkCore;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Data;

/// <summary>
/// Entity Framework Core database context for app deployments and form-access tokens.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Creates a context with the configured options.
    /// </summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Saved mobile app deployment configurations.
    /// </summary>
    public DbSet<AppDeployment> AppDeployments => Set<AppDeployment>();

    /// <summary>
    /// Client magic-link tokens that gate Create/Edit form access.
    /// </summary>
    public DbSet<FormAccessToken> FormAccessTokens => Set<FormAccessToken>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppDeployment>(entity =>
        {
            entity.ToTable("AppDeployments");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CreatedDate).HasDefaultValueSql("timezone('utc', now())");
        });

        modelBuilder.Entity<FormAccessToken>(entity =>
        {
            entity.ToTable("FormAccessTokens");
            entity.HasKey(x => x.Id);

            // Token lookups must be unique and fast — this is the form URL key.
            entity.HasIndex(x => x.Token).IsUnique();

            entity.Property(x => x.CreatedUtc).HasDefaultValueSql("timezone('utc', now())");

            // If a deployment is deleted, keep the token row but clear the link
            // so the magic link no longer shows edit data for a missing record.
            entity.HasOne(x => x.AppDeployment)
                .WithMany()
                .HasForeignKey(x => x.AppDeploymentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
