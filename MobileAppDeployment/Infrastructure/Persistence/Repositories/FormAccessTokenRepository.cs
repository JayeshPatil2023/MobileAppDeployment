using Microsoft.EntityFrameworkCore;

namespace MobileAppDeployment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IFormAccessTokenRepository"/>.
/// </summary>
/// <remarks>
/// Repositories stage changes only; <see cref="IUnitOfWork.SaveChangesAsync"/> commits them.
/// </remarks>
public class FormAccessTokenRepository : IFormAccessTokenRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>Creates a repository bound to the application database context.</summary>
    public FormAccessTokenRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<FormAccessToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return _dbContext.FormAccessTokens
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FormAccessToken?> FindActiveByClientAsync(
        string clientName,
        string clientAppName,
        CancellationToken cancellationToken = default)
    {
        List<FormAccessToken> activeTokens = await _dbContext.FormAccessTokens
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return activeTokens.FirstOrDefault(x =>
            string.Equals(x.ClientName.Trim(), clientName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ClientAppName.Trim(), clientAppName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public Task InsertAsync(FormAccessToken entity, CancellationToken cancellationToken = default)
    {
        _dbContext.FormAccessTokens.Add(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(FormAccessToken entity, CancellationToken cancellationToken = default)
    {
        _dbContext.FormAccessTokens.Update(entity);
        return Task.CompletedTask;
    }
}
