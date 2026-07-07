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
    [string]$AppBundleId,

    [Parameter(Mandatory = $true)]
    [string]$AppId,

    [Parameter(Mandatory = $true)]
    [string]$ProjectId
)

$ErrorActionPreference = 'Stop'

$RepoPath = 'sys.config/build.environment.json'

$Headers = @{
    Authorization = "Bearer $GitHubToken"
    Accept        = "application/vnd.github+json"
}

function Get-GitHubFileContent {
    param([string]$Path)

    $FileInfoUrl = "https://api.github.com/repos/$Owner/$Repo/contents/${Path}?ref=${Branch}"
    Write-Host "GET URL:"
    Write-Host $FileInfoUrl

    return Invoke-RestMethod -Method GET -Uri $FileInfoUrl -Headers $Headers
}

function Set-BuildEnvironmentSection {
    param(
        [psobject]$Section,
        [string]$BundleId,
        [string]$OneSignalAppId,
        [string]$OneSignalProjectId
    )

    $Section.appBundleId = $BundleId

    if ($null -eq $Section.OneSignal) {
        $Section | Add-Member -NotePropertyName OneSignal -NotePropertyValue ([pscustomobject]@{})
    }

    $Section.OneSignal.AppId = $OneSignalAppId
    $Section.OneSignal.ProjectId = $OneSignalProjectId
}

Write-Host ""
Write-Host "Updating ${RepoPath} on ${Owner}/${Repo} (branch: ${Branch})..."
Write-Host "  appBundleId : $AppBundleId"
Write-Host "  AppId       : $AppId"
Write-Host "  ProjectId   : $ProjectId"

$FileInfo = Get-GitHubFileContent -Path $RepoPath

$EncodedContent = ($FileInfo.content -replace '\s', '')
$JsonBytes = [Convert]::FromBase64String($EncodedContent)
$JsonText = [System.Text.Encoding]::UTF8.GetString($JsonBytes)
$Config = $JsonText | ConvertFrom-Json

if ($null -eq $Config.release -or $null -eq $Config.debug) {
    throw "build.environment.json must contain 'release' and 'debug' sections."
}

Set-BuildEnvironmentSection -Section $Config.release -BundleId $AppBundleId -OneSignalAppId $AppId -OneSignalProjectId $ProjectId
Set-BuildEnvironmentSection -Section $Config.debug -BundleId $AppBundleId -OneSignalAppId $AppId -OneSignalProjectId $ProjectId

$UpdatedJson = ($Config | ConvertTo-Json -Depth 20)
$Base64Content = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($UpdatedJson))

$Body = @{
    message = "Updated build.environment.json appBundleId and OneSignal settings"
    content = $Base64Content
    sha     = $FileInfo.sha
    branch  = $Branch
} | ConvertTo-Json

$UpdateUrl = "https://api.github.com/repos/$Owner/$Repo/contents/${RepoPath}"

Write-Host "PUT URL:"
Write-Host $UpdateUrl

Invoke-RestMethod `
    -Method PUT `
    -Uri $UpdateUrl `
    -Headers $Headers `
    -Body $Body

Write-Host ""
Write-Host "${RepoPath} updated successfully."
