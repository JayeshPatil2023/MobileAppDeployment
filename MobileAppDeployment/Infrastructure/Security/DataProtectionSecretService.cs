using Microsoft.AspNetCore.DataProtection;
using MobileAppDeployment.Core.Interfaces.Services;

namespace MobileAppDeployment.Infrastructure.Security;

/// <summary>
/// Encrypts and decrypts secrets using ASP.NET Core Data Protection.
/// </summary>
public class DataProtectionSecretService : ISecretProtectionService
{
    private const string ProtectorPurpose = "OneSignalRestApiKey";
    private readonly IDataProtector _protector;

    /// <summary>
    /// Creates the secret protection service with a purpose-specific data protector.
    /// </summary>
    public DataProtectionSecretService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    /// <inheritdoc />
    public string Protect(string plaintext) => _protector.Protect(plaintext);

    /// <inheritdoc />
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
