using System.Security.Cryptography;
using MobileAppDeployment.Models;
using MobileAppDeployment.Repositories;

namespace MobileAppDeployment.Services;

/// <summary>
/// Default implementation of <see cref="IFormAccessTokenService"/>.
/// </summary>
public class FormAccessTokenService : IFormAccessTokenService
{
    private readonly IFormAccessTokenRepository _repository;

    /// <summary>
    /// Creates the service with the token persistence dependency.
    /// </summary>
    public FormAccessTokenService(IFormAccessTokenRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<FormAccessTokenResponse> IssueAsync(
        string clientName,
        string clientAppName,
        Func<string, string> formUrlBuilder)
    {
        string trimmedClient = clientName.Trim();
        string trimmedApp = clientAppName.Trim();

        // Re-issuing for the same client returns the same shareable link so admins
        // do not accidentally create parallel tokens for one client/app pair.
        FormAccessToken? existing = await _repository.FindActiveByClientAsync(trimmedClient, trimmedApp);
        if (existing is not null)
        {
            return ToResponse(existing, formUrlBuilder, alreadyExisted: true);
        }

        var entity = new FormAccessToken
        {
            Token = GenerateToken(),
            ClientName = trimmedClient,
            ClientAppName = trimmedApp,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };

        await _repository.InsertAsync(entity);
        return ToResponse(entity, formUrlBuilder, alreadyExisted: false);
    }

    /// <inheritdoc />
    public async Task<FormAccessToken?> ResolveActiveAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        FormAccessToken? entity = await _repository.GetByTokenAsync(token.Trim());
        if (entity is null || !entity.IsActive)
        {
            return null;
        }

        return entity;
    }

    /// <inheritdoc />
    public async Task MarkSubmittedAsync(string token, int appDeploymentId)
    {
        FormAccessToken? entity = await _repository.GetByTokenAsync(token.Trim());
        if (entity is null)
        {
            return;
        }

        // First submit only — later edits keep the original AppDeploymentId link.
        if (entity.AppDeploymentId.HasValue)
        {
            return;
        }

        entity.AppDeploymentId = appDeploymentId;
        entity.SubmittedUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
    }

    /// <summary>
    /// Generates a URL-safe opaque token (32 random bytes → 43-char Base64Url).
    /// </summary>
    private static string GenerateToken()
    {
        // 256 bits of entropy is enough for an unguessable magic link without needing JWT claims.
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static FormAccessTokenResponse ToResponse(
        FormAccessToken entity,
        Func<string, string> formUrlBuilder,
        bool alreadyExisted)
    {
        return new FormAccessTokenResponse
        {
            Token = entity.Token,
            ClientName = entity.ClientName,
            ClientAppName = entity.ClientAppName,
            FormUrl = formUrlBuilder(entity.Token),
            AlreadyExisted = alreadyExisted,
            IsSubmitted = entity.AppDeploymentId.HasValue
        };
    }
}
