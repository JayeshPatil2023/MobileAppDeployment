using Microsoft.Extensions.Options;
using MobileAppDeployment.Application.BackgroundJobs;
using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Application.Services;

/// <summary>
/// Uploads client assets (on the request thread) and enqueues the GitHub Actions
/// workflow dispatch to the <see cref="WorkflowDispatchChannel"/> for background processing.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why two phases?</strong>
/// </para>
/// <para>
/// <see cref="IFormFile"/> streams are only valid during the HTTP request that uploaded them.
/// Once the request ends, the stream is disposed. Asset upload <em>must</em> happen on the
/// request thread before the response is sent back to the client.
/// </para>
/// <para>
/// The GitHub API dispatch does not need the uploaded streams — it only needs the public URL
/// of the already-saved files. So the dispatch is enqueued to a
/// <see cref="WorkflowDispatchChannel"/> and executed by <see cref="WorkflowDispatchBackgroundService"/>.
/// </para>
/// <para>
/// <strong>Why Channel instead of Task.Run?</strong>
/// See <see cref="WorkflowDispatchChannel"/> for the detailed rationale. In short:
/// graceful shutdown, proper DI scoping, bounded back-pressure, and structured error handling.
/// </para>
/// </remarks>
public class WorkflowOrchestrationService : IWorkflowOrchestrationService
{
    private static readonly string[] PngOnly = [".png"];

    private readonly IWorkflowJobStore _jobStore;
    private readonly IWorkflowAssetStorageService _assetStorage;
    private readonly WorkflowDispatchChannel _dispatchChannel;
    private readonly GitHubWorkflowDispatchOptions _options;
    private readonly ILogger<WorkflowOrchestrationService> _logger;

    /// <summary>Creates the orchestration service.</summary>
    public WorkflowOrchestrationService(
        IWorkflowJobStore jobStore,
        IWorkflowAssetStorageService assetStorage,
        WorkflowDispatchChannel dispatchChannel,
        IOptions<GitHubWorkflowDispatchOptions> options,
        ILogger<WorkflowOrchestrationService> logger)
    {
        _jobStore = jobStore;
        _assetStorage = assetStorage;
        _dispatchChannel = dispatchChannel;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> StartWorkflowJobAsync(
        WorkflowDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        int maxAttempts = Math.Max(1, _options.MaxRetries);
        string clientName = request.ClientName.Trim();
        WorkflowJobState job = _jobStore.Create(clientName, maxAttempts, request.SavedMessage);

        try
        {
            _jobStore.UpdateProgress(
                job.JobId,
                20,
                "Saving logo and splash images for GitHub Actions download...",
                WorkflowJobStatus.Running,
                attempt: 1);

            // ── Phase 1: Upload assets on the request thread ───────────────
            // IFormFile streams are valid only during this HTTP request.
            string logoUrl = await _assetStorage.SaveAndGetPublicUrlAsync(
                request.LogoFile,
                clientName,
                "logo",
                PngOnly,
                cancellationToken);

            string splashUrl = await _assetStorage.SaveAndGetPublicUrlAsync(
                request.SplashFile,
                clientName,
                "splash",
                PngOnly,
                cancellationToken);

            _jobStore.UpdateProgress(
                job.JobId,
                50,
                "Assets saved. Queuing workflow dispatch...",
                WorkflowJobStatus.Running,
                attempt: 1);

            // ── Phase 2: Enqueue dispatch to background channel ────────────
            // The channel consumer (WorkflowDispatchBackgroundService) will call
            // GitHub API and update job state. No Task.Run needed.
            await _dispatchChannel.WriteAsync(new WorkflowDispatchWorkItem(
                JobId: job.JobId,
                ClientName: clientName,
                LogoPublicUrl: logoUrl,
                SplashPublicUrl: splashUrl,
                AppBundleId: request.AppBundleId.Trim(),
                AppId: request.AppId.Trim(),
                ProjectId: request.ProjectId.Trim(),
                MaxRetries: maxAttempts,
                RetryDelaySeconds: _options.RetryDelaySeconds),
                cancellationToken);

            _logger.LogInformation(
                "Workflow job {JobId} queued for client '{ClientName}'.",
                job.JobId, clientName);

            return job.JobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare workflow job for {ClientName}", clientName);
            _jobStore.MarkFailed(job.JobId, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> StartWorkflowJobFromDeploymentAsync(
        AppDeployment deployment,
        string? savedMessage = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDeployment(deployment);

        int maxAttempts = Math.Max(1, _options.MaxRetries);
        string clientName = deployment.AppName.Trim();
        string banner = savedMessage ?? "Starting app deployment process...";
        WorkflowJobState job = _jobStore.Create(clientName, maxAttempts, banner);

        try
        {
            _jobStore.UpdateProgress(
                job.JobId,
                20,
                "Publishing saved logo and splash images for GitHub Actions download...",
                WorkflowJobStatus.Running,
                attempt: 1);

            // ── Phase 1: Publish saved assets to public workflow-assets folder ──
            // Files are already on disk — we copy them to the publicly-accessible
            // workflow-assets directory so GitHub Actions runners can download them.
            string logoUrl = await _assetStorage.PublishStoredFileAndGetPublicUrlAsync(
                deployment.MobileAppIconPath!,
                clientName,
                "logo",
                PngOnly,
                cancellationToken);

            string splashUrl = await _assetStorage.PublishStoredFileAndGetPublicUrlAsync(
                deployment.LaunchImagePath!,
                clientName,
                "splash",
                PngOnly,
                cancellationToken);

            _jobStore.UpdateProgress(
                job.JobId,
                50,
                "Assets published. Queuing workflow dispatch...",
                WorkflowJobStatus.Running,
                attempt: 1);

            // ── Phase 2: Enqueue dispatch to background channel ────────────
            await _dispatchChannel.WriteAsync(new WorkflowDispatchWorkItem(
                JobId: job.JobId,
                ClientName: clientName,
                LogoPublicUrl: logoUrl,
                SplashPublicUrl: splashUrl,
                AppBundleId: deployment.IosBundleId.Trim(),
                AppId: deployment.OneSignalAppId.Trim(),
                ProjectId: deployment.OneSignalSenderId.Trim(),
                MaxRetries: maxAttempts,
                RetryDelaySeconds: _options.RetryDelaySeconds),
                cancellationToken);

            _logger.LogInformation(
                "Workflow job {JobId} queued from deployment {DeploymentId} for client '{ClientName}'.",
                job.JobId, deployment.Id, clientName);

            return job.JobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare workflow job for deployment {DeploymentId}", deployment.Id);
            _jobStore.MarkFailed(job.JobId, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public WorkflowJobState? GetJob(string jobId) => _jobStore.Get(jobId);

    // ── Validation ────────────────────────────────────────────────────────

    /// <summary>
    /// Validates a saved deployment has every field and asset path required to start the workflow.
    /// </summary>
    private static void ValidateDeployment(AppDeployment deployment)
    {
        if (string.IsNullOrWhiteSpace(deployment.AppName))
        {
            throw new InvalidOperationException("App name is required to start the deployment workflow.");
        }

        if (string.IsNullOrWhiteSpace(deployment.MobileAppIconPath))
        {
            throw new InvalidOperationException(
                "Mobile app icon is required. Save the deployment with a logo before starting the workflow.");
        }

        if (string.IsNullOrWhiteSpace(deployment.LaunchImagePath))
        {
            throw new InvalidOperationException(
                "Launch image is required. Save the deployment with a splash image before starting the workflow.");
        }

        if (string.IsNullOrWhiteSpace(deployment.IosBundleId))
        {
            throw new InvalidOperationException("iOS bundle ID is required to start the deployment workflow.");
        }

        if (string.IsNullOrWhiteSpace(deployment.OneSignalAppId))
        {
            throw new InvalidOperationException("OneSignal App ID is required to start the deployment workflow.");
        }

        if (string.IsNullOrWhiteSpace(deployment.OneSignalSenderId))
        {
            throw new InvalidOperationException("OneSignal Sender ID is required to start the deployment workflow.");
        }
    }

    /// <summary>Validates required inputs before queuing the job.</summary>
    private static void ValidateRequest(WorkflowDispatchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName))
        {
            throw new InvalidOperationException("Client name is required to trigger the workflow.");
        }

        if (request.LogoFile.Length == 0)
        {
            throw new InvalidOperationException("Logo / mobile app icon is required to trigger the workflow.");
        }

        if (request.SplashFile.Length == 0)
        {
            throw new InvalidOperationException("Splash / launch image is required to trigger the workflow.");
        }

        if (string.IsNullOrWhiteSpace(request.AppBundleId))
        {
            throw new InvalidOperationException("App bundle ID is required to trigger the workflow.");
        }

        if (string.IsNullOrWhiteSpace(request.AppId))
        {
            throw new InvalidOperationException("OneSignal App ID is required to trigger the workflow.");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            throw new InvalidOperationException("OneSignal Project ID is required to trigger the workflow.");
        }
    }
}
