using MobileAppDeployment.Web.Middleware;

namespace MobileAppDeployment.Extensions;

/// <summary>
/// Organizes the ASP.NET Core HTTP middleware pipeline into named extension methods.
/// </summary>
/// <remarks>
/// <para>
/// Middleware <strong>order matters</strong> in ASP.NET Core. The methods below are
/// designed to be called in a specific sequence from <c>Program.cs</c>.
/// </para>
/// <para>Order (outermost to innermost):</para>
/// <list type="number">
///   <item><see cref="UseSecurityMiddleware"/> — exception handler + security headers</item>
///   <item>HTTPS redirect + HSTS</item>
///   <item>Static files</item>
///   <item>Rate limiting</item>
///   <item>Routing</item>
///   <item>Authorization</item>
///   <item>Endpoint mapping</item>
/// </list>
/// </remarks>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Registers exception handling and security headers as the outermost middleware layers.
    /// </summary>
    /// <remarks>
    /// Must be called FIRST so that:
    /// <list type="bullet">
    ///   <item>The global exception handler wraps all downstream middleware.</item>
    ///   <item>Security headers are added to every response, including error responses and static files.</item>
    /// </list>
    /// </remarks>
    public static WebApplication UseSecurityMiddleware(this WebApplication app)
    {
        // ── Global exception handler ───────────────────────────────────────
        // Wrap the entire pipeline so unhandled exceptions are caught,
        // logged, and returned as a structured response without leaking internals.
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // ── Security headers ───────────────────────────────────────────────
        // Added before static files so every response (HTML, JSON, and assets)
        // carries the headers.
        app.UseMiddleware<SecurityHeadersMiddleware>();

        return app;
    }

    /// <summary>
    /// Configures health check endpoints.
    /// </summary>
    /// <remarks>
    /// The /health endpoint is intentionally public (no authentication required)
    /// so that load balancers and monitoring systems can reach it.
    /// The response is plain text to avoid any layout rendering overhead.
    /// </remarks>
    public static WebApplication UseAppHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        return app;
    }
}
