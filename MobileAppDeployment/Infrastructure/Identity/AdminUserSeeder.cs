using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MobileAppDeployment.Infrastructure.Identity;

/// <summary>
/// Seeds the single admin user required for the internal deployment portal.
/// </summary>
public static class AdminUserSeeder
{
    /// <summary>
    /// Ensures the configured admin account exists with the Administrator role.
    /// </summary>
    /// <param name="services">Root application service provider.</param>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        IServiceProvider provider = scope.ServiceProvider;

        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        RoleManager<IdentityRole> roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        IConfiguration configuration = provider.GetRequiredService<IConfiguration>();
        ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        ILogger logger = loggerFactory.CreateLogger("AdminUserSeeder");

        string? adminEmail = configuration["Identity:AdminEmail"];
        string? adminPassword = configuration["Identity:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Identity:AdminEmail or Identity:AdminPassword is not configured; admin user was not seeded.");
            return;
        }

        const string adminRole = "Administrator";
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
            if (!roleResult.Succeeded)
            {
                logger.LogError(
                    "Failed to create admin role: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                return;
            }
        }

        IdentityUser? existingUser = await userManager.FindByEmailAsync(adminEmail);
        if (existingUser is not null)
        {
            if (!await userManager.IsInRoleAsync(existingUser, adminRole))
            {
                await userManager.AddToRoleAsync(existingUser, adminRole);
            }

            return;
        }

        var user = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        IdentityResult createResult = await userManager.CreateAsync(user, adminPassword);
        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Failed to seed admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, adminRole);
        logger.LogInformation("Seeded admin user {Email}.", adminEmail);
    }
}
