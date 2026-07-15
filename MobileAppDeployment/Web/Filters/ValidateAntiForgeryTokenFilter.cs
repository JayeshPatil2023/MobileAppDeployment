using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MobileAppDeployment.Web.Filters;

/// <summary>
/// Enforces antiforgery validation for MVC form posts where the filter is applied.
/// </summary>
/// <remarks>
/// Prefer the built-in <see cref="ValidateAntiForgeryTokenAttribute"/> on individual actions.
/// This filter exists so antiforgery can be applied selectively (for example via filter conventions)
/// without affecting API controllers that do not use form tokens.
/// </remarks>
public sealed class ValidateAntiForgeryTokenFilter : IAsyncActionFilter
{
    private readonly IAntiforgery _antiforgery;

    /// <summary>
    /// Creates the filter with the ASP.NET Core antiforgery service.
    /// </summary>
    public ValidateAntiForgeryTokenFilter(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await _antiforgery.ValidateRequestAsync(context.HttpContext);
        await next();
    }
}
