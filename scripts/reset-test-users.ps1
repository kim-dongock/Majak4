<#
.SYNOPSIS
Resets all registered player data to the post-registration baseline.

.DESCRIPTION
Preserves player_account, admin_account, game_admin_member, and all master data.
Deletes gameplay state, owned items, purchase orders, and player logs. MP is reset
to zero so an administrator can grant fresh free MP through /api/admin/cash/adjust.

Stop every MajakServer instance before executing this script.

.EXAMPLE
.\scripts\reset-test-users.ps1 -ConfirmReset RESET_ALL_PLAYERS

.EXAMPLE
.\scripts\reset-test-users.ps1 -ConfirmReset RESET_ALL_PLAYERS `
  -GameConnectionString $env:MAJAK_GAME_DB `
  -LogConnectionString $env:MAJAK_LOG_DB `
  -RedisConnectionString 'localhost:6379,password=your-password'
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$GameConnectionString,
    [string]$LogConnectionString,
    [string]$RedisConnectionString,
    [string]$SettingsPath = (Join-Path $PSScriptRoot '..\server\appsettings.json'),
    [ValidateSet('RESET_ALL_PLAYERS')]
    [string]$ConfirmReset,
    [switch]$AllowActiveServer,
    [switch]$DeletePaifuObjects,
    [string]$PaifuBucket,
    [string]$PaifuPrefix = 'paifu'
)

$ErrorActionPreference = 'Stop'

if ($ConfirmReset -ne 'RESET_ALL_PLAYERS') {
    throw 'Refusing to reset data. Pass -ConfirmReset RESET_ALL_PLAYERS exactly.'
}

if ((Get-Process -Name MajakServer -ErrorAction SilentlyContinue) -and -not $AllowActiveServer) {
    throw 'MajakServer is running. Stop all game servers before resetting data.'
}

function Read-Settings([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Settings file not found: $Path. Supply both database connection strings."
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-ConnectionOption([System.Data.Common.DbConnectionStringBuilder]$Builder, [string[]]$Names) {
    foreach ($name in $Names) {
        if ($Builder.ContainsKey($name) -and -not [string]::IsNullOrWhiteSpace([string]$Builder[$name])) {
            return [string]$Builder[$name]
        }
    }
    return $null
}

function New-MySqlDefaultsFile([string]$ConnectionString) {
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder.ConnectionString = $ConnectionString
    $dbServer = Get-ConnectionOption $builder @('Server', 'Host', 'Data Source')
    $port = Get-ConnectionOption $builder @('Port')
    $user = Get-ConnectionOption $builder @('User ID', 'Uid', 'User', 'Username')
    $password = Get-ConnectionOption $builder @('Password', 'Pwd')
    $database = Get-ConnectionOption $builder @('Database', 'Initial Catalog')
    if ([string]::IsNullOrWhiteSpace($dbServer) -or [string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($database)) {
        throw 'Connection strings must contain Server/Host, User ID/Uid, and Database.'
    }
    if ([string]::IsNullOrWhiteSpace($port)) { $port = '3306' }

    $path = Join-Path $env:TEMP ("majak4-reset-{0}.cnf" -f [Guid]::NewGuid().ToString('N'))
    $lines = @('[client]', "host=$dbServer", "port=$port", "user=$user", "password=$password", "database=$database", 'default-character-set=utf8mb4')
    [System.IO.File]::WriteAllLines($path, $lines, [System.Text.UTF8Encoding]::new($false))
    return $path
}

function Invoke-MySqlSql([string]$MySql, [string]$DefaultsFile, [string]$Sql) {
    $Sql | & $MySql "--defaults-extra-file=$DefaultsFile" '--batch' '--skip-column-names'
    if ($LASTEXITCODE -ne 0) { throw "MySQL failed with exit code $LASTEXITCODE." }
}

function Remove-RedisTestState([string]$ConnectionString) {
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        Write-Warning 'Redis connection string was not supplied; Redis state was not cleared.'
        return
    }
    $redis = Get-Command redis-cli -ErrorAction SilentlyContinue
    if ($null -eq $redis) {
        Write-Warning 'redis-cli was not found; Redis state was not cleared.'
        return
    }

    $parts = $ConnectionString.Split(',')
    $endpoint = $parts[0]
    $host, $port = $endpoint -split ':', 2
    if ([string]::IsNullOrWhiteSpace($port)) { $port = '6379' }
    $password = ($parts | Where-Object { $_ -match '^password=' } | Select-Object -First 1) -replace '^password=', ''
    $previousAuth = $env:REDISCLI_AUTH
    try {
        if (-not [string]::IsNullOrWhiteSpace($password)) { $env:REDISCLI_AUTH = $password }
        foreach ($pattern in @('auth:refresh:*', 'channel:*:members', 'channel:*:rooms', 'channel:*:server', 'room:*', 'continue:*:room', 'game:servers', 'game:server:roomcounts')) {
            $keys = & $redis.Source -h $host -p $port --scan --pattern $pattern
            if ($LASTEXITCODE -ne 0) { throw "Redis scan failed for $pattern." }
            foreach ($key in $keys) {
                if ($key) { & $redis.Source -h $host -p $port DEL $key | Out-Null }
            }
        }
    }
    finally {
        $env:REDISCLI_AUTH = $previousAuth
    }
}

$settings = $null
if ([string]::IsNullOrWhiteSpace($GameConnectionString) -or [string]::IsNullOrWhiteSpace($LogConnectionString) -or [string]::IsNullOrWhiteSpace($RedisConnectionString)) {
    $settings = Read-Settings $SettingsPath
}
if ([string]::IsNullOrWhiteSpace($GameConnectionString)) { $GameConnectionString = $settings.ConnectionStrings.GameDatabase }
if ([string]::IsNullOrWhiteSpace($LogConnectionString)) { $LogConnectionString = $settings.ConnectionStrings.LogDatabase }
if ([string]::IsNullOrWhiteSpace($RedisConnectionString)) { $RedisConnectionString = $settings.Redis.ConnectionString }
if ([string]::IsNullOrWhiteSpace($GameConnectionString) -or [string]::IsNullOrWhiteSpace($LogConnectionString)) {
    throw 'GameConnectionString and LogConnectionString are required.'
}

$mysql = Get-Command mysql -ErrorAction SilentlyContinue
if ($null -eq $mysql) { throw 'MySQL CLI (mysql) was not found on PATH.' }

$gameSql = @'
START TRANSACTION;
SET @initial_gp := COALESCE((SELECT initial_gp FROM game_economy_policy WHERE policy_id = 1), 1000);

-- Keep player_account registration details and all administrator/master data.
UPDATE player_wallet
SET game_money = @initial_gp, pending_game_money = 0, earned_game_money = 0,
    loaned_game_money = 0, gem_count = 0, cash_count = 0, paid_cash_count = 0,
    free_cash_count = 0, row_version = 0, updated_at = CURRENT_TIMESTAMP(3);
UPDATE player_profile
SET common_rating = 1400, experience = 0, best_money_level = 2,
    consecutive_win_loss = 0, all_in_count = 0, last_all_in_at = NULL,
    trick_title_code = NULL, majak_title_code = NULL, event_open_flag = NULL,
    weekly_point = 0, weekly_target_date = CURRENT_DATE, last_played_at = NULL,
    row_version = 0, updated_at = CURRENT_TIMESTAMP(3);

DELETE FROM player_mode_stats;
DELETE FROM player_high_class_summary;
DELETE FROM player_high_class_yaku;
DELETE FROM player_daily_mission;
DELETE FROM player_weekly_reward;
DELETE FROM player_title;
DELETE FROM player_function_item;
DELETE FROM player_custom_item;
DELETE FROM player_present;
DELETE FROM player_grade_rank;
DELETE FROM player_yaku_stats;
DELETE FROM cup_player_rating;
DELETE FROM tournament_player_rating;
DELETE FROM tournament_participant;
DELETE FROM tournament_room;
DELETE FROM tournament_session;
DELETE FROM event_user;
DELETE FROM player_avatar_inventory;
DELETE FROM player_daily_mission_history;
DELETE FROM player_skin;
DELETE FROM player_shop;
DELETE FROM serial_exchange_item;
DELETE FROM channel_runtime;
DELETE FROM grade_rank_schedule;
DELETE FROM cash_charge_order;

-- serial_coupon is master data; reset only its claimed-member association.
UPDATE serial_coupon SET member_no = NULL, updated_at = CURRENT_TIMESTAMP(3);
UPDATE game_clear_count
SET clear_count = 0, admin_no = NULL, count_status = 0, updated_at = CURRENT_TIMESTAMP(3);
COMMIT;
'@

$logSql = @'
START TRANSACTION;
DELETE FROM paifu_archive_member_log;
DELETE FROM paifu_archive_log;
DELETE FROM game_player_result_log;
DELETE FROM game_session_log;
DELETE FROM training_player_result_log;
DELETE FROM training_session_log;
DELETE FROM weekly_reward_claim_log;
DELETE FROM money_transaction_log;
DELETE FROM winning_yaku_log;
DELETE FROM item_purchase_log;
DELETE FROM cash_transaction_log;
DELETE FROM player_login_log;
DELETE FROM daily_mission_completion_log;
DELETE FROM custom_item_purchase_log;
DELETE FROM present_delivery_log;
DELETE FROM grade_rank_snapshot_log;
DELETE FROM cup_match_log;

-- Restore the post-registration GP grant history after resetting each wallet.
INSERT INTO money_transaction_log
    (occurred_at, member_no, event_code, event_title, game_id, amount,
     balance_before, balance_after, is_valid, remote_address)
SELECT CURRENT_TIMESTAMP(3), member_no, 'JM00068', '初期GP支給', 'MAJAK4', game_money,
       0, game_money, TRUE, ''
FROM player_wallet;
-- Preserve admin_operation_log as the administrator security audit trail.
COMMIT;
'@

$gameDefaults = New-MySqlDefaultsFile $GameConnectionString
$logDefaults = New-MySqlDefaultsFile $LogConnectionString
try {
    $previewSql = "SELECT 'players', COUNT(*) FROM player_account UNION ALL SELECT 'wallets', COUNT(*) FROM player_wallet UNION ALL SELECT 'mode_stats', COUNT(*) FROM player_mode_stats UNION ALL SELECT 'cash_orders', COUNT(*) FROM cash_charge_order;"
    Write-Host 'Reset preview (game database):' -ForegroundColor Yellow
    Invoke-MySqlSql $mysql.Source $gameDefaults $previewSql

    if ($PSCmdlet.ShouldProcess('all player game state and player logs', 'Reset to post-registration baseline')) {
        Invoke-MySqlSql $mysql.Source $gameDefaults $gameSql
        Invoke-MySqlSql $mysql.Source $logDefaults $logSql
        Remove-RedisTestState $RedisConnectionString

        if ($DeletePaifuObjects) {
            if ([string]::IsNullOrWhiteSpace($PaifuBucket)) { throw 'PaifuBucket is required with -DeletePaifuObjects.' }
            $aws = Get-Command aws -ErrorAction SilentlyContinue
            if ($null -eq $aws) { throw 'AWS CLI is required with -DeletePaifuObjects.' }
            & $aws.Source s3 rm "s3://$PaifuBucket/$($PaifuPrefix.Trim('/'))/" --recursive
            if ($LASTEXITCODE -ne 0) { throw 'S3 paifu object deletion failed.' }
        }

        Write-Host 'Reset complete. Every wallet was restored to initial GP and an initial-GP history row was recreated. Every MP balance is 0; grant MP next through the admin adjustment page.' -ForegroundColor Green
    }
}
finally {
    Remove-Item -LiteralPath $gameDefaults, $logDefaults -Force -ErrorAction SilentlyContinue
}