using System.Collections.Concurrent;

namespace MobileAppDeployment.Infrastructure.GitHub;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IWorkflowJobStore"/>
/// with automatic eviction of terminal (completed/failed) jobs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Eviction policy:</strong> Terminal jobs older than <see cref="JobRetentionMinutes"/>
/// minutes are removed on each <see cref="Create"/> call. This is a simple lazy-eviction
/// strategy that works well when dispatch frequency is low (one per deployment trigger).
/// </para>
/// <para>
/// <strong>Why not a Timer?</strong> A background timer would require this class to implement
/// <see cref="IDisposable"/> and complicates thread-safety. Lazy eviction on every write keeps
/// the class simple and is sufficient for expected usage patterns. For high-volume scenarios,
/// replace with a dedicated IHostedService eviction sweep.
/// </para>
/// <para>
/// <strong>Memory bound:</strong> At most <see cref="MaxJobCount"/> jobs are retained at any
/// time (a safety cap independent of eviction). If this limit is exceeded, the oldest terminal
/// job is forcibly removed.
/// </para>
/// </remarks>
public class WorkflowJobStore : IWorkflowJobStore
{
    /// <summary>
    /// Completed/failed jobs are retained for this many minutes before eviction.
    /// Keeps the progress page accessible for a reasonable time after deployment completes.
    /// </summary>
    private const int JobRetentionMinutes = 60;

    /// <summary>
    /// Hard upper bound on stored jobs to prevent unbounded memory growth even if
    /// eviction is slower than job creation.
    /// </summary>
    private const int MaxJobCount = 1000;

    private readonly ConcurrentDictionary<string, WorkflowJobState> _jobs = new();

    /// <inheritdoc />
    public WorkflowJobState Create(string clientName, int maxRetries, string? savedMessage)
    {
        // Evict stale terminal jobs before creating a new one.
        EvictExpiredJobs();

        WorkflowJobState job = new()
        {
            JobId = Guid.NewGuid().ToString("N"),
            ClientName = clientName,
            SavedMessage = savedMessage,
            MaxRetries = maxRetries,
            Status = WorkflowJobStatus.Queued,
            PercentComplete = 0,
            CurrentMessage = "Queued — preparing to trigger the base workflow.",
            Attempt = 0
        };

        _jobs[job.JobId] = job;
        return job;
    }

    /// <inheritdoc />
    public WorkflowJobState? Get(string jobId) =>
        _jobs.TryGetValue(jobId, out WorkflowJobState? job) ? job : null;

    /// <inheritdoc />
    public void UpdateProgress(string jobId, int percent, string message, WorkflowJobStatus status, int attempt)
    {
        if (!_jobs.TryGetValue(jobId, out WorkflowJobState? job))
        {
            return;
        }

        job.Status = status;
        job.PercentComplete = Math.Clamp(percent, 0, 100);
        job.CurrentMessage = message;
        job.Attempt = attempt;
    }

    /// <inheritdoc />
    public void MarkCompleted(string jobId, string message)
    {
        if (!_jobs.TryGetValue(jobId, out WorkflowJobState? job))
        {
            return;
        }

        job.Status = WorkflowJobStatus.Completed;
        job.PercentComplete = 100;
        job.CurrentMessage = message;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.ErrorMessage = null;
    }

    /// <inheritdoc />
    public void MarkFailed(string jobId, string errorMessage)
    {
        if (!_jobs.TryGetValue(jobId, out WorkflowJobState? job))
        {
            return;
        }

        job.Status = WorkflowJobStatus.Failed;
        job.CurrentMessage = "Base workflow trigger failed.";
        job.ErrorMessage = errorMessage;
        job.CompletedAt = DateTimeOffset.UtcNow;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Removes terminal jobs that have exceeded the retention window.
    /// If the total job count still exceeds <see cref="MaxJobCount"/>, removes
    /// the oldest terminal jobs until the count is within the limit.
    /// </summary>
    private void EvictExpiredJobs()
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddMinutes(-JobRetentionMinutes);

        // Remove jobs that finished before the retention cutoff.
        foreach (KeyValuePair<string, WorkflowJobState> kvp in _jobs)
        {
            if (kvp.Value.IsTerminal && kvp.Value.CompletedAt < cutoff)
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }

        // Safety cap: if still over MaxJobCount, remove the oldest terminal jobs.
        if (_jobs.Count > MaxJobCount)
        {
            IEnumerable<KeyValuePair<string, WorkflowJobState>> oldest = _jobs
                .Where(kvp => kvp.Value.IsTerminal)
                .OrderBy(kvp => kvp.Value.CompletedAt)
                .Take(_jobs.Count - MaxJobCount);

            foreach (KeyValuePair<string, WorkflowJobState> kvp in oldest)
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}
