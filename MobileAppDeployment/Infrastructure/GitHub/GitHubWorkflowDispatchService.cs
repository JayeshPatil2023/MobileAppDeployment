using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MobileAppDeployment.Infrastructure.GitHub;

/// <summary>
/// Calls the GitHub REST API to trigger a workflow_dispatch run.
/// </summary>
public class GitHubWorkflowDispatchService : IGitHubWorkflowDispatchService
{
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _gitHubOptions;
    private readonly GitHubWorkflowDispatchOptions _workflowOptions;
    private readonly ILogger<GitHubWorkflowDispatchService> _logger;

    public GitHubWorkflowDispatchService(
        HttpClient httpClient,
        IOptions<GitHubOptions> gitHubOptions,
        IOptions<GitHubWorkflowDispatchOptions> workflowOptions,
        ILogger<GitHubWorkflowDispatchService> logger)
    {
        _httpClient = httpClient;
        _gitHubOptions = gitHubOptions.Value;
        _workflowOptions = workflowOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GitHubWorkflowDispatchResult> TriggerAsync(
        string? clientName,
        string logoBlobUrl,
        string splashBlobUrl,
        string appBundleId,
        string appId,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        if (!_workflowOptions.Enabled)
        {
            return GitHubWorkflowDispatchResult.Failed("Workflow dispatch is disabled in configuration.");
        }

        string token = ResolveAccessToken();
        if (string.IsNullOrWhiteSpace(token) || token == "YOUR_GITHUB_TOKEN")
        {
            return GitHubWorkflowDispatchResult.Failed("GitHub token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_workflowOptions.Owner) ||
            string.IsNullOrWhiteSpace(_workflowOptions.Repository) ||
            string.IsNullOrWhiteSpace(_workflowOptions.Workflow) ||
            string.IsNullOrWhiteSpace(_workflowOptions.Ref))
        {
            return GitHubWorkflowDispatchResult.Failed("Workflow dispatch settings are incomplete.");
        }

        string endpoint =
            $"https://api.github.com/repos/{_workflowOptions.Owner}/{_workflowOptions.Repository}/actions/workflows/{_workflowOptions.Workflow}/dispatches";

        HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("MobileAppDeployment/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        string effectiveClientName = string.IsNullOrWhiteSpace(clientName)
            ? _workflowOptions.ClientName
            : clientName.Trim();

        if (string.IsNullOrWhiteSpace(effectiveClientName))
        {
            return GitHubWorkflowDispatchResult.Failed("Client name is required to trigger the workflow.");
        }

        if (string.IsNullOrWhiteSpace(logoBlobUrl) || string.IsNullOrWhiteSpace(splashBlobUrl))
        {
            return GitHubWorkflowDispatchResult.Failed("Logo and splash image URLs are required to trigger the workflow.");
        }

        if (string.IsNullOrWhiteSpace(appBundleId) ||
            string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(projectId))
        {
            return GitHubWorkflowDispatchResult.Failed("App bundle ID, App ID, and Project ID are required to trigger the workflow.");
        }

        object payload = new
        {
            @ref = _workflowOptions.Ref,
            inputs = new
            {
                client_name = effectiveClientName,
                client_branch = _workflowOptions.ClientBranch,
                source_name = _workflowOptions.SourceName,
                source_branch = _workflowOptions.SourceBranch,
                logo_blob_url = logoBlobUrl,
                splash_blob_url = splashBlobUrl,
                app_bundle_id = appBundleId.Trim(),
                app_id = appId.Trim(),
                project_id = projectId.Trim()
            }
        };

        string json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Workflow dispatch triggered for {Owner}/{Repo} ({Workflow}) on ref {Ref} with client {ClientName}",
                    _workflowOptions.Owner,
                    _workflowOptions.Repository,
                    _workflowOptions.Workflow,
                    _workflowOptions.Ref,
                    effectiveClientName);

                return GitHubWorkflowDispatchResult.Succeeded("GitHub Actions workflow triggered successfully.");
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Workflow dispatch failed with status {StatusCode}. Response: {Body}",
                (int)response.StatusCode,
                body);

            return GitHubWorkflowDispatchResult.Failed(
                $"Failed to trigger workflow. GitHub API returned {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while triggering GitHub workflow.");
            return GitHubWorkflowDispatchResult.Failed("Unexpected error occurred while triggering workflow.");
        }
    }

    /// <summary>
    /// Resolves token from environment variable first, then app configuration.
    /// </summary>
    private string ResolveAccessToken()
    {
        string? envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            return envToken.Trim();
        }

        return _gitHubOptions.PersonalAccessToken.Trim();
    }
}
