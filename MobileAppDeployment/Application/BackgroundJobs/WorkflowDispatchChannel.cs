using System.Threading.Channels;

namespace MobileAppDeployment.Application.BackgroundJobs;

/// <summary>
/// A bounded, thread-safe channel that queues workflow dispatch work items
/// between the MVC request thread and the background dispatch service.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why Channel&lt;T&gt; instead of Task.Run?</strong>
/// </para>
/// <para>
/// The previous implementation used <c>_ = Task.Run(async () => ...)</c> which has
/// several serious problems in production:
/// <list type="bullet">
///   <item>No graceful shutdown — the task is orphaned when the app stops.</item>
///   <item>No DI scope — EF Core DbContext and other scoped services cannot be safely
///         used from a fire-and-forget Task.Run without manually creating a scope,
///         which was done incorrectly (IServiceScopeFactory inside a Singleton).</item>
///   <item>No back-pressure — a flood of requests can queue thousands of Task.Run
///         calls without any limit.</item>
///   <item>No supervision — if the task throws, the exception is silently swallowed.</item>
/// </list>
/// </para>
/// <para>
/// <see cref="Channel{T}"/> solves all of these:
/// <list type="bullet">
///   <item>The producer (MVC controller → orchestration service) writes to the channel
///         synchronously on the request thread and returns immediately.</item>
///   <item>The consumer (<see cref="WorkflowDispatchBackgroundService"/>) is a proper
///         <see cref="BackgroundService"/> that participates in the ASP.NET Core host
///         lifecycle and receives a cancellation token when the app shuts down.</item>
///   <item>The channel is bounded (capacity = 200) so a surge of requests cannot
///         queue more work than the server can handle.</item>
///   <item>Exceptions in the consumer are logged and the consumer continues —
///         one failed dispatch does not kill the background service.</item>
/// </list>
/// </para>
/// <para>
/// Registered as a <strong>Singleton</strong> because the channel must outlive
/// individual HTTP requests and be shared between the producer and consumer.
/// </para>
/// </remarks>
public class WorkflowDispatchChannel
{
    /// <summary>Maximum number of pending work items before the producer blocks.</summary>
    private const int ChannelCapacity = 200;

    private readonly Channel<WorkflowDispatchWorkItem> _channel;

    /// <summary>Creates the channel with a bounded capacity.</summary>
    public WorkflowDispatchChannel()
    {
        // BoundedChannelFullMode.Wait: the producer awaits (blocks the request) if the
        // channel is full. This provides natural back-pressure — the HTTP request will
        // take slightly longer rather than silently dropping work items.
        _channel = Channel.CreateBounded<WorkflowDispatchWorkItem>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,   // Multiple HTTP requests can enqueue simultaneously
            SingleReader = true     // Only WorkflowDispatchBackgroundService reads
        });
    }

    /// <summary>
    /// Writes a new dispatch work item to the channel.
    /// Awaits if the channel is at capacity (back-pressure).
    /// </summary>
    public ValueTask WriteAsync(WorkflowDispatchWorkItem item, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(item, cancellationToken);

    /// <summary>
    /// Returns the reader side of the channel for the background consumer.
    /// </summary>
    public ChannelReader<WorkflowDispatchWorkItem> Reader => _channel.Reader;
}
