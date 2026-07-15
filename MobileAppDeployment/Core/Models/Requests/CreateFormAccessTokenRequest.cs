using System.ComponentModel.DataAnnotations;

namespace MobileAppDeployment.Core.Models.Requests;

/// <summary>
/// Request body for <c>POST /api/form-access-tokens</c>.
/// </summary>
public class CreateFormAccessTokenRequest
{
    /// <summary>
    /// Client / organization name used when issuing the token.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Client application name; used to prefill the deployment form App Name.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string ClientAppName { get; set; } = string.Empty;

    /// <summary>
    /// Optional client email. When provided, the issued form URL is emailed to this address.
    /// </summary>
    /// <remarks>
    /// Email is never required for token issuance. Leave null/empty to skip sending.
    /// When set, must be a valid email address format.
    /// Whitespace-only values are treated as omitted so validation stays optional.
    /// </remarks>
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(255)]
    public string? Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string? _email;
}
