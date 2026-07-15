using Microsoft.EntityFrameworkCore;

namespace MobileAppDeployment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core persistence for <see cref="AppDeployment"/> rows.
/// </summary>
/// <remarks>
/// Repositories stage changes only; <see cref="IUnitOfWork.SaveChangesAsync"/> commits them.
/// </remarks>
public class AppDeploymentRepository : IAppDeploymentRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>Creates a repository bound to the application database context.</summary>
    public AppDeploymentRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AppDeployment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppDeployments
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AppDeployment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppDeployments
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public Task InsertAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default)
    {
        // Safety net for partial drafts: never send null into NOT NULL string columns.
        AppDeploymentValidation.NormalizeNonNullableStringsForPartialSave(appDeployment);

        appDeployment.CreatedDate = DateTime.UtcNow;
        _dbContext.AppDeployments.Add(appDeployment);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default)
    {
        AppDeployment? existing = await _dbContext.AppDeployments
            .FirstOrDefaultAsync(x => x.Id == appDeployment.Id, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        AppDeploymentValidation.NormalizeNonNullableStringsForPartialSave(appDeployment);

        _dbContext.Entry(existing).CurrentValues.SetValues(appDeployment);
        existing.ModifiedDate = DateTime.UtcNow;
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        AppDeployment? existing = await _dbContext.AppDeployments
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        _dbContext.AppDeployments.Remove(existing);
        return true;
    }
}
