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

### Workflow dispatch with logo/splash uploads

The Index page **Trigger Build Workflow** form accepts:

- Client name
- Logo PNG
- Splash PNG
- App bundle ID (`appBundleId` in `build.environment.json`)
- OneSignal App ID (`OneSignal.AppId`)
- OneSignal Project ID (`OneSignal.ProjectId`)

The app stores files under `wwwroot/uploads/workflow-assets/{clientName}/` and sends absolute URLs to GitHub as `logo_blob_url` and `splash_blob_url`.

Set a publicly reachable base URL (required for GitHub Actions runners to download images):

```json
"GitHub": {
  "WorkflowDispatch": {
    "PublicBaseUrl": "https://your-deployed-app.example.com"
  }
}
```

If `PublicBaseUrl` is empty, the app uses the current request host (works only when GitHub can reach that URL — not `localhost`).

### Local development with ngrok

GitHub-hosted runners run in the cloud. They **cannot** reach:

- `localhost` / `127.0.0.1`
- Private LAN IPs (`192.168.x.x`, `10.x.x.x`)

To test workflow asset uploads from your local machine, expose the app with **ngrok** and set `PublicBaseUrl` to the ngrok URL.

#### 1. Run the app locally

**Option A — Kestrel (recommended for ngrok)**

```powershell
cd c:\Applications\systenics\MobileAppDeployment\MobileAppDeployment
dotnet run --launch-profile http
```

App listens at `http://localhost:5190`.

**Option B — IIS Express**

Uses `https://localhost:44391` (see `Properties/launchSettings.json`).

#### 2. Start ngrok

For Kestrel (HTTP):

```powershell
ngrok http http://localhost:5190
```

For IIS Express (HTTPS), add host-header rewrite (required — without it you get **400 Bad Request - Invalid Hostname**):

```powershell
ngrok http https://localhost:44391 --host-header=rewrite
```

Or explicitly:

```powershell
ngrok http https://localhost:44391 --host-header=localhost:44391
```

Copy the forwarding URL, e.g. `https://abc123.ngrok-free.dev`.

#### 3. Configure PublicBaseUrl

In `appsettings.json` or User Secrets:

```json
"GitHub": {
  "WorkflowDispatch": {
    "PublicBaseUrl": "https://abc123.ngrok-free.dev"
  }
}
```

Restart the app after changing this value.

#### 4. Verify before triggering the workflow

Open in a browser (ideally from mobile data, not your office Wi‑Fi):

```
https://abc123.ngrok-free.dev
```

After uploading assets via **Trigger Build Workflow**, test a direct file URL:

```
https://abc123.ngrok-free.dev/uploads/workflow-assets/BidMaster/logo.png
```

If the image loads, GitHub Actions can likely download it too.

#### 5. ngrok free tier and GitHub Actions

ngrok free tier may show a browser warning page to automated clients. `Update-GitHubAssets.ps1` sends the `ngrok-skip-browser-warning` header when downloading images. Keep the script in your workflow repo up to date.

#### ngrok troubleshooting

| Symptom | Fix |
|---------|-----|
| `400 Bad Request - Invalid Hostname` | Use `--host-header=rewrite` with IIS Express, or switch to `dotnet run --launch-profile http` |
| Workflow runs but asset download fails | Confirm `PublicBaseUrl` matches current ngrok URL (URL changes each free session unless you use a reserved domain) |
| `localhost` URLs sent to GitHub | Set `PublicBaseUrl`; do not rely on request host when testing locally |

For production, deploy the app to a stable public URL and set `PublicBaseUrl` to that domain instead of ngrok.

Add these inputs to your workflow YAML:

```yaml
on:
  workflow_dispatch:
    inputs:
      logo_blob_url:
        description: "Public URL for logo PNG"
        required: true
        type: string
      splash_blob_url:
        description: "Public URL for splash PNG"
        required: true
        type: string
      app_bundle_id:
        description: "Android/iOS app bundle ID"
        required: true
        type: string
      app_id:
        description: "OneSignal App ID"
        required: true
        type: string
      project_id:
        description: "OneSignal Project ID"
        required: true
        type: string
```

After `create_client_repo.ps1`, run `Update-GitHubAssets.ps1`, then `Update-BuildEnvironment.ps1`:

```yaml
      - name: Update GitHub Assets
        if: success()
        shell: pwsh
        run: |
          ./Update-GitHubAssets.ps1 `
            -GitHubToken "${{ secrets.GH_PAT }}" `
            -Owner "systenics" `
            -Repo "${{ inputs.client_name }}" `
            -Branch "${{ inputs.client_branch }}" `
            -LogoBlobUrl "${{ inputs.logo_blob_url }}" `
            -SplashBlobUrl "${{ inputs.splash_blob_url }}"

      - name: Update Build Environment
        if: success()
        shell: pwsh
        run: |
          ./Update-BuildEnvironment.ps1 `
            -GitHubToken "${{ secrets.GH_PAT }}" `
            -Owner "systenics" `
            -Repo "${{ inputs.client_name }}" `
            -Branch "${{ inputs.client_branch }}" `
            -AppBundleId "${{ inputs.app_bundle_id }}" `
            -AppId "${{ inputs.app_id }}" `
            -ProjectId "${{ inputs.project_id }}"
```

`Update-BuildEnvironment.ps1` updates `sys.config/build.environment.json` in the **client repo** (both `release` and `debug` sections):

- `appBundleId`
- `OneSignal.AppId`
- `OneSignal.ProjectId`

Copy `Scripts/Update-BuildEnvironment.ps1` into your workflow repository (`SA_BaseMVCProject`) alongside the other scripts.

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
| `400 Bad Request - Invalid Hostname` via ngrok | Run ngrok with `--host-header=rewrite` for IIS Express |
| GitHub workflow cannot download logo/splash | Set `PublicBaseUrl` to a public URL (ngrok or deployed server), not localhost |
| `Update-GitHubAssets` 404 with `assets/=branch` in URL | PowerShell parsed `$RepoPath?ref` incorrectly; use updated script with `${RepoPath}?ref=${Branch}` |

Check application logs for entries from `GitHubRepositoryService` and `AppDeploymentController`.
