using System.ComponentModel.DataAnnotations;

namespace MobileAppDeployment.Models;

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
}
