using System.Collections.Concurrent;

namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IWorkflowJobStore"/>.
/// </summary>
public class WorkflowJobStore : IWorkflowJobStore
{
    private readonly ConcurrentDictionary<string, WorkflowJobState> _jobs = new();

    /// <inheritdoc />
    public WorkflowJobState Create(string clientName, int maxRetries, string? savedMessage)
    {
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
}
