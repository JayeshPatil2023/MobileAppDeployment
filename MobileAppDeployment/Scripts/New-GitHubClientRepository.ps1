#requires -Version 5.1
<#
.SYNOPSIS
    Creates a new public GitHub repository for a mobile app client deployment.

.DESCRIPTION
    Uses the GitHub REST API (POST https://api.github.com/user/repos) to create a repository
    under the authenticated user account (JayeshPatil2023). The repository name is derived
    from the client app name with validation and sanitization applied before the API call.

.PARAMETER AppName
    The client app name from the deployment form. Used as the basis for the repository name.

.PARAMETER Description
    Optional repository description. Defaults to a generated message when omitted.

.PARAMETER Private
    When specified, creates a private repository instead of public.

.PARAMETER AccessToken
    GitHub Personal Access Token. Defaults to $env:GITHUB_TOKEN, then YOUR_GITHUB_TOKEN placeholder.

.EXAMPLE
    .\New-GitHubClientRepository.ps1 -AppName "EliteBids"

.EXAMPLE
    $env:GITHUB_TOKEN = "ghp_xxxx"
    .\New-GitHubClientRepository.ps1 -AppName "EliteBids" -Description "EliteBids mobile deployment"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AppName,

    [Parameter(Mandatory = $false)]
    [string]$Description = "",

    [Parameter(Mandatory = $false)]
    [switch]$Private
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

#region Configuration

$Script:GitHubApiCreateRepoUrl = "https://api.github.com/user/repos"
$Script:GitHubOwnerLogin = "JayeshPatil2023"
$Script:GitHubWebBaseUrl = "https://github.com/$Script:GitHubOwnerLogin"
$Script:LogPrefix = "[GitHub-Repo]"

#endregion

#region Logging

<#
.SYNOPSIS
    Writes a timestamped log message to the host.
#>
function Write-GitHubLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter(Mandatory = $false)]
        [ValidateSet("INFO", "WARN", "ERROR")]
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "$Script:LogPrefix [$Level] $timestamp - $Message"
}

#endregion

#region Validation

<#
.SYNOPSIS
    Validates and sanitizes a client app name for use as a GitHub repository name.

.DESCRIPTION
  GitHub repository names must be 1-100 characters and may contain alphanumeric ASCII,
  hyphens, underscores, and periods. This function trims input, replaces spaces with
  hyphens, removes invalid characters, and enforces GitHub naming rules.

.OUTPUTS
    System.String - The sanitized repository name.
#>
function Get-ValidatedGitHubRepositoryName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppName
    )

    if ([string]::IsNullOrWhiteSpace($AppName)) {
        throw "App name cannot be empty."
    }

    $sanitized = $AppName.Trim()
    $sanitized = $sanitized -replace '\s+', '-'
    $sanitized = $sanitized -replace '[^a-zA-Z0-9._-]', ''
    $sanitized = $sanitized -replace '-{2,}', '-'
    $sanitized = $sanitized -replace '\.{2,}', '.'
    $sanitized = $sanitized.Trim('-', '.')

    if ([string]::IsNullOrWhiteSpace($sanitized)) {
        throw "App name '$AppName' cannot be converted into a valid GitHub repository name."
    }

    if ($sanitized.Length -gt 100) {
        throw "Repository name exceeds GitHub's 100-character limit after sanitization."
    }

    if ($sanitized -match '^[-.]|[-.]$') {
        throw "Repository name '$sanitized' cannot start or end with a hyphen or period."
    }

    return $sanitized
}

<#
.SYNOPSIS
    Resolves the GitHub Personal Access Token from parameter, environment, or placeholder.
#>
function Get-GitHubAccessToken {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$AccessToken
    )

    if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
        return $AccessToken.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        return $env:GITHUB_TOKEN.Trim()
    }

    return "YOUR_GITHUB_TOKEN"
}

<#
.SYNOPSIS
    Ensures the access token is present and not the placeholder value.
#>
function Test-GitHubAccessToken {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    if ([string]::IsNullOrWhiteSpace($Token) -or $Token -eq "YOUR_GITHUB_TOKEN") {
        throw "GitHub Personal Access Token is not configured. Set GITHUB_TOKEN or replace YOUR_GITHUB_TOKEN in the script."
    }
}

#endregion

#region API

<#
.SYNOPSIS
    Builds standard headers for GitHub REST API requests.
#>
function New-GitHubRequestHeaders {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AccessToken
    )

    return @{
        Authorization = "Bearer $AccessToken"
        Accept        = "application/vnd.github+json"
        "User-Agent"  = "Systenics-MobileAppDeployment/1.0"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
}

<#
.SYNOPSIS
    Safely extracts a human-readable message from a failed GitHub REST API call.
#>
function Get-GitHubApiFailureMessage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    $fallback = $ErrorRecord.Exception.Message
    $rawBody = $null

    if ($null -ne $ErrorRecord.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($ErrorRecord.ErrorDetails.Message)) {
        $rawBody = $ErrorRecord.ErrorDetails.Message
    }
    elseif ($null -ne $ErrorRecord.Exception.Response) {
        try {
            $responseStream = $ErrorRecord.Exception.Response.GetResponseStream()
            if ($null -ne $responseStream) {
                $reader = New-Object System.IO.StreamReader($responseStream)
                $rawBody = $reader.ReadToEnd()
                $reader.Close()
                $responseStream.Close()
            }
        }
        catch {
            Write-GitHubLog -Message "Could not read GitHub error response body: $($_.Exception.Message)" -Level "WARN"
        }
    }

    if ([string]::IsNullOrWhiteSpace($rawBody)) {
        return $fallback
    }

    try {
        $errorJson = $rawBody | ConvertFrom-Json
        $parts = New-Object System.Collections.Generic.List[string]

        if ($null -ne $errorJson.PSObject.Properties['message']) {
            $messageValue = [string]$errorJson.message
            if (-not [string]::IsNullOrWhiteSpace($messageValue)) {
                [void]$parts.Add($messageValue)
            }
        }

        if ($null -ne $errorJson.PSObject.Properties['errors'] -and $null -ne $errorJson.errors) {
            foreach ($item in @($errorJson.errors)) {
                if ($null -ne $item.PSObject.Properties['message']) {
                    $itemMessage = [string]$item.message
                    if (-not [string]::IsNullOrWhiteSpace($itemMessage)) {
                        [void]$parts.Add($itemMessage)
                    }
                }
            }
        }

        if ($parts.Count -gt 0) {
            return ($parts -join '; ')
        }
    }
    catch {
        Write-GitHubLog -Message "Could not parse GitHub error JSON. Using raw response body." -Level "WARN"
    }

    return $rawBody
}

<#
.SYNOPSIS
    Creates a GitHub repository via the REST API.

.OUTPUTS
    PSCustomObject containing html_url, name, and full_name from the API response.
#>
function New-GitHubRepository {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryName,

        [Parameter(Mandatory = $true)]
        [string]$AccessToken,

        [Parameter(Mandatory = $false)]
        [string]$Description = "",

        [Parameter(Mandatory = $false)]
        [bool]$IsPrivate = $false
    )

    $headers = New-GitHubRequestHeaders -AccessToken $AccessToken

    if ([string]::IsNullOrWhiteSpace($Description)) {
        $Description = "Mobile app deployment repository for $RepositoryName (created by Systenics App Deployment)."
    }

    $body = @{
        name        = $RepositoryName
        description = $Description
        private     = $IsPrivate
        auto_init   = $true
        has_issues  = $true
    } | ConvertTo-Json -Compress

    Write-GitHubLog -Message "Creating repository '$RepositoryName' at $Script:GitHubApiCreateRepoUrl"

    try {
        $response = Invoke-RestMethod `
            -Uri $Script:GitHubApiCreateRepoUrl `
            -Method Post `
            -Headers $headers `
            -Body $body `
            -ContentType "application/json; charset=utf-8"
    }
    catch {
        $restError = $_
        $statusCode = $null
        $apiMessage = Get-GitHubApiFailureMessage -ErrorRecord $restError

        if ($null -ne $restError.Exception.Response -and $null -ne $restError.Exception.Response.StatusCode) {
            $statusCode = [int]$restError.Exception.Response.StatusCode
        }

        if ($statusCode -eq 422) {
            throw "GitHub rejected the repository name '$RepositoryName'. $apiMessage"
        }

        if ($statusCode -eq 401 -or $statusCode -eq 403) {
            throw "GitHub authentication failed (HTTP $statusCode). $apiMessage Verify your Personal Access Token has the 'repo' scope."
        }

        if ($null -ne $statusCode) {
            throw "GitHub API request failed (HTTP $statusCode): $apiMessage"
        }

        throw "GitHub API request failed: $apiMessage"
    }

    if (-not $response.html_url) {
        throw "GitHub API returned an unexpected response without a repository URL."
    }

    Write-GitHubLog -Message "Repository created: $($response.html_url)"

    return [PSCustomObject]@{
        name      = $response.name
        full_name = $response.full_name
        html_url  = $response.html_url
        clone_url = $response.clone_url
    }
}

#endregion

#region Main

<#
.SYNOPSIS
    Orchestrates validation, API call, and structured result output.
#>
function Invoke-ClientRepositoryCreation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppName,

        [Parameter(Mandatory = $false)]
        [string]$Description = "",

        [Parameter(Mandatory = $false)]
        [string]$AccessToken = "",

        [Parameter(Mandatory = $false)]
        [bool]$IsPrivate = $false
    )

    $token = Get-GitHubAccessToken -AccessToken $AccessToken
    Test-GitHubAccessToken -Token $token

    $repositoryName = Get-ValidatedGitHubRepositoryName -AppName $AppName
    Write-GitHubLog -Message "Validated repository name: '$repositoryName' (from app name '$AppName')"

    $repository = New-GitHubRepository `
        -RepositoryName $repositoryName `
        -AccessToken $token `
        -Description $Description `
        -IsPrivate $IsPrivate

    return [PSCustomObject]@{
        success      = $true
        app_name     = $AppName
        repository   = $repository.name
        full_name    = $repository.full_name
        html_url     = $repository.html_url
        clone_url    = $repository.clone_url
        owner        = $Script:GitHubOwnerLogin
        web_base_url = $Script:GitHubWebBaseUrl
    }
}

#endregion

try {
    $result = Invoke-ClientRepositoryCreation `
        -AppName $AppName `
        -Description $Description `
        -IsPrivate:$Private.IsPresent

    # Machine-readable result for ASP.NET Core integration (last line of stdout).
    $result | ConvertTo-Json -Compress
    exit 0
}
catch {
    Write-GitHubLog -Message $_.Exception.Message -Level "ERROR"

    $failure = [PSCustomObject]@{
        success = $false
        error   = $_.Exception.Message
        app_name = $AppName
    }

    $failure | ConvertTo-Json -Compress
    exit 1
}
