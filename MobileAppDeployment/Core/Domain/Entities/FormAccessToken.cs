using System.ComponentModel.DataAnnotations;

namespace MobileAppDeployment.Core.Domain.Entities;

/// <summary>
/// Represents a single-client magic-link token that gates access to the New App Deployment form.
/// </summary>
/// <remarks>
/// Admin generates a token via the protected API; the client opens
/// <c>/AppDeployment/Form/{Token}</c>. Until the client submits the form,
/// <see cref="AppDeploymentId"/> stays null (create mode). After the first
/// successful submit, the token points at the saved deployment so later visits
/// open the edit form instead.
/// </remarks>
public class FormAccessToken
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Cryptographically random opaque token embedded in the client form URL.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable client / organization name supplied when the token was issued.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable client app name supplied when the token was issued.
    /// Prefills <see cref="AppDeployment.AppName"/> on first visit.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string ClientAppName { get; set; } = string.Empty;

    /// <summary>
    /// Linked deployment after the client completes the first form submit; null means create mode.
    /// </summary>
    public int? AppDeploymentId { get; set; }

    /// <summary>
    /// Navigation to the deployment created through this token, when linked.
    /// </summary>
    public AppDeployment? AppDeployment { get; set; }

    /// <summary>
    /// When false, the form URL is rejected even if the token string is known.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UTC timestamp when the admin issued the token.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// UTC timestamp of the client's first successful form submit; null until submitted.
    /// </summary>
    public DateTime? SubmittedUtc { get; set; }
}
