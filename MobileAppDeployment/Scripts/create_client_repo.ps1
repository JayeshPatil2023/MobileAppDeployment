param(
    [Parameter(Mandatory=$true)]
    [string]$ClientName,

    [Parameter(Mandatory=$true)]
    [string]$ClientBranch,

    [Parameter(Mandatory=$true)]
    [string]$SourceName,

    [Parameter(Mandatory=$true)]
    [string]$SourceBranch,

    [string]$ClientRepoUrl,
    [string]$SourceRepoUrl,
    [string]$WorkingDir,
    [string]$MergeTagName = "merged-from-source-$(Get-Date -Format 'yyyyMMddHHmmss')",
    [string]$BaseWorkflowTemplatePath = "./Base-client-deployment.yml",
    [string]$ClientWorkflowTargetPath = ".github/workflows/Base-client-deployment.yml"
)

$ErrorActionPreference = 'Stop'
$OrganizationName = 'systenics'
$TeamSlug = 'auctionworx_team'
$CreateRepository = 1
$script:BootstrappedDefaultBranch = $false

if (-not (Test-Path $BaseWorkflowTemplatePath)) {
    throw "Base workflow template not found: $BaseWorkflowTemplatePath"
}

$ResolvedBaseWorkflowTemplatePath = (Resolve-Path $BaseWorkflowTemplatePath).Path

function Create-GitHubRepository {
    $repo = "$OrganizationName/$ClientName"
    Write-Output "::group::Create GitHub Repository"
     Write-Output "OrganizationName: $OrganizationName"
    Write-Output "RepositoryName: $ClientName"
 
    Write-Host "Checking if repository '$repo' exists..."
    gh repo view $repo *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Repository already exists. Skipping creation."
        Write-Output "::endgroup::"
        return
    }  
     
    gh repo create $repo `
        --private `
        --confirm

    if ($LASTEXITCODE -ne 0) {
        throw "Repository creation failed."
    }

    Write-Output "Assigning team '$TeamSlug'..."
    gh api `
        -X PUT `
        "/orgs/$OrganizationName/teams/$TeamSlug/repos/$OrganizationName/$ClientName" `
        -f permission=push

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to assign team '$TeamSlug'."
    }

    

    Write-Output "Repository created successfully."

    Write-Output "::endgroup::"
}

function Ensure-ClientWorkflowOnDefaultBranch {
    param(
        [string]$RepoFullName
    )

    Write-Output "::group::Ensure Client Deployment Workflow on Default Branch"

    $defaultBranchRaw = gh repo view $RepoFullName --json defaultBranchRef --jq ".defaultBranchRef.name"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to resolve default branch for $RepoFullName."
    }

    $defaultBranch = "$defaultBranchRaw".Trim()
    if ([string]::IsNullOrWhiteSpace($defaultBranch) -or $defaultBranch -eq "null") {
        # Brand new repos can be empty and have no default branch yet.
        # Bootstrap with ClientBranch so workflow discovery and dispatch can work.
        $defaultBranch = $ClientBranch
        $script:BootstrappedDefaultBranch = $true
        Write-Output "No default branch found (empty repository). Bootstrapping with branch: $defaultBranch"
    }

    Write-Output "DefaultBranch           : $defaultBranch"
    Write-Output "WorkflowTemplate        : $ResolvedBaseWorkflowTemplatePath"
    Write-Output "WorkflowTargetPath      : $ClientWorkflowTargetPath"

    $previousBranchRaw = git rev-parse --abbrev-ref HEAD 2>$null
    $previousBranch = "$previousBranchRaw".Trim()

    git fetch origin $defaultBranch 2>$null
    $remoteDefaultBranchExists = $LASTEXITCODE -eq 0

    git show-ref --verify --quiet "refs/heads/$defaultBranch"
    if ($LASTEXITCODE -eq 0) {
        git checkout $defaultBranch
    }
    elseif ($remoteDefaultBranchExists) {
        git checkout -b $defaultBranch "origin/$defaultBranch"
    }
    else {
        # Empty repository path: create the first local branch.
        git checkout -b $defaultBranch
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to checkout default branch $defaultBranch."
    }

    $targetFullPath = Join-Path (Get-Location) ($ClientWorkflowTargetPath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $targetDirectory = Split-Path $targetFullPath -Parent
    if (-not (Test-Path $targetDirectory)) {
        New-Item -Path $targetDirectory -ItemType Directory -Force | Out-Null
    }

    Copy-Item -Path $ResolvedBaseWorkflowTemplatePath -Destination $targetFullPath -Force

    git add $ClientWorkflowTargetPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to stage $ClientWorkflowTargetPath."
    }

    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Output "Workflow file is already up to date on default branch."
    }
    else {
        git commit -m "Add/update Base-client-deployment workflow on default branch"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to commit workflow file on default branch."
        }

        git push -u origin $defaultBranch
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push workflow file to origin/$defaultBranch."
        }

        Write-Output "Workflow file committed to $defaultBranch."

        # Ensure GitHub knows which branch is default (important for workflow discovery).
        gh repo edit $RepoFullName --default-branch $defaultBranch
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to set default branch to $defaultBranch on $RepoFullName."
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($previousBranch) -and $previousBranch -ne "HEAD" -and $previousBranch -ne $defaultBranch) {
        git checkout $previousBranch
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to switch back to $previousBranch."
        }
    }

    Write-Output "::endgroup::"
}




# Build authenticated repo URLs if GH_PAT is available
if (-not $ClientRepoUrl) {
    if ($env:GH_PAT) {
        $ClientRepoUrl = "https://$($env:GH_PAT)@github.com/$OrganizationName/$ClientName.git"
    } else {
        $ClientRepoUrl = "https://github.com/$OrganizationName/$ClientName.git"
    }
}

if (-not $SourceRepoUrl) {
    if ($env:GH_PAT) {
        $SourceRepoUrl = "https://$($env:GH_PAT)@github.com/$OrganizationName/$SourceName.git"
    } else {
        $SourceRepoUrl = "https://github.com/$OrganizationName/$SourceName.git"
    }
}

# Use GitHub Actions runner temp directory if available
if (-not $WorkingDir) {
    if ($env:RUNNER_TEMP) {
        $WorkingDir = Join-Path $env:RUNNER_TEMP $ClientName
    } else {
        $WorkingDir = "C:\Temp\$ClientName"
    }
}

Write-Output "::group::Configuration"
Write-Output "ClientName     : $ClientName"
Write-Output "ClientBranch   : $ClientBranch"
Write-Output "SourceName     : $SourceName"
Write-Output "SourceBranch   : $SourceBranch"
Write-Output "WorkingDir     : $WorkingDir"
Write-Output "MergeTagName   : $MergeTagName"
Write-Output "WorkflowTemplatePath : $ResolvedBaseWorkflowTemplatePath"
Write-Output "ClientWorkflowTarget : $ClientWorkflowTargetPath"
Write-Output "::endgroup::"

try {
    # Remove existing directory if it exists
    if (Test-Path $WorkingDir) {
        Write-Output "::group::Cleanup Existing Directory"
        Write-Output "Deleting existing directory: $WorkingDir"
        Remove-Item -Recurse -Force $WorkingDir
        Write-Output "::endgroup::"
    }

    if ($CreateRepository) {
        Create-GitHubRepository 
    }

    # Clone the destination repo
    Write-Output "::group::Clone Destination Repo"
    git clone $ClientRepoUrl $WorkingDir
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone destination repository." }
    Write-Output "::endgroup::"

    # Move into the working directory
    Set-Location $WorkingDir

    Ensure-ClientWorkflowOnDefaultBranch -RepoFullName "$OrganizationName/$ClientName"

    # Add the source repo as a remote
    Write-Output "::group::Configure Source Remote"
    if (-not (git remote | Select-String '^source$')) {
        git remote add source $SourceRepoUrl
    }
    Write-Output "::endgroup::"

    # Fetch latest branches
    Write-Output "::group::Fetch Latest Changes"
    git fetch source $SourceBranch
    git fetch origin
    Write-Output "::endgroup::"

    # Check if local or remote branch exists
    git show-ref --verify --quiet "refs/heads/$ClientBranch"
    $branchExists = $LASTEXITCODE -eq 0
    $remoteBranchExists = git ls-remote --heads origin $ClientBranch

    if (-not $branchExists -and -not $remoteBranchExists) {
        Write-Output "Creating new branch $ClientBranch from source/$SourceBranch..."
        git checkout -b $ClientBranch source/$SourceBranch
        git push -u origin $ClientBranch
        Write-Output "New branch $ClientBranch created and pushed."
    }
    elseif (-not $branchExists -and $remoteBranchExists) {
        Write-Output "Creating local tracking branch for origin/$ClientBranch..."
        git checkout -b $ClientBranch origin/$ClientBranch
    }
    else {
        Write-Output "Checking out existing branch $ClientBranch..."
        git checkout $ClientBranch
    }

    # Merge latest source changes
    Write-Output "::group::Merge Source Changes"
    git fetch source
    if ($script:BootstrappedDefaultBranch) {
        # Newly created repos have unrelated root history (workflow bootstrap commit).
        git merge source/$SourceBranch --no-commit --no-ff --allow-unrelated-histories
    }
    else {
        git merge source/$SourceBranch --no-commit --no-ff
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Output "Merge conflicts detected."

        # List conflicted files
        $conflictedFiles = git diff --name-only --diff-filter=U
        Write-Output "Conflicted files:"
        $conflictedFiles | ForEach-Object { Write-Output " - $_" }

        # Stage all files, then unstage conflicted ones
        git add .
        foreach ($file in $conflictedFiles) {
            git reset HEAD -- $file
        }

        # Commit only conflict-free files if any
        git diff --cached --quiet
        if ($LASTEXITCODE -eq 0) {
            Write-Output "No conflict-free files to commit."
        } else {
            git commit -m "Merged source/$SourceBranch into $ClientBranch - conflict-free files only"
            git push origin $ClientBranch
            Write-Output "Committed and pushed conflict-free files."
        }

        # Tag the merge point
        $latestCommit = git log -1 --pretty=format:"%H"
        git tag -f $MergeTagName $latestCommit
        git push origin $MergeTagName

        Write-Output "Unmerged conflict files remain. Resolve them manually:"
        $conflictedFiles | ForEach-Object { Write-Output " - $_" }

        # Restore conflict markers
        foreach ($file in $conflictedFiles) {
            git checkout --conflict=merge -- $file
            Write-Output "Restored conflict markers in: $file"
        }
    }
    else {
        # No conflicts - complete merge
        git commit -m "Merged source/$SourceBranch into $ClientBranch"
        git push origin $ClientBranch

        # Tag the merge point
        $latestCommit = git log -1 --pretty=format:"%H"
        git tag -f $MergeTagName $latestCommit
        git push origin $MergeTagName

        Write-Output "Merge completed cleanly and pushed."
    }

    Write-Output "::endgroup::"
}
finally {
    # Cleanup source remote
    try {
        git remote remove source 2>$null
    } catch {
        # ignore cleanup errors
    }

    Write-Output "Cleanup completed."
}
