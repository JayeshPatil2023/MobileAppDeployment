# GitHub Repository Automation

After a **Create** form submission succeeds, the app automatically creates a public GitHub repository named after the **Client App Name** (`AppName`).

- **GitHub account:** [JayeshPatil2023](https://github.com/JayeshPatil2023)
- **API endpoint:** `https://api.github.com/user/repos`
- **Script:** `Scripts/New-GitHubClientRepository.ps1`

---

## Prerequisites

1. **PowerShell 5.1+** (Windows) or **PowerShell 7+** (`pwsh`) — the app prefers `pwsh.exe` when installed.
2. **GitHub Personal Access Token** with the `repo` scope.
3. **TLS / network** access from the app server to `api.github.com`.

### Execution policy

The app invokes PowerShell with `-ExecutionPolicy Bypass` for the script process only. You do **not** need to change the machine-wide execution policy.

To test the script manually:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

---

## Configure the token

Replace `YOUR_GITHUB_TOKEN` using **one** of these options (recommended order):

### Option 1 — Environment variable (recommended for production)

```powershell
$env:GITHUB_TOKEN = "ghp_your_token_here"
```

### Option 2 — User Secrets (recommended for local development)

```powershell
cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment
dotnet user-secrets set "GitHub:PersonalAccessToken" "ghp_your_token_here"
```

### Option 3 — appsettings.Development.json

```json
{
  "GitHub": {
    "PersonalAccessToken": "ghp_your_token_here"
  }
}
```

Never commit real tokens to source control.

---

## Test the script standalone

```powershell
cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment\Scripts

$env:GITHUB_TOKEN = "ghp_your_token_here"

.\New-GitHubClientRepository.ps1 -AppName "EliteBids" -Description "EliteBids mobile deployment"
```

Expected success output (last line):

```json
{"success":true,"repository":"EliteBids","html_url":"https://github.com/JayeshPatil2023/EliteBids",...}
```

---

## Application integration

| Step | Location | What happens |
|------|----------|----------------|
| 1 | `AppDeploymentController.Create` (POST) | Form validated, deployment saved, files uploaded |
| 2 | `IGitHubRepositoryService.CreateClientRepositoryAsync` | Called immediately after successful save |
| 3 | `GitHubRepositoryService` | Runs `Scripts/New-GitHubClientRepository.ps1` with `GITHUB_TOKEN` |
| 4 | Redirect to Index | Success/warning message shown via `TempData` |
| 5 | `IRepoMergeService.StartMergeJob` | When repo merge is enabled, merges source into client repo |
| 6 | `MergeProgress` view | Live progress bar polled via `MergeStatus` API |

**Important:** GitHub creation runs only on **Create**, not **Edit**. A deployment save still succeeds if GitHub creation fails.

When repository creation succeeds and `GitHub:RepoMerge:Enabled` is `true`, the app redirects to the **Merge progress** page instead of Index. The page polls `GET /AppDeployment/MergeStatus?jobId=...` every 1.5 seconds.

---

## Repository merge (source → client)

After a client repository is created, the app runs `Scripts/Merge-LatestClientChanges.ps1` with the sanitized repository name as `-ClientRepoName` (replacing the hard-coded `$clientName` in the legacy script).

| Setting | Default | Description |
|---------|---------|-------------|
| `GitHub:RepoMerge:Enabled` | `true` | Skip merge and go to Index when `false` |
| `GitHub:RepoMerge:MaxRetries` | `3` | Total execution attempts (initial + retries) |
| `GitHub:RepoMerge:RetryDelaySeconds` | `5` | Wait between failed attempts |
| `GitHub:RepoMerge:SourceOwner` | `systenics` | Source repo owner |
| `GitHub:RepoMerge:SourceRepository` | `SA_AWDemoMobile` | Source repo name |
| `GitHub:RepoMerge:SourceBranch` | `master_client` | Branch to merge from |
| `GitHub:RepoMerge:ClientBranch` | `master_dev` | Branch created/updated on client repo |
| `GitHub:RepoMerge:WorkingDirectoryRoot` | `C:\Application` | Local clone directory |

### Test merge script standalone

```powershell
cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment\Scripts

$env:GITHUB_TOKEN = "ghp_your_token_here"

.\Merge-LatestClientChanges.ps1 -ClientRepoName "EliteBids"
```

The script emits `MERGE_PROGRESS:{json}` lines during execution and a final `MERGE_RESULT:{json}` line.

### Non-interactive git (no credential popup)

Repository **creation** uses the GitHub REST API with your PAT directly. Repository **merge** runs local `git` commands (clone, fetch, push). Those commands do **not** use the REST API token automatically.

On Windows, Git Credential Manager (GCM) intercepts HTTPS git requests. If the stored `origin` remote URL has no embedded credentials, GCM opens the **"Select an account"** dialog — even when a PAT is configured in the app for API calls.

The merge script prevents this by:

1. Passing `GITHUB_TOKEN` from the app into the PowerShell process
2. Setting `GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=never`, and `GCM_PROMPT=never`
3. Reconfiguring `origin` to `https://x-access-token:<PAT>@github.com/...` after clone
4. Applying the same authenticated URL to the **source** remote (`systenics/SA_AWDemoMobile`) — required when the source repo is private
5. Disabling the local credential helper so git uses the URL token only

Your PAT must have **`repo` read access** to both the client repos under `JayeshPatil2023` and the source repo under `systenics`.

Ensure the token is available to the **web app process** (not only your interactive PowerShell session):

```powershell
dotnet user-secrets set "GitHub:PersonalAccessToken" "ghp_your_token_here"
```

Or set `GITHUB_TOKEN` in the environment used to launch IIS Express / Kestrel.

### Disable merge locally

```json
"GitHub": {
  "RepoMerge": {
    "Enabled": false
  }
}
```

### Disable integration locally

```json
"GitHub": {
  "Enabled": false
}
```

---

## Repository naming

The script sanitizes `AppName` to meet GitHub rules:

| App name input | Repository name |
|----------------|-----------------|
| `EliteBids` | `EliteBids` |
| `My Client App` | `My-Client-App` |
| `demo.app` | `demo.app` |

Invalid names throw a validation error before the API call.

---

## Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| `GitHub token is not configured` | Set `GITHUB_TOKEN` or `GitHub:PersonalAccessToken` |
| Git Credential Manager popup during merge | Token not passed to git child process; set User Secrets / env var and restart the app |
| `GITHUB_TOKEN is not set` from merge script | Same as above — repo creation can work while merge git commands cannot |
| HTTP 401 / 403 | Token missing `repo` scope or expired |
| HTTP 422 | Repository name already exists under JayeshPatil2023 |
| Script not found | Rebuild project so `Scripts/` is copied to output |

Check application logs for entries from `GitHubRepositoryService` and `AppDeploymentController`.
