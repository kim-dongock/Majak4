# Start Redis (Docker), client, admin and server in separate windows
Write-Host "Starting Majak2 dev environment..." -ForegroundColor Cyan

$rootDir = $PSScriptRoot

# Start Redis via Docker Compose
Write-Host "Starting Redis (Docker)..." -ForegroundColor Magenta
$dockerResult = & docker compose -f "$rootDir\docker-compose.yml" up -d 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start Redis: $dockerResult" -ForegroundColor Red
    exit 1
}
Write-Host "Redis started." -ForegroundColor Magenta

# Start server in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", `
    "`$env:DOTNET_CLI_UI_LANGUAGE='en-US'; Set-Location '$rootDir\server'; dotnet run" `
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
Write-Host "  Client : http://localhost:3000" -ForegroundColor Cyan
Write-Host "  Admin  : http://localhost:5174" -ForegroundColor Cyan
Write-Host ""
Write-Host "Close those windows to stop. To stop Redis run: docker compose down" -ForegroundColor Gray
