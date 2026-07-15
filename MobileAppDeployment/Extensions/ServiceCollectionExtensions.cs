using MobileAppDeployment.Application.AssetUpload;
using MobileAppDeployment.Infrastructure.Email;
using MobileAppDeployment.Infrastructure.HealthChecks;
using MobileAppDeployment.Infrastructure.Security;
using MobileAppDeployment.Web.Filters;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MobileAppDeployment.Extensions;

/// <summary>
/// Groups all application service registrations into named extension methods.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers every application service group.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo("DataProtection-Keys"));

        services
            .AddPersistenceServices(configuration)
            .AddIdentityServices()
            .AddDomainServices()
            .AddEmailServices()
            .AddGitHubServices()
            .AddStorageServices(configuration)
            .AddConfigurationOptions(configuration)
            .AddSecurityServices()
            .AddHealthCheckServices(configuration);

        return services;
    }

    /// <summary>
    /// Registers the EF Core database context, unit of work, and repository implementations.
    /// </summary>
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.EnableDetailedErrors(false);
            options.EnableSensitiveDataLogging(false);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppDeploymentRepository, AppDeploymentRepository>();
        services.AddScoped<IFormAccessTokenRepository, FormAccessTokenRepository>();

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core Identity for admin authentication.
    /// </summary>
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        return services;
    }

    /// <summary>
    /// Registers the domain service layer (business logic above the repository).
    /// </summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<IAppDeploymentService, AppDeploymentService>();
        services.AddScoped<IFormAccessTokenService, FormAccessTokenService>();
        services.AddScoped<IAssetUploadStrategy, AssetUploadStrategy>();

        return services;
    }

    /// <summary>
    /// Registers security-related services and filters.
    /// </summary>
    public static IServiceCollection AddSecurityServices(this IServiceCollection services)
    {
        services.AddScoped<ISecretProtectionService, DataProtectionSecretService>();
        services.AddScoped<ApiKeyAuthorizationFilter>();
        services.AddScoped<ValidateAntiForgeryTokenFilter>();

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core health checks for PostgreSQL and the upload directory.
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IHealthChecksBuilder builder = services.AddHealthChecks()
            .AddCheck<FileSystemHealthCheck>("filesystem");

        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        bool skipDatabase = configuration.GetValue<bool>("HealthChecks:SkipDatabase");

        if (!skipDatabase && !string.IsNullOrWhiteSpace(connectionString))
        {
            builder.AddNpgSql(connectionString);
        }

        return services;
    }

    /// <summary>
    /// Registers the email sending infrastructure.
    /// </summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        services.AddScoped<IEmailService, MailKitEmailService>();
        services.AddScoped<IFormAccessEmailComposer, FormAccessEmailComposer>();

        return services;
    }

    /// <summary>
    /// Registers the GitHub Actions integration services.
    /// </summary>
    public static IServiceCollection AddGitHubServices(this IServiceCollection services)
    {
        services.AddHttpClient<IGitHubWorkflowDispatchService, GitHubWorkflowDispatchService>();
        services.AddSingleton<IWorkflowJobStore, WorkflowJobStore>();
        services.AddSingleton<Application.BackgroundJobs.WorkflowDispatchChannel>();
        services.AddHostedService<Application.BackgroundJobs.WorkflowDispatchBackgroundService>();
        services.AddScoped<IWorkflowOrchestrationService, WorkflowOrchestrationService>();

        return services;
    }

    /// <summary>
    /// Registers asset storage services based on <see cref="StorageOptions.StorageType"/>.
    /// </summary>
    public static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        string storageType = configuration.GetValue<string>($"{StorageOptions.SectionName}:StorageType") ?? "Local";
        if (string.Equals(storageType, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IAssetStorageService, LocalAssetStorageService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported Storage:StorageType '{storageType}'. Implement IAssetStorageService for cloud backends (Azure/S3).");
        }

        services.AddScoped<IWorkflowAssetStorageService, WorkflowAssetStorageService>();

        return services;
    }

    /// <summary>
    /// Binds all strongly-typed configuration option classes from appsettings.
    /// </summary>
    public static IServiceCollection AddConfigurationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FormAccessOptions>(
            configuration.GetSection(FormAccessOptions.SectionName));

        services.Configure<MailgunSmtpOptions>(
            configuration.GetSection(MailgunSmtpOptions.SectionName));

        services.Configure<GitHubOptions>(
            configuration.GetSection(GitHubOptions.SectionName));

        services.Configure<GitHubWorkflowDispatchOptions>(
            configuration.GetSection($"{GitHubOptions.SectionName}:WorkflowDispatch"));

        return services;
    }
}
