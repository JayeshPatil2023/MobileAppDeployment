using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MobileAppDeployment.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI tooling (<c>dotnet ef migrations</c>).
/// </summary>
/// <remarks>
/// Loads configuration in the same order as the web host, including
/// <c>dotnet user-secrets</c>, so <c>dotnet ef database update</c> uses the
/// same connection string as local development.
/// </remarks>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <inheritdoc />
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        // User-secrets require UserSecretsId in the .csproj. If missing, skip silently.
        string? userSecretsId = GetUserSecretsId(basePath);
        if (!string.IsNullOrWhiteSpace(userSecretsId))
        {
            configBuilder.AddUserSecrets(userSecretsId);
        }

        IConfigurationRoot configuration = configBuilder.Build();

        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing or empty. " +
                "Set it with: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" " +
                "\"Host=localhost;Port=5432;Database=SystenicsAppDeployment;Username=postgres;Password=YOUR_PASSWORD;\"");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string? GetUserSecretsId(string projectDirectory)
    {
        string csprojPath = Path.Combine(projectDirectory, "MobileAppDeployment.csproj");
        if (!File.Exists(csprojPath))
        {
            return null;
        }

        string content = File.ReadAllText(csprojPath);
        const string startTag = "<UserSecretsId>";
        int start = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += startTag.Length;
        int end = content.IndexOf("</UserSecretsId>", start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? null : content[start..end].Trim();
    }
}
