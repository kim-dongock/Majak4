# Majak4 Hange Alpha API Deployment
# Usage:
#   .\deploy-hange-majak4.ps1
#   .\deploy-hange-majak4.ps1 -Server <ssh-host> -User <ssh-user> -SshKey <key-path>
# Prerequisites: SSH access, sudo permission, and local .NET SDK

param(
    [string]$Server = "hange-alpha-mahjong4.coconefk",
    [string]$Domain = "alpha-app-majak4.hange.jp",
    [string]$User = "www-web",
    [string]$SshKey = "C:\Users\kim_dongock\Documents\SSH\open_ssl_hange",
    [int]$Port = 5003
)

$ErrorActionPreference = "Stop"

$PROJECT_DIR = $PSScriptRoot
$ZIP_NAME = "hange-majak4-api.zip"
$ZIP_PATH = Join-Path $PSScriptRoot $ZIP_NAME
$PUBLISH_DIR = Join-Path $PSScriptRoot "publish-hange"
$REMOTE_PATH = "/var/www/hange-majak4-api"
$SERVICE = "hange-majak4-api"
$AWS_REGION = "ap-northeast-1"

function Assert-LastCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Operation,
        [int[]]$AllowedExitCodes = @(0)
    )

    if ($LASTEXITCODE -notin $AllowedExitCodes) {
        throw "$Operation failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Ssh {
    param([Parameter(Mandatory = $true)][string]$Command)

    & ssh -i $SshKey -o StrictHostKeyChecking=no "${User}@${Server}" $Command
    Assert-LastCommand "SSH command '$Command'"
}

function Test-SshCommand {
    param([Parameter(Mandatory = $true)][string]$Command)

    & ssh -i $SshKey -o StrictHostKeyChecking=no "${User}@${Server}" $Command *> $null
    return $LASTEXITCODE -eq 0
}

function Invoke-SudoSsh {
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [int[]]$AllowedExitCodes = @(0)
    )

    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($script:SudoPassword)
    try {
        $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        $plainPassword | & ssh -i $SshKey -o StrictHostKeyChecking=no "${User}@${Server}" `
            "sudo -S -p '' -- $Command"
        Assert-LastCommand -Operation "Sudo SSH command '$Command'" -AllowedExitCodes $AllowedExitCodes
    } finally {
        $plainPassword = $null
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }
}

function Test-SudoSshCommand {
    param([Parameter(Mandatory = $true)][string]$Command)

    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($script:SudoPassword)
    try {
        $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        $plainPassword | & ssh -i $SshKey -o StrictHostKeyChecking=no "${User}@${Server}" `
            "sudo -S -p '' -- $Command" *> $null
        return $LASTEXITCODE -eq 0
    } finally {
        $plainPassword = $null
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }
}

Write-Host "=== Majak4 Hange Alpha API Deployment ===" -ForegroundColor Yellow
Write-Host "Target: ${User}@${Server}" -ForegroundColor Cyan
$script:SudoPassword = Read-Host "Remote sudo password for $User" -AsSecureString
$tmpServiceUnit = $null

try {
    Invoke-SudoSsh "true"

    Write-Host "`n[1/6] Checking remote ASP.NET Core runtime..." -ForegroundColor Yellow
    $remoteArchitectureOutput = & ssh -i $SshKey -o StrictHostKeyChecking=no "${User}@${Server}" "uname -m"
    Assert-LastCommand "Remote architecture check"
    $remoteArchitecture = ($remoteArchitectureOutput | Out-String).Trim()
    if ($remoteArchitecture -notin @("aarch64", "arm64")) {
        throw "The remote architecture is '$remoteArchitecture', but this deployment targets linux-arm64."
    }

    $runtimeCheckCommand = "command -v dotnet >/dev/null 2>&1 && dotnet --list-runtimes 2>/dev/null | grep -q '^Microsoft\.AspNetCore\.App 8\.'"
    if (-not (Test-SshCommand $runtimeCheckCommand)) {
        Write-Host "ASP.NET Core 8 Runtime is missing. Installing it with the Microsoft installer..." -ForegroundColor Yellow
        if (Test-SshCommand "command -v curl >/dev/null 2>&1") {
            Invoke-Ssh "curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh"
        } elseif (Test-SshCommand "command -v wget >/dev/null 2>&1") {
            Invoke-Ssh "wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh"
        } else {
            throw "The remote server requires curl or wget to install ASP.NET Core 8 Runtime."
        }
        Invoke-SudoSsh "bash /tmp/dotnet-install.sh --channel 8.0 --runtime aspnetcore --architecture arm64 --install-dir /usr/share/dotnet --no-path"
        Invoke-SudoSsh "ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet"
        Invoke-Ssh "rm -f /tmp/dotnet-install.sh"
    }
    Invoke-Ssh $runtimeCheckCommand

    $icuCheckCommand = "ldconfig -p 2>/dev/null | grep -q 'libicuuc\.so'"
    if (-not (Test-SudoSshCommand $icuCheckCommand)) {
        Write-Host "ICU globalization libraries are missing. Installing them..." -ForegroundColor Yellow
        if (Test-SshCommand "command -v dnf >/dev/null 2>&1") {
            Invoke-SudoSsh "dnf install -y libicu"
        } elseif (Test-SshCommand "command -v yum >/dev/null 2>&1") {
            Invoke-SudoSsh "yum install -y libicu"
        } elseif (Test-SshCommand "command -v apt-get >/dev/null 2>&1") {
            Invoke-SudoSsh "apt-get update"
            Invoke-SudoSsh "env DEBIAN_FRONTEND=noninteractive apt-get install -y libicu-dev"
        } elseif (Test-SshCommand "command -v apk >/dev/null 2>&1") {
            Invoke-SudoSsh "apk add icu-libs"
        } elseif (Test-SshCommand "command -v zypper >/dev/null 2>&1") {
            Invoke-SudoSsh "zypper --non-interactive install libicu"
        } else {
            throw "ICU libraries are missing and no supported package manager was found on the remote server."
        }
    }
    if (-not (Test-SudoSshCommand $icuCheckCommand)) {
        throw "ICU installation completed, but libicuuc is still unavailable on the remote server."
    }

    Write-Host "`n[2/6] Publishing Alpha build..." -ForegroundColor Yellow
    Push-Location $PROJECT_DIR
    try {
        & dotnet publish -c Release -r linux-arm64 --self-contained false -o $PUBLISH_DIR
        Assert-LastCommand "dotnet publish"
    } finally {
        Pop-Location
    }

    Write-Host "`n[3/6] Compressing deployment package..." -ForegroundColor Yellow
    if (Test-Path $ZIP_PATH) {
        Remove-Item $ZIP_PATH -Force
    }
    Compress-Archive -Path "$PUBLISH_DIR/*" -DestinationPath $ZIP_PATH

    Write-Host "`n[4/6] Transferring package to $Server..." -ForegroundColor Yellow
    & scp -i $SshKey -o StrictHostKeyChecking=no $ZIP_PATH "${User}@${Server}:~/"
    Assert-LastCommand "SCP transfer"

    Write-Host "`n[5/6] Configuring the Hange systemd service..." -ForegroundColor Yellow
    $serviceUnit = @"
[Unit]
Description=Majak4 Hange Alpha API
After=network.target

[Service]
WorkingDirectory=$REMOTE_PATH
ExecStart=/usr/bin/dotnet $REMOTE_PATH/MajakServer.dll
Restart=always
RestartSec=10
SyslogIdentifier=$SERVICE
User=$User
Environment=ASPNETCORE_ENVIRONMENT=Alpha
Environment=ASPNETCORE_URLS=http://0.0.0.0:$Port
Environment=AWS__Region=$AWS_REGION

[Install]
WantedBy=multi-user.target
"@
    $tmpServiceUnit = Join-Path $env:TEMP "hange-majak4-api.service"
    [System.IO.File]::WriteAllText($tmpServiceUnit, $serviceUnit, [System.Text.UTF8Encoding]::new($false))
    & scp -i $SshKey -o StrictHostKeyChecking=no $tmpServiceUnit "${User}@${Server}:/tmp/${SERVICE}.service"
    Assert-LastCommand "systemd unit transfer"
    Invoke-SudoSsh "cp /tmp/${SERVICE}.service /etc/systemd/system/${SERVICE}.service"
    Invoke-Ssh "rm /tmp/${SERVICE}.service"
    Remove-Item $tmpServiceUnit -Force -ErrorAction SilentlyContinue
    Invoke-SudoSsh "systemctl daemon-reload"
    Invoke-SudoSsh "systemctl enable $SERVICE"

    Write-Host "`n[6/6] Deploying and restarting the service..." -ForegroundColor Yellow
    Invoke-SudoSsh "systemctl stop $SERVICE"
    Invoke-SudoSsh "mkdir -p $REMOTE_PATH"
    Invoke-SudoSsh "find $REMOTE_PATH -mindepth 1 -maxdepth 1 -exec rm -rf {} +"
    Invoke-SudoSsh -Command "unzip -o ~/$ZIP_NAME -d $REMOTE_PATH" -AllowedExitCodes @(0, 1)
    Invoke-SudoSsh "test -f $REMOTE_PATH/MajakServer.dll"
    Invoke-SudoSsh "chown -R ${User}:${User} $REMOTE_PATH"
    Invoke-Ssh "rm ~/$ZIP_NAME"
    Invoke-SudoSsh "systemctl start $SERVICE"
    Invoke-SudoSsh "systemctl is-active --quiet $SERVICE"
    Invoke-Ssh "curl -fs --retry 30 --retry-delay 1 --retry-connrefused http://127.0.0.1:$Port/health >/dev/null"

    if (Test-SshCommand "systemctl list-unit-files nginx.service --no-legend 2>/dev/null | grep -q '^nginx.service'") {
        Write-Host "    Disabling the unused Nginx service..." -ForegroundColor Yellow
        Invoke-SudoSsh "systemctl disable --now nginx"
    }

    Write-Host "`n=== Majak4 Hange Alpha API Deployment Complete ===" -ForegroundColor Green
    Write-Host "Server : $Server" -ForegroundColor Cyan
    Write-Host "API    : https://$Domain" -ForegroundColor Cyan
    Write-Host "Target : HTTP ${Server}:$Port" -ForegroundColor Cyan
    Write-Host "Health : HTTP ${Server}:$Port/health" -ForegroundColor Cyan
    Write-Host "Logs   : ssh -i $SshKey ${User}@${Server} 'sudo journalctl -u $SERVICE -f'" -ForegroundColor Gray
    Write-Host "`nService Status:" -ForegroundColor Yellow
    Invoke-SudoSsh "systemctl status $SERVICE --no-pager"
} finally {
    $script:SudoPassword = $null
    Remove-Item $ZIP_PATH -Force -ErrorAction SilentlyContinue
    Remove-Item $PUBLISH_DIR -Recurse -Force -ErrorAction SilentlyContinue
    if ($null -ne $tmpServiceUnit) {
        Remove-Item $tmpServiceUnit -Force -ErrorAction SilentlyContinue
    }
}