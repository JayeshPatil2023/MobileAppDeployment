using Microsoft.EntityFrameworkCore;
using MobileAppDeployment.Data;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Repositories;

public class AppDeploymentRepository : IAppDeploymentRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AppDeploymentRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<AppDeployment>> GetAllAsync()
    {
        return await _dbContext.AppDeployments
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();
    }

    public async Task<AppDeployment?> GetByIdAsync(int id)
    {
        return await _dbContext.AppDeployments
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> InsertAsync(AppDeployment appDeployment)
    {
        appDeployment.CreatedDate = DateTime.UtcNow;
        _dbContext.AppDeployments.Add(appDeployment);
        await _dbContext.SaveChangesAsync();
        return appDeployment.Id;
    }

    public async Task<bool> UpdateAsync(AppDeployment appDeployment)
    {
        AppDeployment? existing = await _dbContext.AppDeployments.FirstOrDefaultAsync(x => x.Id == appDeployment.Id);
        if (existing is null)
        {
            return false;
        }

        _dbContext.Entry(existing).CurrentValues.SetValues(appDeployment);
        existing.ModifiedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return true;
    }

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
