namespace MobileAppDeployment.Infrastructure.Persistence;

/// <summary>
/// Thin Unit of Work that commits the shared <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Creates a unit of work bound to the application database context.
    /// </summary>
    /// <param name="dbContext">Shared EF Core context for the current scope.</param>
    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
