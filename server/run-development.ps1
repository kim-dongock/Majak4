param(
    [ValidateSet("studio35_majak4", "hange_majak4")]
    [string]$AwsProfile = "studio35_majak4",

    [ValidateSet("Development", "Alpha")]
    [string]$AspNetCoreEnvironment = "Development"
)

$ErrorActionPreference = "Stop"

$env:ASPNETCORE_ENVIRONMENT = $AspNetCoreEnvironment
$env:AWS__ProfileName = $AwsProfile
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

Write-Host "Starting Majak4 server..." -ForegroundColor Cyan
Write-Host "  Environment : $AspNetCoreEnvironment" -ForegroundColor Cyan
Write-Host "  AWS profile : $AwsProfile" -ForegroundColor Cyan

Push-Location $PSScriptRoot
try {
    dotnet run
} finally {
    Pop-Location
}