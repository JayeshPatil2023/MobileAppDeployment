using Microsoft.EntityFrameworkCore;
using MobileAppDeployment.Data;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IFormAccessTokenRepository"/>.
/// </summary>
public class FormAccessTokenRepository : IFormAccessTokenRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Creates a repository bound to the application database context.
    /// </summary>
    public FormAccessTokenRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<FormAccessToken?> GetByTokenAsync(string token)
    {
        return _dbContext.FormAccessTokens
            .FirstOrDefaultAsync(x => x.Token == token);
    }

    /// <inheritdoc />
    public Task<FormAccessToken?> FindActiveByClientAsync(string clientName, string clientAppName)
    {
        // Match on trimmed, case-insensitive names so "Acme" / "acme" resolve to one token.
        string normalizedClient = clientName.Trim().ToLowerInvariant();
        string normalizedApp = clientAppName.Trim().ToLowerInvariant();

        return _dbContext.FormAccessTokens
            .Where(x => x.IsActive)
            .FirstOrDefaultAsync(x =>
                x.ClientName.ToLower() == normalizedClient &&
                x.ClientAppName.ToLower() == normalizedApp);
    }

    /// <inheritdoc />
    public async Task<FormAccessToken> InsertAsync(FormAccessToken entity)
    {
        _dbContext.FormAccessTokens.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(FormAccessToken entity)
    {
        _dbContext.FormAccessTokens.Update(entity);
        await _dbContext.SaveChangesAsync();
    }
}
