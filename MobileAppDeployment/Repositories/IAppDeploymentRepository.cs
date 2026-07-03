using MobileAppDeployment.Models;

namespace MobileAppDeployment.Repositories;

public interface IAppDeploymentRepository
{
    Task<IEnumerable<AppDeployment>> GetAllAsync();
    Task<AppDeployment?> GetByIdAsync(int id);
    Task<int> InsertAsync(AppDeployment appDeployment);
    Task<bool> UpdateAsync(AppDeployment appDeployment);
    Task<bool> DeleteAsync(int id);
}
