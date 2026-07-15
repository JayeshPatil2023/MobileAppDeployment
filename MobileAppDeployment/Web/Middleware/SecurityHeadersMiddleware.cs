namespace MobileAppDeployment.Web.Middleware;

/// <summary>
/// Adds defensive HTTP security headers to every response.
/// </summary>
/// <remarks>
/// <para>
/// These headers mitigate the most common browser-based attacks:
/// clickjacking, MIME sniffing, information disclosure via Referer,
/// and cross-site scripting via inline scripts.
/// </para>
/// <para>
/// The Content-Security-Policy allows the CDNs already used by the app
/// (Google Fonts, Bootstrap, jQuery via lib/) plus 'self' for local assets.
/// Adjust the font/script sources if libraries are pinned to a new CDN.
/// </para>
/// <para>Registration order: add BEFORE <c>UseStaticFiles</c> so that even
/// static file responses carry the headers.</para>
/// </remarks>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware with the next pipeline delegate.</summary>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        // ── Clickjacking protection ────────────────────────────────────────
        // Prevents the app from being embedded in an <iframe> on a different origin.
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // ── MIME-type sniffing protection ──────────────────────────────────
        // Stops browsers from guessing a content-type different from the
        // declared Content-Type header (e.g. treating a JPEG as a script).
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // ── Referrer information control ───────────────────────────────────
        // Sends the full URL only to same-origin requests; sends only the
        // origin to cross-origin HTTPS destinations; nothing over plain HTTP.
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // ── Permissions Policy ─────────────────────────────────────────────
        // Disables browser features the app does not require.
        context.Response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=()";

        // ── Content Security Policy ────────────────────────────────────────
        // Restricts which resources the browser may load.
        //
        // default-src 'self'      → Only same-origin resources by default
        // script-src  'self'      → Scripts from same origin only (lib/ bundled)
        //             'nonce-...' → inline scripts use nonces (not yet implemented;
        //                           remove unsafe-inline once nonces are applied)
        //             'unsafe-inline' → temporary until nonces are added to Razor views
        // style-src   'self'      → Same-origin CSS
        //             fonts.googleapis.com → Google Fonts CSS
        //             'unsafe-inline' → Bootstrap component styles written inline in views
        // font-src    fonts.gstatic.com → Google Fonts font files
        // img-src     'self' data: → Local images + base64 data URIs
        // connect-src 'self'      → AJAX/fetch only to same origin (WorkflowStatus polling)
        // frame-ancestors 'none'  → Equivalent to X-Frame-Options DENY (belt-and-suspenders)
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        await _next(context);
    }
}
