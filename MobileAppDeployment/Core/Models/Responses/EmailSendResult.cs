namespace MobileAppDeployment.Core.Models.Responses;

/// <summary>
/// Outcome of an outbound email attempt.
/// </summary>
public sealed class EmailSendResult
{
    /// <summary>
    /// True when the SMTP server accepted the message for delivery.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Human-readable error when <see cref="Succeeded"/> is false; otherwise null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful send result.
    /// </summary>
    public static EmailSendResult Success() => new() { Succeeded = true };

    /// <summary>
    /// Creates a failed send result with a safe error message for API/logging use.
    /// </summary>
    /// <param name="errorMessage">Reason the send failed.</param>
    public static EmailSendResult Failure(string errorMessage) =>
        new() { Succeeded = false, ErrorMessage = errorMessage };
}
