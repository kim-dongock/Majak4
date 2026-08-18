param(
    [Parameter(Mandatory = $true)]
    [string]$Bucket,

    [Parameter(Mandatory = $true)]
    [string]$AllowedOrigin,

    [ValidateRange(1, 365)]
    [int]$RetentionDays = 90,

    [string]$Prefix = "paifu"
)

$ErrorActionPreference = "Stop"
$aws = Get-Command aws -ErrorAction SilentlyContinue
if ($null -eq $aws) {
    throw "AWS CLI is required. Install and authenticate the AWS CLI before running this script."
}

$normalizedPrefix = $Prefix.Trim('/')
$lifecycle = @{
    Rules = @(
        @{
            ID = "majak4-paifu-retention"
            Status = "Enabled"
            Filter = @{ Prefix = "$normalizedPrefix/" }
            Expiration = @{ Days = $RetentionDays }
        }
    )
} | ConvertTo-Json -Depth 6

$cors = @{
    CORSRules = @(
        @{
            AllowedHeaders = @("*")
            AllowedMethods = @("GET")
            AllowedOrigins = @($AllowedOrigin)
            ExposeHeaders = @("Content-Encoding", "Content-Type")
            MaxAgeSeconds = 300
        }
    )
} | ConvertTo-Json -Depth 6

$lifecyclePath = Join-Path $env:TEMP "majak4-paifu-lifecycle.json"
$corsPath = Join-Path $env:TEMP "majak4-paifu-cors.json"
try {
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($lifecyclePath, $lifecycle, $utf8WithoutBom)
    [System.IO.File]::WriteAllText($corsPath, $cors, $utf8WithoutBom)
    & aws s3api put-bucket-lifecycle-configuration --bucket $Bucket --lifecycle-configuration "file://$lifecyclePath"
    & aws s3api put-bucket-cors --bucket $Bucket --cors-configuration "file://$corsPath"
}
finally {
    Remove-Item $lifecyclePath, $corsPath -Force -ErrorAction SilentlyContinue
}
