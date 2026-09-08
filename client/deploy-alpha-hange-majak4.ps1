# Majak4 Hange Alpha Client Deployment
# Usage:
#   .\deploy-alpha-hange-majak4.ps1
#   .\deploy-alpha-hange-majak4.ps1 -CloudFrontId <distribution-id>

param(
    [string]$S3Bucket = "alpha-game-majak4-hange-jp",
    [string]$SiteUrl = "https://alpha-game-majak4.hange.jp",
    [string]$AwsProfile = "hange_majak4",
    [string]$Region = "ap-northeast-1",
    [string]$CloudFrontId = "ER8JDGPEYL97",
    [ValidateSet("auto", "always", "never")]
    [string]$AssetUploadMode = "auto",
    [switch]$SkipCertificateCheck
)

$ErrorActionPreference = "Stop"

$PROJECT_DIR = $PSScriptRoot
$DIST_PATH = Join-Path $PROJECT_DIR "dist"
$ASSETS_PATH = Join-Path $DIST_PATH "assets"
$AWS_COMMON_ARGS = @("--profile", $AwsProfile, "--region", $Region)
if ($SkipCertificateCheck) {
    $AWS_COMMON_ARGS += "--no-verify-ssl"
}

function Assert-LastCommand {
    param([Parameter(Mandatory = $true)][string]$Operation)

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Aws {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Operation
    )

    & aws @Arguments @AWS_COMMON_ARGS
    Assert-LastCommand $Operation
}

Write-Host "=== Majak4 Hange Alpha Client Deployment ===" -ForegroundColor Yellow
Write-Host "Site   : $SiteUrl" -ForegroundColor Cyan
Write-Host "Bucket : s3://$S3Bucket" -ForegroundColor Cyan

# 1. Build with client/.env.alpha.
Write-Host "`n[1/4] Building the Hange Alpha client..." -ForegroundColor Yellow
Push-Location $PROJECT_DIR
try {
    npm run build -- --mode alpha
    Assert-LastCommand "Client build"
} finally {
    Pop-Location
}

if (-not (Test-Path $DIST_PATH)) {
    throw "Build output was not found at '$DIST_PATH'."
}

# 2. Ensure the target bucket exists.
Write-Host "`n[2/4] Checking the target bucket..." -ForegroundColor Yellow
$previousErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = "SilentlyContinue"
    & aws s3api head-bucket --bucket $S3Bucket @AWS_COMMON_ARGS 2>$null
    $bucketExists = $LASTEXITCODE -eq 0
} finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

if (-not $bucketExists) {
    Write-Host "    Creating s3://$S3Bucket in $Region..." -ForegroundColor Yellow
    $createBucketArgs = @("s3api", "create-bucket", "--bucket", $S3Bucket)
    if ($Region -ne "us-east-1") {
        $createBucketArgs += @("--create-bucket-configuration", "LocationConstraint=$Region")
    }
    Invoke-Aws -Arguments $createBucketArgs -Operation "S3 bucket creation"
}

# 3. Upload immutable bundled assets and uncached HTML.
Write-Host "`n[3/4] Uploading the Hange Alpha client..." -ForegroundColor Yellow
Invoke-Aws -Arguments @(
    "s3", "sync", $DIST_PATH, "s3://$S3Bucket",
    "--delete",
    "--exclude", "*.html",
    "--exclude", "assets/*",
    "--cache-control", "public,max-age=31536000,immutable"
) -Operation "Static file upload"

$shouldSyncAssets = $false
switch ($AssetUploadMode) {
    "always" {
        $shouldSyncAssets = $true
    }
    "never" {
        Write-Host "    Bundled asset upload skipped by -AssetUploadMode never." -ForegroundColor DarkYellow
    }
    default {
        if (-not (Test-Path $ASSETS_PATH)) {
            throw "Build output is missing '$ASSETS_PATH'."
        }
        $shouldSyncAssets = $true
    }
}

if ($shouldSyncAssets) {
    Invoke-Aws -Arguments @(
        "s3", "sync", $ASSETS_PATH, "s3://$S3Bucket/assets",
        "--delete",
        "--cache-control", "public,max-age=31536000,immutable"
    ) -Operation "Bundled asset upload"
}

Invoke-Aws -Arguments @(
    "s3", "sync", $DIST_PATH, "s3://$S3Bucket",
    "--delete",
    "--exclude", "*",
    "--include", "*.html",
    "--cache-control", "no-cache,no-store,must-revalidate",
    "--content-type", "text/html; charset=utf-8"
) -Operation "HTML upload"

# 4. Invalidate CloudFront when a distribution is supplied.
Write-Host "`n[4/4] Finalizing deployment..." -ForegroundColor Yellow
if (-not [string]::IsNullOrWhiteSpace($CloudFrontId)) {
    Invoke-Aws -Arguments @(
        "cloudfront", "create-invalidation",
        "--distribution-id", $CloudFrontId,
        "--paths", "/*"
    ) -Operation "CloudFront invalidation"
} else {
    Write-Host "    CloudFront invalidation skipped because no distribution ID was supplied." -ForegroundColor DarkYellow
}

Write-Host "`n=== Hange Alpha Client Deployment Complete ===" -ForegroundColor Green
Write-Host "Client URL: $SiteUrl" -ForegroundColor Cyan
Write-Host "Ensure the CloudFront distribution routes the client origin to s3://$S3Bucket." -ForegroundColor Yellow