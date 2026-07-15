using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MobileAppDeployment.Extensions;

/// <summary>
/// Registers ASP.NET Core rate limiting policies for abuse prevention.
/// </summary>
/// <remarks>
/// <para>
/// Three sliding-window policies are defined:
/// </para>
/// <list type="bullet">
///   <item>
///     <term>token-api</term>
///     <description>
///       Applied to <c>POST /api/form-access-tokens</c>.
///       Allows 10 requests per minute per IP. Prevents admins from accidentally
///       issuing thousands of tokens via scripts.
///     </description>
///   </item>
///   <item>
///     <term>form-submit</term>
///     <description>
///       Applied to all Create/Edit POST actions.
///       Allows 5 requests per minute per IP. Prevents form submission floods.
///     </description>
///   </item>
///   <item>
///     <term>global</term>
///     <description>
///       Applied globally as a catch-all.
///       Allows 200 requests per minute per IP.
///     </description>
///   </item>
/// </list>
/// <para>
/// All limiters use a sliding window (not fixed) to avoid thundering-herd
/// bursts at window boundaries.
/// </para>
/// </remarks>
public static class RateLimitingExtensions
{
    /// <summary>Policy name for the token-issuance admin API endpoint.</summary>
    public const string TokenApiPolicy = "token-api";

    /// <summary>Policy name for client form submission endpoints.</summary>
    public const string FormSubmitPolicy = "form-submit";

    /// <summary>Policy name applied globally to all requests.</summary>
    public const string GlobalPolicy = "global";

    /// <summary>
    /// Adds all application rate-limiting policies to the service container.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // ── 429 response ─────────────────────────────────────────────
            // Return a clean 429 with Retry-After when the limit is hit.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers["Retry-After"] = "60";
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error":"Too many requests. Please wait before trying again."}""",
                    cancellationToken);
            };

            // ── Token-issuance API: 10 req / 60 s per IP ─────────────────
            options.AddSlidingWindowLimiter(TokenApiPolicy, policy =>
            {
                policy.Window = TimeSpan.FromSeconds(60);
                policy.SegmentsPerWindow = 6;                // 10-second segments
                policy.PermitLimit = 10;
                policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                policy.QueueLimit = 0;                       // Reject immediately
            });

            // ── Client form submission: 5 req / 60 s per IP ──────────────
            options.AddSlidingWindowLimiter(FormSubmitPolicy, policy =>
            {
                policy.Window = TimeSpan.FromSeconds(60);
                policy.SegmentsPerWindow = 6;
                policy.PermitLimit = 5;
                policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                policy.QueueLimit = 0;
            });

            // ── Global catch-all: 200 req / 60 s per IP ──────────────────
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext =>
                {
                    // Partition by IP address. X-Forwarded-For is checked first
                    // so the limit applies to the real client behind a proxy/load balancer.
                    string partitionKey = httpContext.Request.Headers["X-Forwarded-For"]
                        .FirstOrDefault()
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";

                    return RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey,
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            Window = TimeSpan.FromSeconds(60),
                            SegmentsPerWindow = 6,
                            PermitLimit = 200,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        });
                });
        });

        return services;
    }
}
