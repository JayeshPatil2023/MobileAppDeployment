using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MobileAppDeployment.Infrastructure.HealthChecks;

/// <summary>
/// Verifies that the deployment upload directory under <c>wwwroot/uploads/</c> is writable.
/// </summary>
public class FileSystemHealthCheck : IHealthCheck
{
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Creates the file system health check.
    /// </summary>
    public FileSystemHealthCheck(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string uploadDirectory = Path.Combine(_environment.WebRootPath, "uploads");

        try
        {
            Directory.CreateDirectory(uploadDirectory);

            string tempFilePath = Path.Combine(uploadDirectory, $".health-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempFilePath, "health-check");
            File.Delete(tempFilePath);

            return Task.FromResult(HealthCheckResult.Healthy("Upload directory is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Upload directory is not writable.",
                ex));
        }
    }
}
