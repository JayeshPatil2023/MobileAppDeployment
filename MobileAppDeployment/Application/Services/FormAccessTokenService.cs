using System.Security.Cryptography;

namespace MobileAppDeployment.Application.Services;

/// <summary>
/// Default implementation of <see cref="IFormAccessTokenService"/>.
/// </summary>
public class FormAccessTokenService : IFormAccessTokenService
{
    private readonly IFormAccessTokenRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Creates the service with the token persistence dependency.
    /// </summary>
    public FormAccessTokenService(IFormAccessTokenRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<FormAccessTokenResponse> IssueAsync(
        string clientName,
        string clientAppName,
        Func<string, string> formUrlBuilder)
    {
        string trimmedClient = clientName.Trim();
        string trimmedApp = clientAppName.Trim();

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
        await _unitOfWork.SaveChangesAsync();
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

        if (entity.AppDeploymentId.HasValue)
        {
            return;
        }

        entity.AppDeploymentId = appDeploymentId;
        entity.SubmittedUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Generates a URL-safe opaque token (32 random bytes → 43-char Base64Url).
    /// </summary>
    private static string GenerateToken()
    {
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
