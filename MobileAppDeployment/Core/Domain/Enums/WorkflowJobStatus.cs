namespace MobileAppDeployment.Core.Domain.Enums;

/// <summary>
/// Lifecycle status for a base workflow dispatch job shown in the progress UI.
/// </summary>
public enum WorkflowJobStatus
{
    /// <summary>Job has been accepted and is waiting to run.</summary>
    Queued,

    /// <summary>Job is currently executing a GitHub workflow dispatch attempt.</summary>
    Running,

    /// <summary>Job failed an attempt and will retry.</summary>
    Retrying,

    /// <summary>Job finished successfully.</summary>
    Completed,

    /// <summary>Job exhausted retries or failed permanently.</summary>
    Failed
}
