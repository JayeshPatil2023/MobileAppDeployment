namespace MobileAppDeployment.Core.Interfaces.Services;

/// <summary>
/// Abstraction for encrypting and decrypting sensitive string values at rest.
/// </summary>
public interface ISecretProtectionService
{
    /// <summary>Encrypts a plaintext secret for storage.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypts a protected secret back to plaintext.</summary>
    string Unprotect(string ciphertext);
}
