# Majak4 API Deployment
# Usage:
#   .\deploy-majak4.ps1
# Prerequisites: SSH key, dotnet SDK

$ErrorActionPreference = "Stop"

$SERVER      = "52.194.197.87"
$USER        = "ubuntu"
$SSH_KEY     = "C:\Users\kim_dongock\.ssh\wonderlog-redis-key2.pem"
$DOMAIN      = "app-majak4.studio35app.net"
$REMOTE_PATH = "/var/www/majak4-api"
$SERVICE     = "majak4-api"
$PORT        = 5003
$PROJECT_DIR = $PSScriptRoot
$ZIP_NAME    = "majak4-api.zip"
$ZIP_PATH    = Join-Path $PSScriptRoot $ZIP_NAME
$PUBLISH_DIR = Join-Path $PSScriptRoot "publish"

function ssh-run($cmd) {
    ssh -i $SSH_KEY -o StrictHostKeyChecking=no "${USER}@${SERVER}" $cmd
}

Write-Host "=== Majak4 API Deployment ===" -ForegroundColor Yellow

# ─── 1. Build ────────────────────────────────────────────────────
Write-Host "`n[1/6] Building..." -ForegroundColor Yellow
Push-Location $PROJECT_DIR
dotnet publish -c Release -r linux-arm64 --self-contained false -o $PUBLISH_DIR
Pop-Location

# ─── 2. Compress ─────────────────────────────────────────────────
Write-Host "`n[2/6] Compressing..." -ForegroundColor Yellow
if (Test-Path $ZIP_PATH) { Remove-Item $ZIP_PATH }
Compress-Archive -Path "$PUBLISH_DIR/*" -DestinationPath $ZIP_PATH

# ─── 3. Transfer ─────────────────────────────────────────────────
Write-Host "`n[3/6] Transferring to $SERVER ..." -ForegroundColor Yellow
scp -i $SSH_KEY -o StrictHostKeyChecking=no $ZIP_PATH "${USER}@${SERVER}:~/"

# ─── 4. Setup systemd service (first deploy only) ────────────────
Write-Host "`n[4/6] Ensuring systemd service exists..." -ForegroundColor Yellow
$serviceUnit = @"
[Unit]
Description=Majak4 API
After=network.target

[Service]
WorkingDirectory=$REMOTE_PATH
ExecStart=/usr/bin/dotnet $REMOTE_PATH/MajakServer.dll
Restart=always
RestartSec=10
SyslogIdentifier=$SERVICE
User=ubuntu
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:$PORT

[Install]
WantedBy=multi-user.target
"@
$serviceUnit | ssh -i $SSH_KEY -o StrictHostKeyChecking=no "${USER}@${SERVER}" `
    "cat | sudo tee /etc/systemd/system/${SERVICE}.service > /dev/null"
ssh-run "sudo systemctl daemon-reload"
ssh-run "sudo systemctl enable $SERVICE"

# ─── 5. Deploy ───────────────────────────────────────────────────
Write-Host "`n[5/6] Deploying on server..." -ForegroundColor Yellow
ssh-run "sudo systemctl stop $SERVICE 2>/dev/null || true"
ssh-run "sleep 8 ; sudo fuser -k ${PORT}/tcp 2>/dev/null || true ; sleep 3"
ssh-run "sudo mkdir -p $REMOTE_PATH"
ssh-run "sudo rm -rf $REMOTE_PATH/*"
ssh-run "sudo unzip -o ~/$ZIP_NAME -d $REMOTE_PATH > /dev/null 2>&1; true"
ssh-run "sudo chown -R ubuntu:ubuntu $REMOTE_PATH"
ssh-run "rm ~/$ZIP_NAME"
ssh-run "sudo systemctl start $SERVICE"

# ─── 6. Setup / reload Nginx ─────────────────────────────────────
Write-Host "`n[6/6] Configuring Nginx..." -ForegroundColor Yellow
$nginxAvailable = "/etc/nginx/sites-available/$DOMAIN"
$tmpNginxConf = Join-Path $env:TEMP "nginx-majak4-deploy.conf"
@"
server {
    listen 80;
    server_name $DOMAIN;

    location / {
        proxy_pass         http://localhost:$PORT;
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
"@ | Out-String | ForEach-Object { [System.IO.File]::WriteAllText($tmpNginxConf, $_, [System.Text.UTF8Encoding]::new($false)) }
scp -i $SSH_KEY -o StrictHostKeyChecking=no $tmpNginxConf "${USER}@${SERVER}:/tmp/nginx-deploy.conf"
ssh-run "sudo cp /tmp/nginx-deploy.conf $nginxAvailable"
ssh-run "sudo ln -sf $nginxAvailable /etc/nginx/sites-enabled/$DOMAIN"
ssh-run "sudo nginx -t && sudo systemctl reload nginx"
ssh-run "sudo certbot --nginx --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring -d $DOMAIN"
Remove-Item $tmpNginxConf -ErrorAction SilentlyContinue

# ─── Cleanup ─────────────────────────────────────────────────────
Remove-Item $ZIP_PATH -ErrorAction SilentlyContinue
Remove-Item -Recurse $PUBLISH_DIR -ErrorAction SilentlyContinue

Write-Host "`n=== Majak4 API Deployment Complete! ===" -ForegroundColor Green
Write-Host "API : https://$DOMAIN" -ForegroundColor Cyan
Write-Host "SSL : ssh -i $SSH_KEY ${USER}@${SERVER} 'sudo certbot --nginx -d $DOMAIN'" -ForegroundColor Yellow
Write-Host "Logs: ssh -i $SSH_KEY ${USER}@${SERVER} 'sudo journalctl -u $SERVICE -f'" -ForegroundColor Gray

Write-Host "`nService Status:" -ForegroundColor Yellow
ssh-run "sudo systemctl status $SERVICE --no-pager | head -20"
