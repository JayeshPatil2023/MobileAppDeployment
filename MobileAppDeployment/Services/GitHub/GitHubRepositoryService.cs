using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Invokes the PowerShell GitHub automation script after a deployment is saved.
/// </summary>
public class GitHubRepositoryService : IGitHubRepositoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly GitHubOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<GitHubRepositoryService> _logger;

    public GitHubRepositoryService(IOptions<GitHubOptions> options, IWebHostEnvironment environment, ILogger<GitHubRepositoryService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GitHubRepositoryResult> CreateClientRepositoryAsync(AppDeployment deployment, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("GitHub repository creation is disabled in configuration.");
            return GitHubRepositoryResult.Skipped("GitHub integration is disabled.");
        }

        string token = ResolveAccessToken();
        if (string.IsNullOrWhiteSpace(token) || token == "YOUR_GITHUB_TOKEN")
        {
            _logger.LogWarning("GitHub Personal Access Token is not configured.");
            return GitHubRepositoryResult.Skipped("GitHub token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(deployment.AppName))
        {
            return GitHubRepositoryResult.Failed("App name is required to create a GitHub repository.");
        }

        string scriptPath = Path.Combine(_environment.ContentRootPath, "Scripts", "New-GitHubClientRepository.ps1");
        if (!File.Exists(scriptPath))
        {
            _logger.LogError("GitHub script not found at {ScriptPath}", scriptPath);
            return GitHubRepositoryResult.Failed("GitHub automation script was not found on the server.");
        }

        string description = BuildRepositoryDescription(deployment);

        ProcessStartInfo startInfo = CreatePowerShellStartInfo(
            scriptPath,
            deployment.AppName,
            description,
            token);

        _logger.LogInformation(
            "Creating GitHub repository for app {AppName} under {Owner}",
            deployment.AppName,
            _options.Owner);

        using Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return GitHubRepositoryResult.Failed("Failed to start PowerShell process.");
            }

            string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogWarning("GitHub script stderr: {StdErr}", stderr.Trim());
            }

            GitHubRepositoryResult result = ParseScriptOutput(stdout, process.ExitCode);
            if (result.Success)
            {
                _logger.LogInformation("GitHub repository created: {HtmlUrl}", result.HtmlUrl);
            }
            else
            {
                _logger.LogWarning("GitHub repository creation failed: {Error}", result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating GitHub repository for {AppName}", deployment.AppName);
            return GitHubRepositoryResult.Failed("An unexpected error occurred while creating the GitHub repository.");
        }
    }

    /// <summary>
    /// Resolves token from environment variable first, then application configuration.
    /// </summary>
    private string ResolveAccessToken()
    {
        string? envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            return envToken.Trim();
        }

        return _options.PersonalAccessToken.Trim();
    }

    /// <summary>
    /// Builds a meaningful repository description from deployment metadata.
    /// </summary>
    private static string BuildRepositoryDescription(AppDeployment deployment)
    {
        string organization = string.IsNullOrWhiteSpace(deployment.OrganizationName)
            ? "client"
            : deployment.OrganizationName.Trim();

        return $"{deployment.AppName} mobile app deployment for {organization}. Managed by Systenics App Deployment.";
    }

    /// <summary>
    /// Creates process start info for Windows PowerShell or PowerShell 7.
    /// Uses <see cref="ProcessStartInfo.ArgumentList"/> so values with spaces are passed correctly.
    /// </summary>
    private static ProcessStartInfo CreatePowerShellStartInfo(string scriptPath, string appName, string description, string token)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = ResolvePowerShellExecutable(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-AppName");
        startInfo.ArgumentList.Add(appName);
        startInfo.ArgumentList.Add("-Description");
        startInfo.ArgumentList.Add(description);
        startInfo.Environment["GITHUB_TOKEN"] = token;

        return startInfo;
    }

    /// <summary>
    /// Prefers PowerShell 7 when available, otherwise Windows PowerShell 5.1.
    /// </summary>
    private static string ResolvePowerShellExecutable()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pwshPath = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
        if (File.Exists(pwshPath))
        {
            return pwshPath;
        }

        return "powershell.exe";
    }

    /// <summary>
    /// Parses the JSON payload emitted as the last line of script stdout.
    /// </summary>
    private GitHubRepositoryResult ParseScriptOutput(string stdout, int exitCode)
    {
        string? jsonLine = stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(line => line.TrimStart().StartsWith('{'));

        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return GitHubRepositoryResult.Failed(
                exitCode == 0
                    ? "GitHub script completed without a JSON result."
                    : "GitHub script failed without a JSON error payload.");
        }

        try
        {
            ScriptResult? payload = JsonSerializer.Deserialize<ScriptResult>(jsonLine, JsonOptions);
            if (payload is null)
            {
                return GitHubRepositoryResult.Failed("GitHub script returned an invalid JSON payload.");
            }

            if (payload.Success)
            {
                if (string.IsNullOrWhiteSpace(payload.HtmlUrl))
                {
                    _logger.LogWarning(
                        "GitHub script reported success but no html_url was returned. Raw JSON: {Json}",
                        jsonLine);
                    return GitHubRepositoryResult.Failed(
                        "GitHub repository may have been created, but the response did not include a repository URL.");
                }

                return GitHubRepositoryResult.Succeeded(
                    payload.Repository ?? payload.FullName ?? string.Empty,
                    payload.HtmlUrl,
                    payload.CloneUrl);
            }

            return GitHubRepositoryResult.Failed(payload.Error ?? "GitHub repository creation failed.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse GitHub script output: {Output}", jsonLine);
            return GitHubRepositoryResult.Failed("Failed to parse GitHub script response.");
        }
    }

    /// <summary>
    /// JSON contract shared with New-GitHubClientRepository.ps1 stdout.
    /// </summary>
    private sealed class ScriptResult
    {
        public bool Success { get; set; }

        public string? Error { get; set; }

        public string? Repository { get; set; }

        public string? FullName { get; set; }

        public string? HtmlUrl { get; set; }

        public string? CloneUrl { get; set; }
    }
}
