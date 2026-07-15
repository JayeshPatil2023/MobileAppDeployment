using System.Collections.Concurrent;
using System.Reflection;
using MobileAppDeployment.Core.Models.Jobs;
using MobileAppDeployment.Infrastructure.GitHub;

namespace MobileAppDeployment.Tests;

/// <summary>
/// Unit tests for <see cref="WorkflowJobStore"/> eviction behavior.
/// </summary>
public class WorkflowJobStoreTests
{
    /// <summary>
    /// Enforces the hard cap of 1000 stored jobs when more than 1000 terminal jobs are created.
    /// </summary>
    [Fact]
    public void Create_EvictsOldestTerminalJobs_WhenCountExceeds1000()
    {
        var store = new WorkflowJobStore();

        for (int i = 0; i < 1001; i++)
        {
            WorkflowJobState job = store.Create($"client-{i}", maxRetries: 1, savedMessage: null);
            store.MarkCompleted(job.JobId, "done");
        }

        store.Create("overflow-client", maxRetries: 1, savedMessage: null);

        int terminalCount = GetTerminalJobCount(store);
        Assert.True(terminalCount <= 1000);
    }

    private static int GetTerminalJobCount(WorkflowJobStore store)
    {
        FieldInfo? field = typeof(WorkflowJobStore).GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var jobs = (ConcurrentDictionary<string, WorkflowJobState>)field.GetValue(store)!;
        return jobs.Values.Count(job => job.IsTerminal);
    }
}
