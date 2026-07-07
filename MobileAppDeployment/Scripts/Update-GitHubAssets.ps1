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
    [string]$LogoBlobUrl,

    [Parameter(Mandatory = $true)]
    [string]$SplashBlobUrl
)

$ErrorActionPreference = 'Stop'

$Headers = @{
    Authorization = "Bearer $GitHubToken"
    Accept        = "application/vnd.github+json"
}

# ngrok free tier may block automated downloads without this header.
$DownloadHeaders = @{
    "ngrok-skip-browser-warning" = "true"
}

function Update-GitHubFile {
    param(
        [string]$BlobUrl,
        [string]$RepoPath,
        [string]$CommitMessage
    )

    Write-Host ""
    Write-Host "Processing $RepoPath..."

    $TempFile = Join-Path $env:TEMP ([System.IO.Path]::GetFileName($RepoPath))

    Invoke-WebRequest `
        -Uri $BlobUrl `
        -OutFile $TempFile `
        -Headers $DownloadHeaders

    $Base64 = [Convert]::ToBase64String(
        [System.IO.File]::ReadAllBytes($TempFile)
    )

    # Use ${RepoPath} and ${Branch} so PowerShell does not parse ?ref as part of the variable name.
    $FileInfoUrl = "https://api.github.com/repos/$Owner/$Repo/contents/${RepoPath}?ref=${Branch}"

    Write-Host "GET URL:"
    Write-Host $FileInfoUrl

    $Body = @{
        message = $CommitMessage
        content = $Base64
        branch  = $Branch
    }

    try {
        $FileInfo = Invoke-RestMethod `
            -Method GET `
            -Uri $FileInfoUrl `
            -Headers $Headers

        $Body.sha = $FileInfo.sha
    }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response -or [int]$response.StatusCode -ne 404) {
            throw
        }

        Write-Host "File not found on branch ${Branch}; creating ${RepoPath}."
    }

    $UpdateUrl = "https://api.github.com/repos/$Owner/$Repo/contents/${RepoPath}"

    Write-Host "PUT URL:"
    Write-Host $UpdateUrl

    Invoke-RestMethod `
        -Method PUT `
        -Uri $UpdateUrl `
        -Headers $Headers `
        -Body ($Body | ConvertTo-Json)

    Write-Host "$RepoPath updated successfully."
}

Update-GitHubFile `
    -BlobUrl $LogoBlobUrl `
    -RepoPath "assets/icon-only.png" `
    -CommitMessage "Updated logo"

Update-GitHubFile `
    -BlobUrl $SplashBlobUrl `
    -RepoPath "assets/splash.png" `
    -CommitMessage "Updated splash screen"

Write-Host ""
Write-Host "Done."
