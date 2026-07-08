param(
    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [Parameter(Mandatory = $true)]
    [string]$Owner,

    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [Parameter(Mandatory = $false)]
    [string]$WorkflowFileName = 'Base-client-deployment.yml',

    [Parameter(Mandatory = $false)]
    [int]$WaitSeconds = 10
)

$ErrorActionPreference = 'Stop'

$Headers = @{
    Authorization = "Bearer $GitHubToken"
    Accept        = "application/vnd.github+json"
}

if ($WaitSeconds -gt 0) {
    Write-Host "Waiting ${WaitSeconds}s for GitHub to register the client workflow..."
    Start-Sleep -Seconds $WaitSeconds
}

$DispatchUrl = "https://api.github.com/repos/$Owner/$Repo/actions/workflows/${WorkflowFileName}/dispatches"

$Body = @{
    ref = $Branch
} | ConvertTo-Json

Write-Host ""
Write-Host "Triggering client deployment workflow..."
Write-Host "  Repository : ${Owner}/${Repo}"
Write-Host "  Workflow   : $WorkflowFileName"
Write-Host "  Ref        : $Branch"
Write-Host "POST URL:"
Write-Host $DispatchUrl

try {
    Invoke-RestMethod `
        -Method POST `
        -Uri $DispatchUrl `
        -Headers $Headers `
        -Body $Body `
        -ContentType 'application/json'
}
catch {
    $response = $_.Exception.Response
    if ($null -ne $response) {
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        throw "Failed to trigger client workflow ($([int]$response.StatusCode)): $errorBody"
    }

    throw
}

Write-Host ""
Write-Host "Client deployment workflow triggered successfully."
