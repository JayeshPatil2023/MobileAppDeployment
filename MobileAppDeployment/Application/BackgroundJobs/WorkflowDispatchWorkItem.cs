namespace MobileAppDeployment.Application.BackgroundJobs;

/// <summary>
/// Represents a single unit of work to dispatch to the GitHub Actions workflow.
/// Published by <see cref="WorkflowDispatchChannel"/> and consumed by
/// <see cref="WorkflowDispatchBackgroundService"/>.
/// </summary>
/// <remarks>
/// <para>
/// This record carries all the information needed for a dispatch attempt without
/// holding references to scoped services or request objects (both of which would
/// be invalid by the time the background task runs).
/// </para>
/// <para>
/// <strong>Why a record?</strong> Work items are immutable value-like objects —
/// they represent a captured intent, not a mutable state. Using a record enforces
/// this intent and gives value-equality semantics for free.
/// </para>
/// </remarks>
/// <param name="JobId">The unique job ID for progress tracking.</param>
/// <param name="ClientName">Client name used as workflow input <c>client_name</c>.</param>
/// <param name="LogoPublicUrl">Publicly-accessible URL to the logo asset for GitHub runners.</param>
/// <param name="SplashPublicUrl">Publicly-accessible URL to the splash asset for GitHub runners.</param>
/// <param name="AppBundleId">iOS App Bundle ID passed to the workflow.</param>
/// <param name="AppId">OneSignal App ID passed to the workflow.</param>
/// <param name="ProjectId">OneSignal Project/Sender ID passed to the workflow.</param>
/// <param name="MaxRetries">Maximum dispatch attempts (initial + retries).</param>
/// <param name="RetryDelaySeconds">Seconds to wait between retry attempts.</param>
public record WorkflowDispatchWorkItem(
    string JobId,
    string ClientName,
    string LogoPublicUrl,
    string SplashPublicUrl,
    string AppBundleId,
    string AppId,
    string ProjectId,
    int MaxRetries,
    int RetryDelaySeconds);
