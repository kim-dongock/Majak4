# Majak4 Frontend Deployment
# Usage:
#   .\deploy-front-majak4.ps1 [-CloudFrontId <distribution-id>]
# Prerequisites: AWS CLI configured (aws configure)

param(
    [string]$S3Bucket = "majak4-front",
    [string]$CloudFrontId = "ES3BNFE78O2FR"
)

$ErrorActionPreference = "Stop"

$PROJECT_DIR   = $PSScriptRoot
$S3_BUCKET     = $S3Bucket
$CLOUDFRONT_ID = $CloudFrontId
$VITE_SERVICE  = "majak4"
$SITE_URL      = "https://majak4.studio35app.net"
$API_URL       = "https://app-majak4.studio35app.net"
$S3_REGION     = "ap-northeast-1"
$DIST_PATH     = Join-Path $PROJECT_DIR "dist"

Write-Host "=== Majak4 Frontend Deployment ===" -ForegroundColor Yellow

# ─── 1. Build ──────────────────────────────────────────────
Write-Host "`n[1/3] Building for '$VITE_SERVICE' service..." -ForegroundColor Yellow
$env:VITE_SERVICE_ID = $VITE_SERVICE
$env:VITE_API_BASE_URL = $API_URL
Push-Location $PROJECT_DIR
try {
    npm run build
} finally {
    Pop-Location
}

if (-not (Test-Path $DIST_PATH)) {
    Write-Host "Build failed — dist folder not found." -ForegroundColor Red
    exit 1
}

# ─── 2. S3 Sync ──────────────────────────────────────────────────
Write-Host "`n[2/3] Syncing to s3://$S3_BUCKET ..." -ForegroundColor Yellow

aws s3 sync $DIST_PATH "s3://$S3_BUCKET" `
    --region $S3_REGION `
    --delete `
    --exclude "*.html" `
    --cache-control "public,max-age=31536000,immutable"

aws s3 sync $DIST_PATH "s3://$S3_BUCKET" `
    --region $S3_REGION `
    --delete `
    --exclude "*" `
    --include "*.html" `
    --cache-control "no-cache,no-store,must-revalidate" `
    --content-type "text/html; charset=utf-8"

# ─── 3. S3 Static Website 설정 ───────────────────────────────────
Write-Host "`n[3/3] Configuring S3 static website hosting..." -ForegroundColor Yellow
aws s3api put-bucket-website `
    --bucket $S3_BUCKET `
    --region $S3_REGION `
    --website-configuration '{\"IndexDocument\":{\"Suffix\":\"index.html\"},\"ErrorDocument\":{\"Key\":\"index.html\"}}'

# ─── CloudFront 캐시 무효화 ────────────────────────────────────────
if ($CLOUDFRONT_ID -ne "") {
    Write-Host "`n[+] Invalidating CloudFront cache..." -ForegroundColor Yellow
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    aws cloudfront create-invalidation `
        --distribution-id $CLOUDFRONT_ID `
        --paths "/*" 2>&1
    $ErrorActionPreference = $prev
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    Cache invalidated." -ForegroundColor Green
    } else {
        Write-Host "    CloudFront invalidation skipped (insufficient IAM permissions)." -ForegroundColor DarkYellow
        Write-Host "    Invalidate manually: aws cloudfront create-invalidation --distribution-id $CLOUDFRONT_ID --paths '/*'" -ForegroundColor Gray
    }
}

Write-Host "`n=== Majak4 Frontend Deployment Complete! ===" -ForegroundColor Green
Write-Host "Frontend URL : $SITE_URL" -ForegroundColor Cyan
Write-Host "`nNote: Set the following S3 bucket policy (if not already done):" -ForegroundColor Yellow
Write-Host '  { "Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"s3:GetObject","Resource":"arn:aws:s3:::'"$S3_BUCKET"'/*"}] }' -ForegroundColor Gray
