#requires -Version 5.1
<#
.SYNOPSIS
    Merges the latest source mobile repository into a newly created client repository.

.DESCRIPTION
    Clones the client repository under JayeshPatil2023, adds the systenics source repo as a remote,
    and merges the configured source branch into the client branch. Emits MERGE_PROGRESS JSON lines
    for live UI updates and a final MERGE_RESULT JSON line.

.PARAMETER ClientRepoName
    Sanitized client repository name (matches the app name used when creating the GitHub repo).

.PARAMETER ClientBranch
    Branch to create or update on the client repository.

.PARAMETER SourceOwner
    GitHub owner of the source repository.

.PARAMETER SourceRepository
    Source repository name.

.PARAMETER SourceBranch
    Source branch to merge from.

.PARAMETER WorkingDirectoryRoot
    Root folder where the client repo is cloned.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientRepoName,

    [Parameter(Mandatory = $false)]
    [string]$ClientBranch = "master_dev",

    [Parameter(Mandatory = $false)]
    [string]$SourceOwner = "systenics",

    [Parameter(Mandatory = $false)]
    [string]$SourceRepository = "SA_AWDemoMobile",

    [Parameter(Mandatory = $false)]
    [string]$SourceBranch = "master_client",

    [Parameter(Mandatory = $false)]
    [string]$WorkingDirectoryRoot = "C:\Application"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Script:GitHubClientOwner = "JayeshPatil2023"
$Script:ProgressPrefix = "MERGE_PROGRESS:"
$Script:ResultPrefix = "MERGE_RESULT:"

<#
.SYNOPSIS
    Builds a clone/push URL, embedding GITHUB_TOKEN for non-interactive git auth.
#>
function Get-GitHubRepositoryGitUrl {
    param(
        [string]$Owner,
        [string]$RepositoryName
    )

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        return "https://x-access-token:$($env:GITHUB_TOKEN)@github.com/$Owner/$RepositoryName.git"
    }

    return "https://github.com/$Owner/$RepositoryName.git"
}

<#
.SYNOPSIS
    Disables interactive Git Credential Manager prompts for automated execution.
#>
function Initialize-GitNonInteractiveMode {
    $env:GIT_TERMINAL_PROMPT = '0'
    $env:GCM_INTERACTIVE = 'never'
    $env:GCM_PROMPT = 'never'
}

<#
.SYNOPSIS
    Runs git with credential helper disabled so PAT-in-URL auth works non-interactively.
#>
function Invoke-Git {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$GitArguments
    )

    & git -c credential.helper= -c credential.interactive=false @GitArguments
}

<#
.SYNOPSIS
    Configures the local repository to authenticate via the remote URL only (no credential manager UI).
#>
function Set-GitLocalAuthConfig {
    Invoke-Git config --local credential.helper ''
    Invoke-Git config --local credential.interactive false
}

<#
.SYNOPSIS
    Updates a remote URL to include the PAT so fetch/push never invoke Git Credential Manager.
#>
function Set-GitRemoteAuthenticatedUrl {
    param(
        [string]$RemoteName,
        [string]$Owner,
        [string]$RepositoryName
    )

    $authenticatedUrl = Get-GitHubRepositoryGitUrl -Owner $Owner -RepositoryName $RepositoryName
    Invoke-Git remote set-url $RemoteName $authenticatedUrl
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to configure authenticated URL for remote '$RemoteName'."
    }
}

<#
.SYNOPSIS
    Writes a machine-readable progress event for the ASP.NET Core progress UI.
#>
function Write-MergeProgress {
    param(
        [int]$Percent,
        [string]$Message,
        [string]$Stage
    )

    $payload = [ordered]@{
        percent = [Math]::Max(0, [Math]::Min(100, $Percent))
        message = $Message
        stage   = $Stage
    } | ConvertTo-Json -Compress

    Write-Output "$Script:ProgressPrefix$payload"
}

<#
.SYNOPSIS
    Writes the final merge result as JSON on the last line of stdout.
#>
function Write-MergeResult {
    param(
        [bool]$Success,
        [string]$Message,
        [string]$ClientRepoName,
        [string]$ClientRepoUrl = "",
        [string]$ErrorDetail = ""
    )

    $payload = [ordered]@{
        success          = $Success
        message          = $Message
        client_repo_name = $ClientRepoName
        client_repo_url  = $ClientRepoUrl
        error            = $ErrorDetail
    } | ConvertTo-Json -Compress

    Write-Output "$Script:ResultPrefix$payload"
}

<#
.SYNOPSIS
    Fetches a single branch using an explicit refspec (avoids case-collision warnings on Windows).
#>
function Invoke-GitFetchBranch {
    param(
        [string]$Remote,
        [string]$Branch
    )

    Invoke-Git fetch --no-tags $Remote "${Branch}:refs/remotes/${Remote}/${Branch}"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch ${Remote}/${Branch}"
    }
}

<#
.SYNOPSIS
    Returns $true when the index has staged changes ready to commit.
#>
function Test-GitHasStagedChanges {
    Invoke-Git diff --cached --quiet
    return $LASTEXITCODE -ne 0
}

<#
.SYNOPSIS
    Returns $true when a merge is in progress (MERGE_HEAD exists).
#>
function Test-GitMergeInProgress {
    Invoke-Git rev-parse -q --verify MERGE_HEAD 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

try {
    Initialize-GitNonInteractiveMode

    if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        throw "GITHUB_TOKEN is not set. Configure GitHub:PersonalAccessToken or the GITHUB_TOKEN environment variable so git operations run without interactive prompts."
    }

    $clientRepoUrl = "https://github.com/$Script:GitHubClientOwner/$ClientRepoName"
    $clientCloneUrl = Get-GitHubRepositoryGitUrl -Owner $Script:GitHubClientOwner -RepositoryName $ClientRepoName
    $sourceRepoUrl = Get-GitHubRepositoryGitUrl -Owner $SourceOwner -RepositoryName $SourceRepository
    $workingDir = Join-Path $WorkingDirectoryRoot $ClientRepoName
    $mergeTagName = "merged-from-source-$(Get-Date -Format 'yyyyMMddHHmmss')"
    $conflictedFiles = @()

    Write-MergeProgress -Percent 5 -Message "Preparing workspace for $ClientRepoName" -Stage "prepare"

    if (Test-Path $workingDir) {
        Write-MergeProgress -Percent 10 -Message "Removing existing working directory" -Stage "cleanup"
        Set-Location $WorkingDirectoryRoot
        Remove-Item -Recurse -Force $workingDir
    }

    if (-not (Test-Path $WorkingDirectoryRoot)) {
        New-Item -ItemType Directory -Path $WorkingDirectoryRoot -Force | Out-Null
    }

    Write-MergeProgress -Percent 20 -Message "Cloning client repository $ClientRepoName" -Stage "clone-client"
    Invoke-Git clone $clientCloneUrl $workingDir
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone client repository $clientRepoUrl" }

    Set-Location $workingDir

    Invoke-Git config core.ignoreCase true
    Set-GitLocalAuthConfig
    Set-GitRemoteAuthenticatedUrl -RemoteName 'origin' -Owner $Script:GitHubClientOwner -RepositoryName $ClientRepoName

    Write-MergeProgress -Percent 35 -Message "Adding source repository remote" -Stage "add-remote"
    if (git remote | Select-String -SimpleMatch 'source') {
        Set-GitRemoteAuthenticatedUrl -RemoteName 'source' -Owner $SourceOwner -RepositoryName $SourceRepository
    }
    else {
        Invoke-Git remote add source $sourceRepoUrl
        if ($LASTEXITCODE -ne 0) { throw "Failed to add source remote for $SourceOwner/$SourceRepository" }
    }

    Write-MergeProgress -Percent 45 -Message "Fetching source and client branches" -Stage "fetch"
    Invoke-GitFetchBranch -Remote 'source' -Branch $SourceBranch

    $remoteClientBranch = Invoke-Git ls-remote --heads origin $ClientBranch
    if ($remoteClientBranch) {
        Invoke-GitFetchBranch -Remote 'origin' -Branch $ClientBranch
    }

    Write-MergeProgress -Percent 55 -Message "Preparing client branch $ClientBranch" -Stage "branch"
    Invoke-Git show-ref --verify --quiet "refs/heads/$ClientBranch" | Out-Null
    $branchExists = $LASTEXITCODE -eq 0
    $remoteBranchExists = Invoke-Git ls-remote --heads origin $ClientBranch

    if (-not $branchExists -and -not $remoteBranchExists) {
        Invoke-Git checkout -b $ClientBranch "source/$SourceBranch"
        if ($LASTEXITCODE -ne 0) { throw "Failed to create branch $ClientBranch from source/$SourceBranch" }
        Invoke-Git push -u origin $ClientBranch
        if ($LASTEXITCODE -ne 0) { throw "Failed to push new branch $ClientBranch" }
    }
    elseif (-not $branchExists -and $remoteBranchExists) {
        Invoke-Git checkout -b $ClientBranch "origin/$ClientBranch"
        if ($LASTEXITCODE -ne 0) { throw "Failed to checkout remote branch $ClientBranch" }
    }
    else {
        Invoke-Git checkout $ClientBranch
        if ($LASTEXITCODE -ne 0) { throw "Failed to checkout existing branch $ClientBranch" }
    }

    Write-MergeProgress -Percent 70 -Message "Merging source/$SourceBranch into $ClientBranch" -Stage "merge"
    Invoke-GitFetchBranch -Remote 'source' -Branch $SourceBranch
    Invoke-Git merge "source/$SourceBranch" --no-commit --no-ff
    $mergeExitCode = $LASTEXITCODE

    if ($mergeExitCode -ne 0) {
        Write-MergeProgress -Percent 80 -Message "Merge conflicts detected - committing conflict-free files" -Stage "conflicts"
        $conflictedFiles = @(Invoke-Git diff --name-only --diff-filter=U)
        Invoke-Git add .

        foreach ($file in $conflictedFiles) {
            Invoke-Git reset HEAD -- $file
        }

        Invoke-Git diff --cached --quiet
        $hasStagedChanges = $LASTEXITCODE -ne 0
        if ($hasStagedChanges) {
            Invoke-Git commit -m "Merged source/$SourceBranch into $ClientBranch - conflict-free files only"
            Invoke-Git push origin $ClientBranch
        }

        $latestCommit = Invoke-Git log -1 --pretty=format:"%H"
        Invoke-Git tag -f $mergeTagName $latestCommit
        Invoke-Git push origin $mergeTagName
        if ($LASTEXITCODE -ne 0) { throw "Failed to push merge tag $mergeTagName" }

        $conflictList = ($conflictedFiles -join '; ')
        throw "Merge completed with conflicts in: $conflictList"
    }

    $mergeInProgress = Test-GitMergeInProgress
    $hasStagedMergeChanges = Test-GitHasStagedChanges

    if ($mergeInProgress -and $hasStagedMergeChanges) {
        Write-MergeProgress -Percent 85 -Message "Completing merge commit" -Stage "commit"
        Invoke-Git commit -m "Merged source/$SourceBranch into $ClientBranch"
        if ($LASTEXITCODE -ne 0) { throw "Failed to create merge commit" }

        Write-MergeProgress -Percent 92 -Message "Pushing merged changes to GitHub" -Stage "push"
        Invoke-Git push origin $ClientBranch
        if ($LASTEXITCODE -ne 0) { throw "Failed to push merged branch $ClientBranch" }
    }
    elseif (-not $mergeInProgress -and -not $hasStagedMergeChanges) {
        Write-MergeProgress -Percent 85 -Message "Client branch already includes the latest source changes" -Stage "up-to-date"
    }
    else {
        throw "Merge finished in an unexpected state (in progress: $mergeInProgress, staged: $hasStagedMergeChanges)."
    }

    $latestCommit = Invoke-Git log -1 --pretty=format:"%H"
    Invoke-Git tag -f $mergeTagName $latestCommit
    Invoke-Git push origin $mergeTagName
    if ($LASTEXITCODE -ne 0) { throw "Failed to push merge tag $mergeTagName" }

    Write-MergeProgress -Percent 98 -Message "Cleaning up temporary git remote" -Stage "cleanup-remote"
    if (git remote | Select-String -SimpleMatch 'source') {
        Invoke-Git remote remove source
    }

    Write-MergeProgress -Percent 100 -Message "Merge completed successfully" -Stage "done"
    Write-MergeResult -Success $true -Message "Source code merged into $ClientRepoName successfully." -ClientRepoName $ClientRepoName -ClientRepoUrl $clientRepoUrl
    exit 0
}
catch {
    $errorMessage = $_.Exception.Message
    Write-MergeResult -Success $false -Message "Repository merge failed." -ClientRepoName $ClientRepoName -ErrorDetail $errorMessage
    exit 1
}
