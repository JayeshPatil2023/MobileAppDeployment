/*
 * ─────────────────────────────────────────────────────────────────────────────
 *  MobileAppDeployment — Application Entry Point
 * ─────────────────────────────────────────────────────────────────────────────
 *
 *  Program.cs should read like a high-level table of contents, not an
 *  implementation manual. All service registrations live in:
 *    Extensions/ServiceCollectionExtensions.cs
 *
 *  All middleware pipeline configuration lives in:
 *    Extensions/WebApplicationExtensions.cs
 *
 *  Middleware order (IMPORTANT — do not reorder without understanding why):
 *    1. Global exception handler       ← outermost; catches all downstream exceptions
 *    2. Security headers               ← applied to every response including errors
 *    3. HTTPS redirect + HSTS         ← transport security
 *    4. Static files                   ← served before routing for performance
 *    5. Rate limiting                  ← before routing so limits apply to all endpoints
 *    6. Routing                        ← matches requests to endpoints
 *    7. Authorization                  ← checks auth after routing sets endpoint metadata
 *    8. Endpoint mapping               ← executes matched endpoint
 *
 * ─────────────────────────────────────────────────────────────────────────────
 */

using MobileAppDeployment.Extensions;
using MobileAppDeployment.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// ── Service Registration ───────────────────────────────────────────────────
// All registrations are organized into logical groups in ServiceCollectionExtensions.cs.

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAppRateLimiting();
builder.Services.AddApplicationServices(builder.Configuration);

// ── Application Build ──────────────────────────────────────────────────────

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────────────────────────
//
// 1 & 2: Global exception handling + security headers (must be outermost)
app.UseSecurityMiddleware();

// 3: Transport security
if (!app.Environment.IsDevelopment())
{
    // HSTS tells browsers to only use HTTPS for this domain for 1 year.
    // Only enable in Production — Development uses http://localhost.
    app.UseHsts();
}

app.UseHttpsRedirection();

// 4: Static files (wwwroot) — no auth required; security headers already applied above
app.UseStaticFiles();

// 5: Rate limiting — applied before routing so all matched requests are rate-limited
app.UseRateLimiter();

// 6: Routing — builds endpoint metadata from controller route attributes
app.UseRouting();

// 7: Authentication + authorization
app.UseAuthentication();
app.UseAuthorization();

// 8: Health checks — public endpoint, no auth
app.UseAppHealthChecks();

// ── Route Configuration ────────────────────────────────────────────────────

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AppDeployment}/{action=Index}/{id?}");

await AdminUserSeeder.SeedAsync(app.Services);

app.Run();

/// <summary>
/// Exposes the implicit Program class for integration testing.
/// </summary>
public partial class Program;
