param(
    [ValidateSet("studio35_majak4", "hange_majak4")]
    [string]$AwsProfile = "studio35_majak4",

    [ValidateSet("Development", "Alpha")]
    [string]$ServerEnvironment = "Development"
)

# Start client, admin and server in separate windows.
# The server environment selects the Parameter Store path; AwsProfile selects the AWS account.
Write-Host "Starting Majak4 dev environment..." -ForegroundColor Cyan
Write-Host "  Server environment : $ServerEnvironment" -ForegroundColor Cyan
Write-Host "  AWS profile        : $AwsProfile" -ForegroundColor Cyan

$rootDir = $PSScriptRoot

# Start server in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", `
    "& '$rootDir\server\run-development.ps1' -AwsProfile '$AwsProfile' -AspNetCoreEnvironment '$ServerEnvironment'" `
    -WindowStyle Normal

# Start client in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", `
    "Set-Location '$rootDir\client'; npm run dev" `
    -WindowStyle Normal

# Start admin in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", `
    "Set-Location '$rootDir\admin'; npm run dev" `
    -WindowStyle Normal

Write-Host ""
Write-Host "All windows opened:" -ForegroundColor Green
Write-Host "  Server : http://localhost:5246" -ForegroundColor Cyan
Write-Host "  Client : http://localhost:5173" -ForegroundColor Cyan
Write-Host "  Admin  : http://localhost:5174" -ForegroundColor Cyan
Write-Host ""
Write-Host "Close those windows to stop." -ForegroundColor Gray
