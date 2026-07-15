using MobileAppDeployment.Core.Interfaces.Infrastructure;

namespace MobileAppDeployment.Application.BackgroundJobs;

/// <summary>
/// Long-running background service that consumes workflow dispatch work items
/// from <see cref="WorkflowDispatchChannel"/> and calls the GitHub API.
/// </summary>
/// <remarks>
/// <para>
/// This service replaces the previous <c>_ = Task.Run(async () => ...)</c>
/// fire-and-forget pattern in <c>WorkflowOrchestrationService</c>. See
/// <see cref="WorkflowDispatchChannel"/> for the full rationale.
/// </para>
/// <para>
/// <strong>Lifecycle:</strong>
/// <list type="bullet">
///   <item>Started automatically by the ASP.NET Core host at application startup.</item>
///   <item>Receives a <see cref="CancellationToken"/> when the host signals shutdown,
///         giving it a chance to complete the current item before stopping.</item>
///   <item>Never stops the loop on a failed dispatch — exceptions are logged and
///         the loop continues processing the next item.</item>
/// </list>
/// </para>
/// <para>
/// <strong>DI Scoping:</strong>
/// This service is a Singleton (as all <see cref="BackgroundService"/> subclasses are).
/// Scoped services (<see cref="IGitHubWorkflowDispatchService"/>) are resolved from a
/// new DI scope per work item. <see cref="IWorkflowJobStore"/> is a Singleton and can
/// be injected directly.
/// </para>
/// </remarks>
public class WorkflowDispatchBackgroundService : BackgroundService
{
    private readonly WorkflowDispatchChannel _channel;
    private readonly IWorkflowJobStore _jobStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowDispatchBackgroundService> _logger;

    /// <summary>Creates the background dispatch service.</summary>
    public WorkflowDispatchBackgroundService(
        WorkflowDispatchChannel channel,
        IWorkflowJobStore jobStore,
        IServiceScopeFactory scopeFactory,
        ILogger<WorkflowDispatchBackgroundService> logger)
    {
        _channel = channel;
        _jobStore = jobStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads work items from the channel one at a time and processes each with a fresh
    /// DI scope. The loop exits cleanly when <paramref name="stoppingToken"/> is cancelled
    /// (i.e., the app is shutting down) or the channel writer has been closed.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Service} started. Waiting for workflow dispatch work items.",
            nameof(WorkflowDispatchBackgroundService));

        await foreach (WorkflowDispatchWorkItem item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessWorkItemAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // App is shutting down — stop processing and let the host exit cleanly.
                _logger.LogInformation(
                    "{Service} stopping due to application shutdown. Current item: {JobId}.",
                    nameof(WorkflowDispatchBackgroundService), item.JobId);
                break;
            }
            catch (Exception ex)
            {
                // Log and continue — one failed dispatch should not stop the entire service.
                _logger.LogError(ex,
                    "Unhandled exception processing dispatch work item {JobId} for {ClientName}.",
                    item.JobId, item.ClientName);
            }
        }

        _logger.LogInformation("{Service} stopped.", nameof(WorkflowDispatchBackgroundService));
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task ProcessWorkItemAsync(WorkflowDispatchWorkItem item, CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Processing dispatch work item {JobId} for client '{ClientName}'.",
            item.JobId, item.ClientName);

        // Create a new DI scope for scoped services (e.g. typed HttpClient for GitHub API).
        // IWorkflowJobStore is Singleton and injected directly — no scope needed.
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var dispatchService = scope.ServiceProvider.GetRequiredService<IGitHubWorkflowDispatchService>();

        WorkflowJobState? job = _jobStore.Get(item.JobId);
        if (job is null)
        {
            _logger.LogWarning("Work item {JobId} has no corresponding job in the store. Skipping.", item.JobId);
            return;
        }

        // ── Attempt dispatch with retry ────────────────────────────────────
        for (int attempt = 1; attempt <= item.MaxRetries; attempt++)
        {
            _jobStore.UpdateProgress(
                item.JobId,
                percent: 20 + (attempt - 1) * 15,
                message: attempt == 1
                    ? "Dispatching workflow to GitHub Actions..."
                    : $"Retrying ({attempt}/{item.MaxRetries})...",
                status: attempt == 1 ? WorkflowJobStatus.Running : WorkflowJobStatus.Retrying,
                attempt: attempt);

            _logger.LogInformation(
                "Dispatch attempt {Attempt}/{MaxRetries} for job {JobId}.",
                attempt, item.MaxRetries, item.JobId);

            try
            {
                GitHubWorkflowDispatchResult result = await dispatchService.TriggerAsync(
                    clientName: item.ClientName,
                    logoBlobUrl: item.LogoPublicUrl,
                    splashBlobUrl: item.SplashPublicUrl,
                    appBundleId: item.AppBundleId,
                    appId: item.AppId,
                    projectId: item.ProjectId,
                    cancellationToken: stoppingToken);

                if (result.Success)
                {
                    _jobStore.MarkCompleted(
                        item.JobId,
                        "Workflow dispatched successfully. GitHub Actions will process your deployment.");

                    _logger.LogInformation(
                        "Workflow dispatch succeeded for job {JobId} (attempt {Attempt}).",
                        item.JobId, attempt);

                    return;
                }

                // Dispatch call succeeded HTTP-wise but reported a non-success result.
                throw new InvalidOperationException(
                    result.Message ?? "GitHub API returned a non-success response.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // App shutdown — stop retrying.
                _jobStore.MarkFailed(item.JobId, "Deployment cancelled due to application shutdown.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Dispatch attempt {Attempt}/{MaxRetries} failed for job {JobId}: {Message}",
                    attempt, item.MaxRetries, item.JobId, ex.Message);

                if (attempt < item.MaxRetries)
                {
                    // Wait before retrying.
                    await Task.Delay(TimeSpan.FromSeconds(item.RetryDelaySeconds), stoppingToken);
                }
                else
                {
                    // All attempts exhausted.
                    _jobStore.MarkFailed(
                        item.JobId,
                        $"All {item.MaxRetries} dispatch attempts failed. Last error: {ex.Message}");

                    _logger.LogError(ex,
                        "All {MaxRetries} dispatch attempts failed for job {JobId}.",
                        item.MaxRetries, item.JobId);
                }
            }
        }
    }
}
