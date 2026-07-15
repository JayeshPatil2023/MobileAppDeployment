using System.Net.Mime;
using System.Text.Json;

namespace MobileAppDeployment.Web.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns a structured JSON or
/// HTML error response without exposing internal details to the client.
/// </summary>
/// <remarks>
/// <para>
/// In <strong>Development</strong> the full exception message and stack trace
/// are included in the response body to aid debugging.
/// In any other environment only a generic error message is returned so that
/// internal implementation details are never exposed.
/// </para>
/// <para>
/// The response format follows RFC 7807 (Problem Details for HTTP APIs).
/// MVC views receive a redirect to the Error page; API requests (Accept:
/// application/json) receive a JSON Problem Details object.
/// </para>
/// <para>
/// Registration: add as the FIRST middleware in <c>Program.cs</c> so every
/// downstream exception is caught.
/// </para>
/// </remarks>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    /// <summary>Creates the middleware.</summary>
    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not a server error; no logging needed.
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        // Do not overwrite a response that has already started streaming.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Decide response format based on Accept header.
        bool wantsJson = context.Request.Headers.Accept
            .ToString()
            .Contains("application/json", StringComparison.OrdinalIgnoreCase);

        if (wantsJson)
        {
            await WriteJsonProblemDetailsAsync(context, ex);
        }
        else
        {
            // For HTML requests, redirect to the Error view so the layout renders.
            context.Response.Redirect("/Home/Error");
        }
    }

    private async Task WriteJsonProblemDetailsAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "An unexpected error occurred.",
            status = 500,
            // Only expose details in Development to avoid leaking internals.
            detail = _env.IsDevelopment()
                ? ex.Message
                : "An internal server error occurred. Please try again later.",
            traceId = context.TraceIdentifier
        };

        string json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
