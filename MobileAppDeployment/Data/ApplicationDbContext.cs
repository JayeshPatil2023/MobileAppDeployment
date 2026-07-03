using Microsoft.EntityFrameworkCore;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<AppDeployment> AppDeployments => Set<AppDeployment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppDeployment>(entity =>
        {
            entity.ToTable("AppDeployments");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CreatedDate).HasDefaultValueSql("timezone('utc', now())");
        });
    }
}
