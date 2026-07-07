using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Runs the merge PowerShell script in the background with configurable retries
/// and streams progress updates to <see cref="IRepoMergeJobStore"/>.
/// </summary>
public class RepoMergeService : IRepoMergeService
{
    private const string ProgressPrefix = "MERGE_PROGRESS:";
    private const string ResultPrefix = "MERGE_RESULT:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly RepoMergeOptions _options;
    private readonly GitHubOptions _gitHubOptions;
    private readonly IRepoMergeJobStore _jobStore;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RepoMergeService> _logger;

    public RepoMergeService(
        IOptions<RepoMergeOptions> options,
        IOptions<GitHubOptions> gitHubOptions,
        IRepoMergeJobStore jobStore,
        IWebHostEnvironment environment,
        ILogger<RepoMergeService> logger)
    {
        _options = options.Value;
        _gitHubOptions = gitHubOptions.Value;
        _jobStore = jobStore;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public string StartMergeJob(string clientRepoName, string appName, string? repositoryUrl)
    {
        RepoMergeJobState job = _jobStore.Create(
            clientRepoName,
            appName,
            repositoryUrl,
            Math.Max(1, _options.MaxRetries));

        // Fire-and-forget background execution; progress is polled via the job store.
        _ = Task.Run(() => ExecuteMergeWithRetriesAsync(job.JobId, clientRepoName));

        return job.JobId;
    }

    /// <inheritdoc />
    public RepoMergeJobState? GetJob(string jobId) => _jobStore.Get(jobId);

    /// <summary>
    /// Executes the merge script up to <see cref="RepoMergeOptions.MaxRetries"/> times,
    /// waiting <see cref="RepoMergeOptions.RetryDelaySeconds"/> between failed attempts.
    /// </summary>
    private async Task ExecuteMergeWithRetriesAsync(string jobId, string clientRepoName)
    {
        int maxAttempts = Math.Max(1, _options.MaxRetries);
        string? lastError = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            RepoMergeJobStatus runningStatus = attempt == 1
                ? RepoMergeJobStatus.Running
                : RepoMergeJobStatus.Retrying;

            string startMessage = attempt == 1
                ? "Starting repository merge..."
                : $"Retrying merge (attempt {attempt} of {maxAttempts})...";

            _jobStore.UpdateProgress(jobId, attempt == 1 ? 1 : 0, startMessage, runningStatus, attempt);

            MergeScriptResult result = await RunMergeScriptOnceAsync(jobId, clientRepoName, attempt);

            if (result.Success)
            {
                _jobStore.MarkCompleted(jobId, result.Message ?? "Source code merged into the client repository successfully.");
                _logger.LogInformation(
                    "Repository merge completed for {ClientRepo} on attempt {Attempt}",
                    clientRepoName,
                    attempt);
                return;
            }

            lastError = result.Error ?? "Repository merge failed without a detailed error.";
            _logger.LogWarning(
                "Repository merge attempt {Attempt}/{MaxAttempts} failed for {ClientRepo}: {Error}",
                attempt,
                maxAttempts,
                clientRepoName,
                lastError);

            if (attempt < maxAttempts)
            {
                int delaySeconds = Math.Max(0, _options.RetryDelaySeconds);
                _jobStore.UpdateProgress(
                    jobId,
                    0,
                    $"Merge failed — waiting {delaySeconds}s before retry {attempt + 1} of {maxAttempts}.",
                    RepoMergeJobStatus.Retrying,
                    attempt);

                if (delaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }

        _jobStore.MarkFailed(jobId, lastError ?? "Repository merge failed after all retry attempts.");
    }

    /// <summary>
    /// Invokes the configured PowerShell merge script once and parses live progress output.
    /// </summary>
    private async Task<MergeScriptResult> RunMergeScriptOnceAsync(string jobId, string clientRepoName, int attempt)
    {
        if (!_options.Enabled)
        {
            return MergeScriptResult.Failed("Repository merge is disabled in configuration.");
        }

        string scriptPath = Path.Combine(_environment.ContentRootPath, "Scripts", _options.ScriptFileName);
        if (!File.Exists(scriptPath))
        {
            _logger.LogError("Merge script not found at {ScriptPath}", scriptPath);
            return MergeScriptResult.Failed("Repository merge script was not found on the server.");
        }

        ProcessStartInfo startInfo = CreatePowerShellStartInfo(scriptPath, clientRepoName);
        using Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return MergeScriptResult.Failed("Failed to start PowerShell process for repository merge.");
            }

            Task<string> stdoutTask = ReadStdoutWithProgressAsync(process.StandardOutput, jobId, attempt);
            string stderr = await process.StandardError.ReadToEndAsync();
            string stdout = await stdoutTask;

            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogWarning("Merge script stderr (attempt {Attempt}): {StdErr}", attempt, stderr.Trim());
            }

            MergeScriptResult? parsedResult = ParseMergeResult(stdout);
            if (parsedResult is not null)
            {
                return parsedResult;
            }

            return process.ExitCode == 0
                ? MergeScriptResult.Succeeded("Source code merged into the client repository successfully.")
                : MergeScriptResult.Failed(
                    string.IsNullOrWhiteSpace(stderr)
                        ? "Repository merge script exited with an error."
                        : stderr.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during merge script execution for {ClientRepo}", clientRepoName);
            return MergeScriptResult.Failed($"Unexpected error during repository merge: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads script stdout line-by-line and updates job progress when MERGE_PROGRESS lines are emitted.
    /// </summary>
    private async Task<string> ReadStdoutWithProgressAsync(TextReader reader, string jobId, int attempt)
    {
        List<string> lines = [];
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            lines.Add(line);

            if (line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
            {
                ApplyProgressLine(jobId, line[ProgressPrefix.Length..], attempt);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Deserializes a MERGE_PROGRESS JSON payload and updates the in-memory job store.
    /// </summary>
    private void ApplyProgressLine(string jobId, string json, int attempt)
    {
        try
        {
            MergeProgressPayload? payload = JsonSerializer.Deserialize<MergeProgressPayload>(json, JsonOptions);
            if (payload is null)
            {
                return;
            }

            _jobStore.UpdateProgress(
                jobId,
                payload.Percent,
                payload.Message ?? "Merging...",
                RepoMergeJobStatus.Running,
                attempt);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Ignored unparsable merge progress line: {Json}", json);
        }
    }

    /// <summary>
    /// Parses the MERGE_RESULT JSON line from script stdout, if present.
    /// </summary>
    private static MergeScriptResult? ParseMergeResult(string stdout)
    {
        string? resultLine = stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(line => line.StartsWith(ResultPrefix, StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(resultLine))
        {
            return null;
        }

        try
        {
            MergeResultPayload? payload = JsonSerializer.Deserialize<MergeResultPayload>(
                resultLine[ResultPrefix.Length..],
                JsonOptions);

            if (payload is null)
            {
                return MergeScriptResult.Failed("Merge script returned an invalid result payload.");
            }

            return payload.Success
                ? MergeScriptResult.Succeeded(payload.Message ?? "Merge completed successfully.")
                : MergeScriptResult.Failed(payload.Error ?? payload.Message ?? "Repository merge failed.");
        }
        catch (JsonException)
        {
            return MergeScriptResult.Failed("Failed to parse merge script result.");
        }
    }

    /// <summary>
    /// Builds PowerShell start info with merge script parameters and optional GitHub token for git push.
    /// </summary>
    private ProcessStartInfo CreatePowerShellStartInfo(string scriptPath, string clientRepoName)
    {
        ProcessStartInfo startInfo = new()
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
        startInfo.ArgumentList.Add("-ClientRepoName");
        startInfo.ArgumentList.Add(clientRepoName);
        startInfo.ArgumentList.Add("-ClientBranch");
        startInfo.ArgumentList.Add(_options.ClientBranch);
        startInfo.ArgumentList.Add("-SourceOwner");
        startInfo.ArgumentList.Add(_options.SourceOwner);
        startInfo.ArgumentList.Add("-SourceRepository");
        startInfo.ArgumentList.Add(_options.SourceRepository);
        startInfo.ArgumentList.Add("-SourceBranch");
        startInfo.ArgumentList.Add(_options.SourceBranch);
        startInfo.ArgumentList.Add("-WorkingDirectoryRoot");
        startInfo.ArgumentList.Add(_options.WorkingDirectoryRoot);

        string? token = ResolveAccessToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            startInfo.Environment["GITHUB_TOKEN"] = token;
        }

        return startInfo;
    }

    /// <summary>
    /// Resolves GitHub token from environment variable or application configuration.
    /// </summary>
    private string? ResolveAccessToken()
    {
        string? envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            return envToken.Trim();
        }

        string configToken = _gitHubOptions.PersonalAccessToken.Trim();
        return string.IsNullOrWhiteSpace(configToken) || configToken == "YOUR_GITHUB_TOKEN"
            ? null
            : configToken;
    }

    /// <summary>
    /// Prefers PowerShell 7 when available, otherwise Windows PowerShell 5.1.
    /// </summary>
    private static string ResolvePowerShellExecutable()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pwshPath = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
        return File.Exists(pwshPath) ? pwshPath : "powershell.exe";
    }

    /// <summary>
    /// Internal result of a single merge script execution attempt.
    /// </summary>
    private sealed class MergeScriptResult
    {
        public bool Success { get; init; }

        public string? Message { get; init; }

        public string? Error { get; init; }

        public static MergeScriptResult Succeeded(string message) => new() { Success = true, Message = message };

        public static MergeScriptResult Failed(string error) => new() { Success = false, Error = error };
    }

    /// <summary>
    /// JSON contract for MERGE_PROGRESS stdout lines from the PowerShell script.
    /// </summary>
    private sealed class MergeProgressPayload
    {
        public int Percent { get; set; }

        public string? Message { get; set; }

        public string? Stage { get; set; }
    }

    /// <summary>
    /// JSON contract for the final MERGE_RESULT stdout line from the PowerShell script.
    /// </summary>
    private sealed class MergeResultPayload
    {
        public bool Success { get; set; }

        public string? Message { get; set; }

        public string? Error { get; set; }

        public string? ClientRepoName { get; set; }

        public string? ClientRepoUrl { get; set; }
    }
}
