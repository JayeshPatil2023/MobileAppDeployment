using MobileAppDeployment.Models;

namespace MobileAppDeployment.Services;

public interface IAppDeploymentService
{
    Task<IEnumerable<AppDeployment>> GetAllAsync();
    Task<AppDeployment?> GetByIdAsync(int id);
    Task<int> CreateAsync(AppDeployment appDeployment);
    Task<bool> UpdateAsync(AppDeployment appDeployment);
    Task<bool> DeleteAsync(int id);
}
