using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MobileAppDeployment.Infrastructure.Persistence.Configurations;
using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for app deployments, form-access tokens, and Identity.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    /// <summary>
    /// Creates a context with the configured options (injected by DI).
    /// </summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    /// <summary>Saved mobile app deployment configurations.</summary>
    public DbSet<AppDeployment> AppDeployments => Set<AppDeployment>();

    /// <summary>Client magic-link tokens that gate Create/Edit form access.</summary>
    public DbSet<FormAccessToken> FormAccessTokens => Set<FormAccessToken>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new AppDeploymentConfiguration());
        modelBuilder.ApplyConfiguration(new FormAccessTokenConfiguration());
    }
}
