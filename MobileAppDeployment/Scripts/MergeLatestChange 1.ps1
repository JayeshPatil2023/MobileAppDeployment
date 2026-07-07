# Legacy script — use Merge-LatestClientChanges.ps1 -ClientRepoName "<repo>" from the app instead.
# client
$clientName         = "YOUR_CLIENT_REPO_NAME" # placeholder; replaced dynamically via -ClientRepoName in Merge-LatestClientChanges.ps1
$clientBranch       = "master_dev"

# $folderName         = "BidMaster_01"
# $sourceName         = "SA_MobileBaseAPI" 
# $sourceBranch       = "master"

$sourceName          = "SA_AWDemoMobile"
$sourceBranch       = "master_client"


$clientRepoUrl      = "https://github.com/JayeshPatil2023/$clientName"
$workingDir         = "C:\Application\$clientName"

# mobile source
# $sourceRepoUrl      = "https://github.com/systenics/SA_AWDemoMobile" 
# $sourceBranch       = "master_client"

# MVC API source
$sourceRepoUrl      = "https://github.com/systenics/$sourceName"
$newTag             = "merged-from-source-$(Get-Date -Format 'yyyyMMddHHmmss')"


# Directory where you want to clone the destination repo



#  $newTag = "last-cherry-pick-$clientName"

# Remove existing directory if it exists
if (Test-Path $workingDir) {
    Write-Host "Deleting existing directory..."
    Set-Location "C:\Temp"
    Remove-Item -Recurse -Force $workingDir
}

# Clone the destination repo
Write-Host "Cloning destination repo..."
git clone $clientRepoUrl $workingDir

# Move into the working directory
Set-Location $workingDir

# Add the source repo as a remote
Write-Host "Adding source repo as remote..." 
# Add source repo as remote if not already
if (-not (git remote | Select-String 'source')) {
    git remote add source $sourceRepoUrl
}

# Fetch latest from source master and destination branches
Write-Host "Fetching source master and destination branches..."
git fetch source $sourceBranch
git fetch origin   # Fetches all branches and tags from your destination 



# Check if local or remote branch exists
$branchExists = git show-ref --verify --quiet "refs/heads/$clientBranch"
$remoteBranchExists = git ls-remote --heads origin $clientBranch
if (-not $branchExists -and -not $remoteBranchExists) {
    Write-Host "Creating new branch $clientBranch directly from source/$sourceBranch..."
    git checkout -b $clientBranch source/$sourceBranch
    git push -u origin $clientBranch

    Write-Host "✅ New branch $clientBranch created and pushed."
}
elseif (-not $branchExists -and $remoteBranchExists) {
    Write-Host "Local branch $clientBranch doesn't exist, but remote does. Creating local branch tracking remote..."
    git checkout -b $clientBranch origin/$clientBranch
} 
else {
                Write-Host "Branch $clientBranch already exists locally."
            Write-Host "Checking out existing client branch $clientBranch..."
            git checkout $clientBranch

}


# ✅ Always merge latest source changes into destination branch
Write-Host "Merging latest changes from source/$sourceBranch into $clientBranch..."
git fetch source
# git merge source/$sourceBranch
            # Merge source/master into destination branch
            Write-Host "Merging source/$sourceBranch into $clientBranch..."

            # it stages the changes but does not automatically create a merge commit.
            # git merge source/$sourceBranch --no-edit

            # always create a merge commit, even if the merge could be performed via a fast-forward (i.e., simply moving the branch pointer ahead without a merge commit)
            git merge source/$sourceBranch --no-commit --no-ff


            if ($LASTEXITCODE -ne 0) {
                Write-Host "`n⚠️ Merge conflicts detected.`n"

                # List conflicted files
                $conflictedFiles = git diff --name-only --diff-filter=U
                Write-Host "Conflicted files:`n$conflictedFiles`n"

                # Stage only non-conflicted files
                $cleanFiles = git diff --name-only --diff-filter=U --relative | ForEach-Object { "!" + $_ }
                git add .

                foreach ($file in $conflictedFiles) {
                    git reset HEAD -- $file
                    #  Write-Host "Leaving conflicted file: $file"
                    # Optional: can explicitly checkout conflict stage versions if needed
                    # git checkout --conflict=merge -- $file
                }

                # Commit staged clean files if any
                if (git diff --cached --quiet) {
                    Write-Host "✅ No conflict-free files to commit."
                } else {
                    git commit -m "Merged source/master into $clientBranch — conflict-free files only"
                    git push origin $clientBranch
                    Write-Host "✅ Committed and pushed conflict-free files."
                }

                # Tag the merge point
                $latestCommit = git log -1 --pretty=format:"%H"
                git tag -f $mergeTagName $latestCommit
                git push origin $mergeTagName

                Write-Host "`n🚨 Unmerged conflict files remain. Review and resolve them manually:"
                $conflictedFiles | ForEach-Object { Write-Host " - $_" }
            }
            else {
                # No conflicts — complete merge
                git commit -m "Merged source/master into $clientBranch"
                git push origin $clientBranch

                # Tag the merge point
                $latestCommit = git log -1 --pretty=format:"%H"
                git tag -f $mergeTagName $latestCommit
                git push origin $mergeTagName

                Write-Host "✅ Merge completed cleanly and pushed."
            }

# }

# Remove source remote
git remote remove source
Write-Host "`nCleanup done."

# Write-Host "`n🔧 To restore conflict markers for conflicted files before resolving, run:"
foreach ($file in $conflictedFiles) {
    git checkout --conflict=merge -- $file
    Write-Host "Restored conflict markers in: $file"
}
 
 