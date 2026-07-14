using Microsoft.EntityFrameworkCore;
using MobileAppDeployment.Data;
using MobileAppDeployment.Helpers;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Repositories;

/// <summary>
/// EF Core persistence for <see cref="AppDeployment"/> rows.
/// </summary>
public class AppDeploymentRepository : IAppDeploymentRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Creates a repository bound to the application database context.
    /// </summary>
    public AppDeploymentRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AppDeployment>> GetAllAsync()
    {
        return await _dbContext.AppDeployments
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<AppDeployment?> GetByIdAsync(int id)
    {
        return await _dbContext.AppDeployments
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    /// <inheritdoc />
    public async Task<int> InsertAsync(AppDeployment appDeployment)
    {
        // Safety net for partial drafts: never send null into NOT NULL string columns.
        AppDeploymentValidation.NormalizeNonNullableStringsForPartialSave(appDeployment);

        appDeployment.CreatedDate = DateTime.UtcNow;
        _dbContext.AppDeployments.Add(appDeployment);
        await _dbContext.SaveChangesAsync();
        return appDeployment.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(AppDeployment appDeployment)
    {
        AppDeployment? existing = await _dbContext.AppDeployments.FirstOrDefaultAsync(x => x.Id == appDeployment.Id);
        if (existing is null)
        {
            return false;
        }

        // Same null→empty coercion as Insert — Edit save can also post null for empty fields.
        AppDeploymentValidation.NormalizeNonNullableStringsForPartialSave(appDeployment);

        _dbContext.Entry(existing).CurrentValues.SetValues(appDeployment);
        existing.ModifiedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _dbContext.AppDeployments.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
        {
            return false;
        }

        _dbContext.AppDeployments.Remove(existing);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
