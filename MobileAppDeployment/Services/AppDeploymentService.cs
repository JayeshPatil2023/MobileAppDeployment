using MobileAppDeployment.Models;
using MobileAppDeployment.Repositories;

namespace MobileAppDeployment.Services;

public class AppDeploymentService : IAppDeploymentService
{
    private readonly IAppDeploymentRepository _repository;

    public AppDeploymentService(IAppDeploymentRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<AppDeployment>> GetAllAsync() =>
        _repository.GetAllAsync();

    public Task<AppDeployment?> GetByIdAsync(int id) =>
        _repository.GetByIdAsync(id);

    public Task<int> CreateAsync(AppDeployment appDeployment) =>
        _repository.InsertAsync(appDeployment);

    public Task<bool> UpdateAsync(AppDeployment appDeployment) =>
        _repository.UpdateAsync(appDeployment);

    public Task<bool> DeleteAsync(int id) =>
        _repository.DeleteAsync(id);
}
