param(
    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [Parameter(Mandatory = $true)]
    [string]$Owner,

    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [Parameter(Mandatory = $true)]
    [string]$SourceFilePath,

    [Parameter(Mandatory = $false)]
    [string]$DestinationPath = '.github/workflows/Base-client-deployment.yml',

    [Parameter(Mandatory = $false)]
    [string]$WorkingDir
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SourceFilePath)) {
    throw "Source workflow file not found: $SourceFilePath"
}
$resolvedSourceFilePath = (Resolve-Path $SourceFilePath).Path

function Invoke-Git {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$GitArguments
    )

    & git -c credential.helper= -c credential.interactive=false @GitArguments
}

function Initialize-GitNonInteractiveMode {
    $env:GIT_TERMINAL_PROMPT = '0'
    $env:GCM_INTERACTIVE = 'never'
    $env:GCM_PROMPT = 'never'
}

function Get-AuthenticatedRepoUrl {
    return "https://x-access-token:${GitHubToken}@github.com/${Owner}/${Repo}.git"
}

Write-Host ""
Write-Host "Copying workflow to ${Owner}/${Repo}..."
Write-Host "  Source      : $SourceFilePath"
Write-Host "  Destination : $DestinationPath"
Write-Host "  Branch      : $Branch"

Initialize-GitNonInteractiveMode

$usingExistingClone = $false
if ([string]::IsNullOrWhiteSpace($WorkingDir)) {
    if ($env:RUNNER_TEMP) {
        $WorkingDir = Join-Path $env:RUNNER_TEMP "workflow-copy-$Repo"
    }
    else {
        $WorkingDir = Join-Path ([System.IO.Path]::GetTempPath()) "workflow-copy-$Repo"
    }
}
elseif (Test-Path (Join-Path $WorkingDir '.git')) {
    $usingExistingClone = $true
    Write-Host "  WorkingDir  : $WorkingDir (reusing merge clone)"
}

if (-not $usingExistingClone) {
    Write-Host "  WorkingDir  : $WorkingDir (fresh clone)"
    if (Test-Path $WorkingDir) {
        Remove-Item -Recurse -Force $WorkingDir
    }

    $cloneUrl = Get-AuthenticatedRepoUrl
    Invoke-Git clone --branch $Branch --single-branch $cloneUrl $WorkingDir
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to clone ${Owner}/${Repo} on branch ${Branch}."
    }
}

Set-Location $WorkingDir

Invoke-Git config user.name "github-actions[bot]"
Invoke-Git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

if ($usingExistingClone) {
    Invoke-Git fetch origin $Branch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch origin/${Branch}."
    }

    Invoke-Git checkout $Branch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to checkout branch ${Branch}."
    }

    Invoke-Git pull origin $Branch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pull latest changes from origin/${Branch}."
    }
}

$destinationFullPath = Join-Path $WorkingDir ($DestinationPath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
$destinationDirectory = Split-Path $destinationFullPath -Parent

if (-not (Test-Path $destinationDirectory)) {
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
}

Copy-Item -Path $resolvedSourceFilePath -Destination $destinationFullPath -Force

Invoke-Git add $DestinationPath
if ($LASTEXITCODE -ne 0) {
    throw "Failed to stage ${DestinationPath}."
}

Invoke-Git diff --cached --quiet
$hasChanges = $LASTEXITCODE -ne 0

if (-not $hasChanges) {
    Write-Host "Workflow file is unchanged; skipping commit."
}
else {
    Invoke-Git commit -m "Add client deployment workflow from base template"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to commit workflow file."
    }

    if ($usingExistingClone) {
        Invoke-Git remote set-url origin (Get-AuthenticatedRepoUrl)
    }

    Invoke-Git push origin $Branch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push workflow file to origin/${Branch}."
    }
}

Write-Host ""
Write-Host "Client deployment workflow copied successfully."
