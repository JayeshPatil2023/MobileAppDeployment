using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MobileAppDeployment.Infrastructure.Persistence;

namespace MobileAppDeployment.Tests;

/// <summary>
/// Configures the application host for integration tests with an in-memory database.
/// </summary>
public class MobileAppWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-api-key-for-integration-tests";
    private readonly string _databaseName = $"IntegrationTests-{Guid.NewGuid()}";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=Test;Username=test;Password=test;",
                ["FormAccess:ApiKey"] = TestApiKey,
                ["HealthChecks:SkipDatabase"] = "true",
                ["Identity:AdminEmail"] = "",
                ["Identity:AdminPassword"] = "",
                ["GitHub:Enabled"] = "false",
                ["Storage:StorageType"] = "Local"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContextRegistrations(services);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        using IServiceScope scope = host.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        return host;
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        List<ServiceDescriptor> descriptors = services
            .Where(d =>
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(DbContextOptions))
            .ToList();

        foreach (ServiceDescriptor descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
