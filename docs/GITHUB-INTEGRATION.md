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

**Important:** GitHub creation runs only on **Create**, not **Edit**. A deployment save still succeeds if GitHub creation fails.

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
| HTTP 401 / 403 | Token missing `repo` scope or expired |
| HTTP 422 | Repository name already exists under JayeshPatil2023 |
| Script not found | Rebuild project so `Scripts/` is copied to output |

Check application logs for entries from `GitHubRepositoryService` and `AppDeploymentController`.
