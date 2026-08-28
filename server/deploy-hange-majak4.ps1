# Majak4 Hange Alpha API Deployment
# Usage:
#   .\deploy-hange-majak4.ps1
#   .\deploy-hange-majak4.ps1 -Server <ssh-host> -User <ssh-user> -SshKey <key-path>
# Prerequisites: SSH access, sudo permission, local .NET SDK, and remote HTTPS access

param(
    [string]$Server = "hange-alpha-mahjong4.coconefk",
    [string]$Domain = "alpha-app-majak4.hange.jp",
    [string]$User = "www-web",
    [string]$SshKey = "C:\Users\kim_dongock\Documents\SSH\open_ssl_hange",
    [int]$Port = 5003
)

$ErrorActionPreference = "Stop"

$REMOTE_PATH = "/var/www/hange-majak4-api"
$SERVICE = "hange-majak4-api"
$PROJECT_DIR = $PSScriptRoot
$ZIP_NAME = "hange-majak4-api.zip"
$ZIP_PATH = Join-Path $PSScriptRoot $ZIP_NAME
$PUBLISH_DIR = Join-Path $PSScriptRoot "publish-hange"
$LOCAL_ENV_PATH = Join-Path $PSScriptRoot ".env.hange-alpha"

if (-not (Test-Path $LOCAL_ENV_PATH)) {
    throw "Hange deployment settings are missing. Create '$LOCAL_ENV_PATH' from '.env.example'."
}

$localSettings = @{}
Get-Content $LOCAL_ENV_PATH | ForEach-Object {
    $line = $_.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith("#")) { return }
    $parts = $line -split "=", 2
    if ($parts.Count -eq 2) {
        $localSettings[$parts[0].Trim()] = $parts[1].Trim()
    }
}

$requiredSettings = @("GAME_DB_CONNECTION", "LOG_DB_CONNECTION", "REDIS_CONNECTION")
foreach ($settingName in $requiredSettings) {
    if ([string]::IsNullOrWhiteSpace($localSettings[$settingName])) {
        throw "Required Hange deployment setting '$settingName' is missing from '$LOCAL_ENV_PATH'."
    }
}

$GAME_DB_CONNECTION = $localSettings["GAME_DB_CONNECTION"]
$LOG_DB_CONNECTION = $localSettings["LOG_DB_CONNECTION"]
$REDIS_CONNECTION = $localSettings["REDIS_CONNECTION"]

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
$tmpNginxConf = $null

try {
    Invoke-SudoSsh "true"

    Write-Host "`n[1/7] Checking remote ASP.NET Core runtime..." -ForegroundColor Yellow
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

    Write-Host "`n[2/7] Publishing Alpha build..." -ForegroundColor Yellow
    Push-Location $PROJECT_DIR
    try {
        & dotnet publish -c Release -r linux-arm64 --self-contained false -o $PUBLISH_DIR
        Assert-LastCommand "dotnet publish"
    } finally {
        Pop-Location
    }

    Write-Host "`n[3/7] Compressing deployment package..." -ForegroundColor Yellow
    if (Test-Path $ZIP_PATH) {
        Remove-Item $ZIP_PATH -Force
    }
    Compress-Archive -Path "$PUBLISH_DIR/*" -DestinationPath $ZIP_PATH

    Write-Host "`n[4/7] Transferring package to $Server..." -ForegroundColor Yellow
    & scp -i $SshKey -o StrictHostKeyChecking=no $ZIP_PATH "${User}@${Server}:~/"
    Assert-LastCommand "SCP transfer"

    Write-Host "`n[5/7] Configuring the Hange systemd service..." -ForegroundColor Yellow
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
Environment=ASPNETCORE_URLS=http://localhost:$Port
Environment=AWS__ParameterStore__Enabled=false
Environment="ConnectionStrings__GameDatabase=$GAME_DB_CONNECTION"
Environment="ConnectionStrings__LogDatabase=$LOG_DB_CONNECTION"
Environment="Redis__ConnectionString=$REDIS_CONNECTION"

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

    Write-Host "`n[6/7] Deploying and restarting the service..." -ForegroundColor Yellow
    Invoke-SudoSsh "systemctl stop $SERVICE"
    Invoke-SudoSsh "mkdir -p $REMOTE_PATH"
    Invoke-SudoSsh "find $REMOTE_PATH -mindepth 1 -maxdepth 1 -exec rm -rf {} +"
    Invoke-SudoSsh -Command "unzip -o ~/$ZIP_NAME -d $REMOTE_PATH" -AllowedExitCodes @(0, 1)
    Invoke-SudoSsh "test -f $REMOTE_PATH/MajakServer.dll"
    Invoke-SudoSsh "chown -R ${User}:${User} $REMOTE_PATH"
    Invoke-Ssh "rm ~/$ZIP_NAME"
    Invoke-SudoSsh "systemctl start $SERVICE"
    Invoke-SudoSsh "systemctl is-active --quiet $SERVICE"

    Write-Host "`n[7/7] Configuring Nginx for $Domain..." -ForegroundColor Yellow
    $nginxCheckCommand = "command -v nginx >/dev/null 2>&1 || test -x /usr/sbin/nginx"
    if (-not (Test-SshCommand $nginxCheckCommand)) {
        Write-Host "Nginx is missing. Installing it with the available package manager..." -ForegroundColor Yellow
        if (Test-SshCommand "command -v amazon-linux-extras >/dev/null 2>&1") {
            Invoke-SudoSsh "amazon-linux-extras install -y nginx1"
        } elseif (Test-SshCommand "command -v dnf >/dev/null 2>&1") {
            Invoke-SudoSsh "dnf install -y nginx"
        } elseif (Test-SshCommand "command -v yum >/dev/null 2>&1") {
            Invoke-SudoSsh "yum install -y nginx"
        } elseif (Test-SshCommand "command -v apt-get >/dev/null 2>&1") {
            Invoke-SudoSsh "apt-get update"
            Invoke-SudoSsh "env DEBIAN_FRONTEND=noninteractive apt-get install -y nginx"
        } elseif (Test-SshCommand "command -v zypper >/dev/null 2>&1") {
            Invoke-SudoSsh "zypper --non-interactive install nginx"
        } elseif (Test-SshCommand "command -v apk >/dev/null 2>&1") {
            Invoke-SudoSsh "apk add nginx"
        } else {
            throw "Nginx is missing and no supported package manager was found on the remote server."
        }
    }
    Invoke-Ssh $nginxCheckCommand
    if (Test-SshCommand "test -d /etc/nginx/sites-available && test -d /etc/nginx/sites-enabled") {
        $nginxTarget = "/etc/nginx/sites-available/$Domain"
        $nginxEnabled = "/etc/nginx/sites-enabled/$Domain"
    } else {
        Invoke-SudoSsh "mkdir -p /etc/nginx/conf.d"
        $nginxTarget = "/etc/nginx/conf.d/$Domain.conf"
        $nginxEnabled = $null
    }

    $hasCertificate = Test-SudoSshCommand "test -f /etc/letsencrypt/live/$Domain/fullchain.pem"
    $tmpNginxConf = Join-Path $env:TEMP "nginx-hange-majak4-deploy.conf"
    if ($hasCertificate) {
        $nginxConfig = @"
server {
    server_name $Domain;

    location / {
        proxy_pass         http://localhost:$Port;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade `$http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host `$host;
        proxy_set_header   X-Real-IP `$remote_addr;
        proxy_set_header   X-Forwarded-For `$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto `$scheme;
        proxy_cache_bypass `$http_upgrade;
        proxy_buffering    off;
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
        proxy_connect_timeout 10s;
        client_max_body_size 20M;
    }

    listen 443 ssl;
    ssl_certificate /etc/letsencrypt/live/$Domain/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$Domain/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;
}

server {
    if (`$host = $Domain) {
        return 301 https://`$host`$request_uri;
    }
    listen 80;
    server_name $Domain;
    return 404;
}
"@
    } else {
        Write-Host "Let's Encrypt certificate is missing. Installing an HTTP bootstrap configuration." -ForegroundColor Yellow
        $nginxConfig = @"
server {
    listen 80;
    server_name $Domain;

    location / {
        proxy_pass         http://localhost:$Port;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade `$http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host `$host;
        proxy_set_header   X-Real-IP `$remote_addr;
        proxy_set_header   X-Forwarded-For `$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto `$scheme;
        proxy_cache_bypass `$http_upgrade;
        proxy_buffering    off;
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
        proxy_connect_timeout 10s;
        client_max_body_size 20M;
    }
}
"@
    }
    [System.IO.File]::WriteAllText($tmpNginxConf, $nginxConfig, [System.Text.UTF8Encoding]::new($false))
    & scp -i $SshKey -o StrictHostKeyChecking=no $tmpNginxConf "${User}@${Server}:/tmp/nginx-hange-majak4-deploy.conf"
    Assert-LastCommand "Nginx configuration transfer"
    Invoke-SudoSsh "cp /tmp/nginx-hange-majak4-deploy.conf $nginxTarget"
    if ($null -ne $nginxEnabled) {
        Invoke-SudoSsh "ln -sf $nginxTarget $nginxEnabled"
    }
    Invoke-SudoSsh "nginx -t"
    Invoke-SudoSsh "systemctl enable nginx"
    Invoke-SudoSsh "systemctl restart nginx"

    if (-not $hasCertificate) {
        $certbotCheckCommand = "command -v certbot >/dev/null 2>&1 || test -x /usr/bin/certbot"
        if (-not (Test-SshCommand $certbotCheckCommand)) {
            Write-Host "Certbot is missing. Installing Certbot and its Nginx plugin..." -ForegroundColor Yellow
            if (Test-SshCommand "command -v dnf >/dev/null 2>&1") {
                Invoke-SudoSsh "dnf install -y certbot python3-certbot-nginx"
            } elseif (Test-SshCommand "command -v yum >/dev/null 2>&1") {
                Invoke-SudoSsh "yum install -y certbot python3-certbot-nginx"
            } elseif (Test-SshCommand "command -v apt-get >/dev/null 2>&1") {
                Invoke-SudoSsh "apt-get update"
                Invoke-SudoSsh "env DEBIAN_FRONTEND=noninteractive apt-get install -y certbot python3-certbot-nginx"
            } elseif (Test-SshCommand "command -v zypper >/dev/null 2>&1") {
                Invoke-SudoSsh "zypper --non-interactive install certbot python3-certbot-nginx"
            } elseif (Test-SshCommand "command -v apk >/dev/null 2>&1") {
                Invoke-SudoSsh "apk add certbot certbot-nginx"
            } else {
                throw "Certbot is missing and no supported package manager was found on the remote server."
            }
        }
        Invoke-Ssh $certbotCheckCommand
        Write-Host "Issuing a Let's Encrypt certificate for $Domain..." -ForegroundColor Yellow
        Invoke-SudoSsh "certbot --nginx --non-interactive --agree-tos --register-unsafely-without-email --redirect --keep-until-expiring -d $Domain"
        $hasCertificate = Test-SudoSshCommand "test -f /etc/letsencrypt/live/$Domain/fullchain.pem"
        if (-not $hasCertificate) {
            throw "Certbot completed, but the certificate for '$Domain' was not found."
        }
    }

    Invoke-Ssh "rm /tmp/nginx-hange-majak4-deploy.conf"
    Remove-Item $tmpNginxConf -Force -ErrorAction SilentlyContinue

    Write-Host "`n=== Majak4 Hange Alpha API Deployment Complete ===" -ForegroundColor Green
    Write-Host "Server : $Server" -ForegroundColor Cyan
    $apiScheme = if ($hasCertificate) { "https" } else { "http" }
    Write-Host "API    : ${apiScheme}://$Domain" -ForegroundColor Cyan
    Write-Host "Port   : $Port" -ForegroundColor Cyan
    if ($hasCertificate) {
        Write-Host "SSL    : Let's Encrypt certificate configured" -ForegroundColor Cyan
    } else {
        Write-Host "SSL    : Pending public DNS A/AAAA record for $Domain" -ForegroundColor Yellow
    }
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
    if ($null -ne $tmpNginxConf) {
        Remove-Item $tmpNginxConf -Force -ErrorAction SilentlyContinue
    }
}