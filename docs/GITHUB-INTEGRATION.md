# GitHub Integration — App Deployment & Base Workflow

## Overview

> **Form access:** Create is token-gated. Clients reach the form via
> `/AppDeployment/Form/{token}` issued by `POST /api/form-access-tokens`.
> See [`docs/TOKEN-BASED-FORM-ACCESS.md`](TOKEN-BASED-FORM-ACCESS.md).

**Saving** and **starting** the deployment workflow are separate steps:

1. **Save deployment** (Create or Edit) — requires only **Organization Name** and **App Name**, then saves PostgreSQL data and any uploaded assets under `wwwroot/uploads/{id}/`, linking the form-access token on first save. **Does not** start GitHub Actions or enforce full form completeness.
2. **Start App Deployment Process** (Edit only, after first save) — runs **full** required-field + asset validation on the saved row; only then publishes logo/splash to `wwwroot/uploads/workflow-assets/`, dispatches the base workflow in `systenics/SA_BaseMVCProject`, and shows live progress with retries.

Repo creation, local PowerShell merge, and the in-app merge progress path have been **removed** from the production Create flow.  
Those operations now run inside GitHub Actions (see scripts under `MobileAppDeployment/Scripts/` and the workflow YAML in `SA_BaseMVCProject`).

---

## End-to-end sequence

```
User fills deployment form (Create via token URL)
        │
        ▼
User clicks Save deployment
        │
        ▼
Validate Organization Name + App Name only (partial drafts allowed)
        │
        ▼
Save AppDeployment to DB + store any uploaded assets under /uploads/{id}/
        │
        ▼
Link form-access token → deployment (first save only)
        │
        ▼
Redirect → same token URL (now Edit) + success banner
        │
        ▼
User reviews / edits fields, clicks Save deployment as needed (no workflow)
        │
        ▼
User clicks Start App Deployment Process
        │
        ▼
POST /AppDeployment/StartDeployment/{id}
  • load saved deployment from DB (not unsaved form fields)
  • full required-field + asset validation
      - incomplete → re-show Edit with all errors (workflow blocked)
      - complete → continue
  • IWorkflowOrchestrationService.StartWorkflowJobFromDeploymentAsync
      - copy logo/splash → wwwroot/uploads/workflow-assets/{client}/
      - build public URLs (PublicBaseUrl / ngrok)
      - queue job in IWorkflowJobStore
      - background: workflow_dispatch with retries
        │
        ▼
Redirect → WorkflowProgress?jobId=...
        │
        ▼
Browser polls GET /AppDeployment/WorkflowStatus?jobId=... every 1.5s
        │
        ▼
Success / Failure banner in UI
```

Meanwhile, `SA_BaseMVCProject` runs `create_client_repo.ps1`, asset update, build.environment update, and client deployment workflow trigger.

---

## Field mapping (saved deployment → workflow inputs)

| Deployment field | Workflow input |
|------------------|----------------|
| App Name (`AppName`) | `client_name` |
| Mobile App Icon path (`MobileAppIconPath`) | `logo_blob_url` (via public URL) |
| Launch Image path (`LaunchImagePath`) | `splash_blob_url` (via public URL) |
| Bundle ID iOS (`IosBundleId`) | `app_bundle_id` |
| OneSignal App ID (`OneSignalAppId`) | `app_id` |
| OneSignal Sender ID (`OneSignalSenderId`) | `project_id` |

Other workflow inputs (`client_branch`, `source_name`, `source_branch`) come from `GitHub:WorkflowDispatch` config.

---

## Architecture (app services)

| Component | Role |
|-----------|------|
| `AppDeploymentController` | Save-only Create/Edit; explicit `StartDeployment` action |
| `IWorkflowOrchestrationService` / `WorkflowOrchestrationService` | `StartWorkflowJobFromDeploymentAsync` — publish assets, start job, retry dispatch |
| `IGitHubWorkflowDispatchService` / `GitHubWorkflowDispatchService` | HTTP `POST .../actions/workflows/{file}/dispatches` |
| `IWorkflowAssetStorageService` | `PublishStoredFileAndGetPublicUrlAsync` copies saved assets into `workflow-assets/` and builds absolute URLs |
| `IWorkflowJobStore` / `WorkflowJobStore` | In-memory job state for progress UI |
| `WorkflowProgress` + `WorkflowStatus` | Live UI + JSON poll endpoint |

### Retry behavior

Configured under `GitHub:WorkflowDispatch`:

| Setting | Default | Meaning |
|---------|---------|---------|
| `MaxRetries` | `3` | Total dispatch attempts |
| `RetryDelaySeconds` | `5` | Wait between failed attempts |

Only the **GitHub API dispatch** is retried. Asset publish runs once on the request thread when the user clicks **Start App Deployment Process**.

---

## Configuration

```json
"GitHub": {
  "PersonalAccessToken": "",
  "WorkflowDispatch": {
    "Enabled": true,
    "Owner": "systenics",
    "Repository": "SA_BaseMVCProject",
    "Workflow": "main.yml",
    "Ref": "master",
    "ClientBranch": "master_dev",
    "SourceName": "SA_AWDemoMobile",
    "SourceBranch": "master_client",
    "PublicBaseUrl": "https://your-public-host-or-ngrok",
    "MaxRetries": 3,
    "RetryDelaySeconds": 5
  }
}
```

Configure the PAT via User Secrets or `GITHUB_TOKEN` (never commit real tokens):

```powershell
dotnet user-secrets set "GitHub:PersonalAccessToken" "ghp_your_token_here"
```

Token needs scopes to dispatch Actions on `SA_BaseMVCProject` (and whatever the base workflow needs for client repos).

### PublicBaseUrl / ngrok

GitHub-hosted runners cannot reach `localhost`. Set `PublicBaseUrl` in `appsettings.json` (or User Secrets) to a public URL (production host or ngrok). See the **Local development with ngrok** section below.

Do **not** set `"PublicBaseUrl": ""` in `appsettings.Development.json` — that empty value overrides the base config and the app used to fall back to `https://localhost:...`, which Actions cannot download.

---

## Prerequisites

1. Valid GitHub PAT available to the web app process
2. Publicly reachable `PublicBaseUrl` when testing workflow asset downloads
3. Base workflow file in `SA_BaseMVCProject` with matching `workflow_dispatch` inputs
4. Deployment saved with logo, splash, bundle ID, and OneSignal IDs before clicking **Start App Deployment Process**

---

## create_client_repo.ps1 (base workflow script)

Used **inside** GitHub Actions (not by the ASP.NET Create action anymore).

Empty-repo bootstrap notes (default branch / unrelated histories) remain in the section below historically titled under “Repository merge” updates — still apply when the Actions job runs `create_client_repo.ps1`.

### `create_client_repo.ps1` update notes (empty-repo bootstrap + workflow discovery)

The base workflow relies on a client-side workflow file (`Base-client-deployment.yml`) triggered via:

```powershell
gh workflow run Base-client-deployment.yml --repo systenics/<client> --ref <client_branch>
```

GitHub only discovers workflows on the repository **default branch**. Brand-new client repos initially have no commits / default branch. The script bootstraps `.github/workflows/Base-client-deployment.yml`, sets the default branch, then merges source with `--allow-unrelated-histories` when needed, and uses `$LASTEXITCODE` for local-branch checks.

---

## Local development with ngrok

GitHub-hosted runners cannot reach:

- `localhost` / `127.0.0.1`
- Private LAN IPs

### Run app + tunnel

```powershell
dotnet run --launch-profile http
ngrok http http://localhost:5190
```

For IIS Express HTTPS:

```powershell
ngrok http https://localhost:44391 --host-header=rewrite
```

Set:

```json
"PublicBaseUrl": "https://abc123.ngrok-free.dev"
```

`Update-GitHubAssets.ps1` sends `ngrok-skip-browser-warning` for free-tier downloads.

---

## Base workflow steps (SA_BaseMVCProject)

Typical order after dispatch:

1. `create_client_repo.ps1` — create/merge client repo + ensure client deployment workflow on default branch  
2. `Update-GitHubAssets.ps1` — logo/splash  
3. `Update-BuildEnvironment.ps1` — `sys.config/build.environment.json`  
4. `gh workflow run Base-client-deployment.yml` — client deployment  

See earlier sections in this file / workflow YAML for exact step snippets.

---

## Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| Workflow dispatch disabled / token missing | Set `GitHub:PersonalAccessToken` or `GITHUB_TOKEN` |
| Dispatch HTTP 404 | Wrong `Owner`/`Repository`/`Workflow`/`Ref` |
| Assets not downloaded by Actions | `PublicBaseUrl` not public (localhost) |
| Start button error: asset not found | Save deployment again so logo/splash exist under `/uploads/{id}/` |
| UI stays queued | App recycled — in-memory `WorkflowJobStore` lost |
| `gh workflow run` not found on default branch | Put `Base-client-deployment.yml` on client default branch (handled by `create_client_repo.ps1`) |

Check logs from `WorkflowOrchestrationService`, `GitHubWorkflowDispatchService`, and `AppDeploymentController`.
