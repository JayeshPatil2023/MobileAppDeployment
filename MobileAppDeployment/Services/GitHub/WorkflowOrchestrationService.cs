using Microsoft.Extensions.Options;

namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Uploads client assets, dispatches the base GitHub Actions workflow with configurable retries,
/// and streams progress updates to <see cref="IWorkflowJobStore"/>.
/// </summary>
/// <remarks>
/// Asset upload runs on the request thread (while <see cref="IFormFile"/> streams are valid).
/// Only the GitHub API dispatch is retried in the background.
/// </remarks>
public class WorkflowOrchestrationService : IWorkflowOrchestrationService
{
    private static readonly string[] PngOnly = [".png"];

    private readonly IWorkflowJobStore _jobStore;
    private readonly IWorkflowAssetStorageService _assetStorage;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GitHubWorkflowDispatchOptions _options;
    private readonly ILogger<WorkflowOrchestrationService> _logger;

    public WorkflowOrchestrationService(
        IWorkflowJobStore jobStore,
        IWorkflowAssetStorageService assetStorage,
        IServiceScopeFactory scopeFactory,
        IOptions<GitHubWorkflowDispatchOptions> options,
        ILogger<WorkflowOrchestrationService> logger)
    {
        _jobStore = jobStore;
        _assetStorage = assetStorage;
        _scopeFactory = scopeFactory;
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

            // Upload while FormFile streams from the HTTP request are still valid.
            string logoUrl = await _assetStorage.SaveAndGetPublicUrlAsync(
                request.LogoFile,
                clientName,
                "logo",
                PngOnly);

            string splashUrl = await _assetStorage.SaveAndGetPublicUrlAsync(
                request.SplashFile,
                clientName,
                "splash",
                PngOnly);

            _jobStore.UpdateProgress(
                job.JobId,
                50,
                "Assets saved. Queuing base workflow dispatch...",
                WorkflowJobStatus.Running,
                attempt: 1);

            DispatchPayload payload = new(
                job.JobId,
                clientName,
                logoUrl,
                splashUrl,
                request.AppBundleId.Trim(),
                request.AppId.Trim(),
                request.ProjectId.Trim());

            // Fire-and-forget dispatch with retries; UI polls the job store.
            _ = Task.Run(() => ExecuteDispatchWithRetriesAsync(payload), CancellationToken.None);
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
    public WorkflowJobState? GetJob(string jobId) => _jobStore.Get(jobId);

    /// <summary>
    /// Retries workflow_dispatch against the GitHub API until success or MaxRetries is exhausted.
    /// </summary>
    private async Task ExecuteDispatchWithRetriesAsync(DispatchPayload payload)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IGitHubWorkflowDispatchService dispatchService =
            scope.ServiceProvider.GetRequiredService<IGitHubWorkflowDispatchService>();
        IWorkflowJobStore jobStore = scope.ServiceProvider.GetRequiredService<IWorkflowJobStore>();
        GitHubWorkflowDispatchOptions options = scope.ServiceProvider
            .GetRequiredService<IOptions<GitHubWorkflowDispatchOptions>>()
            .Value;

        int maxAttempts = Math.Max(1, options.MaxRetries);
        string? lastError = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            WorkflowJobStatus status = attempt == 1 ? WorkflowJobStatus.Running : WorkflowJobStatus.Retrying;
            string message = attempt == 1
                ? "Dispatching base workflow to GitHub Actions..."
                : $"Retrying workflow dispatch (attempt {attempt} of {maxAttempts})...";

            jobStore.UpdateProgress(payload.JobId, 60 + (attempt * 5), message, status, attempt);

            try
            {
                GitHubWorkflowDispatchResult result = await dispatchService.TriggerAsync(
                    payload.ClientName,
                    payload.LogoUrl,
                    payload.SplashUrl,
                    payload.AppBundleId,
                    payload.AppId,
                    payload.ProjectId);

                if (result.Success)
                {
                    jobStore.MarkCompleted(payload.JobId, result.Message);
                    _logger.LogInformation(
                        "Base workflow triggered for {ClientName} on attempt {Attempt}",
                        payload.ClientName,
                        attempt);
                    return;
                }

                lastError = result.Message;
                _logger.LogWarning(
                    "Workflow dispatch attempt {Attempt}/{MaxAttempts} failed for {ClientName}: {Error}",
                    attempt,
                    maxAttempts,
                    payload.ClientName,
                    lastError);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "Workflow dispatch attempt {Attempt}/{MaxAttempts} threw for {ClientName}",
                    attempt,
                    maxAttempts,
                    payload.ClientName);
            }

            if (attempt < maxAttempts)
            {
                int delaySeconds = Math.Max(0, options.RetryDelaySeconds);
                jobStore.UpdateProgress(
                    payload.JobId,
                    40,
                    $"Workflow dispatch failed — waiting {delaySeconds}s before retry {attempt + 1} of {maxAttempts}.",
                    WorkflowJobStatus.Retrying,
                    attempt);

                if (delaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }

        jobStore.MarkFailed(
            payload.JobId,
            lastError ?? "Base workflow trigger failed after all retry attempts.");
    }

    /// <summary>
    /// Validates required inputs before queuing the job.
    /// </summary>
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

    /// <summary>
    /// Data required for background dispatch retries (no file streams).
    /// </summary>
    private sealed record DispatchPayload(
        string JobId,
        string ClientName,
        string LogoUrl,
        string SplashUrl,
        string AppBundleId,
        string AppId,
        string ProjectId);
}
