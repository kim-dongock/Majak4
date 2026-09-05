using System.Text.Json;
using MajakServer.Infrastructure;
using StackExchange.Redis;

namespace MajakServer.Services;

/// <summary>
/// ゲームサーバー実行状態管理 — Redis で各サーバーの heartbeat、ルーム数、
/// チャンネル実行リースを管理する。
///
/// Redis キー:
///   game:servers          ZSET  member=serverUrl  score=lastSeenUnixTime
///   game:server:roomcounts HASH  field=serverUrl   value=roomCount
///
/// チャンネルの割り当て先は channel_master.server_url を正本とし、このサービスでは選択しない。
/// AP-04 §8 参照。
/// </summary>
public class ServerLoadService
{
    private const string ClaimChannelLeaseScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then
            redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
            return 1
        end
        if current == ARGV[1] then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            return 1
        end
        return 0
        """;

    private const string RenewChannelLeaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            return 1
        end
        return 0
        """;

    private const string ReleaseChannelLeaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            redis.call('DEL', KEYS[1])
            return 1
        end
        return 0
        """;

    private const string ResetChannelLeaseScript = """
        return redis.call('DEL', KEYS[1])
        """;

    private const string ServersKey   = "game:servers";
    private const string RoomCountKey = "game:server:roomcounts";

    // サーバーが最後に報告してから何秒以内なら「生存」とみなすか
    private const int AliveThresholdSeconds = 30;

    private readonly RedisService _redis;
    private readonly ILogger<ServerLoadService>? _logger;
    private int _unavailableLogged;
    private int _registrationLogged;

    public ServerLoadService(RedisService redis, ILogger<ServerLoadService>? logger = null)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// このサーバー自身の現在のルーム数を Redis に登録する。
    /// ServerStatusBackgroundService から定期的に呼ばれる。
    /// </summary>
    public async Task RegisterSelfAsync(string serverUrl, int roomCount)
    {
        if (!_redis.IsAvailable)
        {
            if (Interlocked.Exchange(ref _unavailableLogged, 1) == 0)
                _logger?.LogWarning(
                    "Server heartbeat skipped because Redis is unavailable. serverUrl={ServerUrl}",
                    serverUrl);
            return;
        }

        if (Interlocked.Exchange(ref _unavailableLogged, 0) == 1)
            _logger?.LogInformation(
                "Server heartbeat resumed after Redis became available. serverUrl={ServerUrl}",
                serverUrl);

        double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var db = _redis.Db!;

        // ZADD game:servers <now> <serverUrl>
        await db.SortedSetAddAsync(ServersKey, serverUrl, now);

        // HSET game:server:roomcounts <serverUrl> <roomCount>
        await db.HashSetAsync(RoomCountKey, serverUrl, roomCount);

        if (Interlocked.Exchange(ref _registrationLogged, 1) == 0)
            _logger?.LogInformation(
                "Server heartbeat registered in Redis. serverUrl={ServerUrl} roomCount={RoomCount} timestamp={Timestamp}",
                serverUrl,
                roomCount,
                now);
        else
            _logger?.LogDebug(
                "Server heartbeat refreshed in Redis. serverUrl={ServerUrl} roomCount={RoomCount} timestamp={Timestamp}",
                serverUrl,
                roomCount,
                now);

        // 古いエントリを掃除 (AliveThreshold の 2 倍以上古いもの)
        var staleServers = await db.SortedSetRangeByScoreAsync(
            ServersKey, double.NegativeInfinity, now - AliveThresholdSeconds * 2);
        await db.SortedSetRemoveRangeByScoreAsync(
            ServersKey, double.NegativeInfinity, now - AliveThresholdSeconds * 2);
        if (staleServers.Length > 0)
        {
            var fields = staleServers.Select(x => (RedisValue)x!).ToArray();
            await db.HashDeleteAsync(RoomCountKey, fields);
            await db.HashDeleteAsync(ChannelCountKey, fields);
        }
    }

    /// <summary>
    /// グレースフルシャットダウン時に呼ぶ。
    /// Redis からこのサーバーのエントリを即座に削除する。
    /// </summary>
    public async Task UnregisterSelfAsync(string serverUrl)
    {
        if (!_redis.IsAvailable) return;
        var db = _redis.Db!;
        await db.SortedSetRemoveAsync(ServersKey, serverUrl);
        await db.HashDeleteAsync(RoomCountKey, serverUrl);
        await db.HashDeleteAsync(ChannelCountKey, serverUrl);
    }

    public async Task<bool> IsServerActiveAsync(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            _logger?.LogWarning("Server activity check rejected an empty server URL.");
            return false;
        }
        if (!_redis.IsAvailable)
        {
            _logger?.LogWarning(
                "Server activity check failed because Redis is unavailable. serverUrl={ServerUrl}",
                serverUrl);
            return false;
        }

        var lastSeen = await _redis.Db!.SortedSetScoreAsync(ServersKey, serverUrl);
        var threshold = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - AliveThresholdSeconds;
        var isActive = lastSeen.HasValue && lastSeen.Value >= threshold;
        if (!isActive)
            _logger?.LogWarning(
                "Server is inactive in Redis. serverUrl={ServerUrl} lastSeen={LastSeen} threshold={Threshold}",
                serverUrl,
                lastSeen,
                threshold);
        else
            _logger?.LogDebug(
                "Server is active in Redis. serverUrl={ServerUrl} lastSeen={LastSeen}",
                serverUrl,
                lastSeen);
        return isActive;
    }

    public async Task ResetChannelLeaseAsync(string chanelId)
    {
        if (!_redis.IsAvailable || string.IsNullOrWhiteSpace(chanelId)) return;

        await _redis.Db!.ScriptEvaluateAsync(
            ResetChannelLeaseScript,
            new RedisKey[] { string.Format(ChannelServerKey, chanelId) });
    }

    // ─── DB 割り当て済みチャンネルの実行リース ───────────────────────────
    //
    // Redis キー:
    //   channel:{chanelId}:server     STRING TTL=60s  このチャンネルを担当するサーバー URL
    //   game:server:channelcounts     HASH   serverUrl → 担当チャンネル数
    //
    // channel_master.server_url と一致するサーバーだけが、入場時にこのリースを取得する。

    private const string ChannelServerKey  = "channel:{0}:server";
    private const string ChannelCountKey   = "game:server:channelcounts";
    private static readonly TimeSpan ChannelLeaseTtl = TimeSpan.FromSeconds(60);

    /// <summary>
    /// このサーバーがチャンネルを担当していることを Redis に登録する。
    /// EnterChannelCommand (c1e) から呼ばれる。
    /// 既に自サーバーが登録済みなら TTL を更新するだけ。
    /// </summary>
    public async Task<bool> ClaimChannelAsync(string chanelId, string serverUrl)
    {
        if (!_redis.IsAvailable) return true;

        var db  = _redis.Db!;
        var key = string.Format(ChannelServerKey, chanelId);

        return await ClaimChannelLeaseAsync(db, key, serverUrl);
    }

    /// <summary>
    /// このサーバーが担当するチャンネルの TTL を一括更新する (heartbeat)。
    /// ServerStatusBackgroundService から 8 秒ごとに呼ばれる。
    /// </summary>
    public async Task<IReadOnlySet<string>> RefreshChannelLeasesBatchAsync(
        IEnumerable<string> chanelIds, string serverUrl)
    {
        var ids = chanelIds.Distinct(StringComparer.Ordinal).ToArray();
        if (!_redis.IsAvailable) return ids.ToHashSet(StringComparer.Ordinal);

        var db = _redis.Db!;
        var renewals = await Task.WhenAll(ids.Select(async id => new
        {
            Id = id,
            Renewed = await ClaimChannelLeaseAsync(
                db, string.Format(ChannelServerKey, id), serverUrl),
        }));
        var owned = renewals
            .Where(result => result.Renewed)
            .Select(result => result.Id)
            .ToHashSet(StringComparer.Ordinal);
        await db.HashSetAsync(ChannelCountKey, serverUrl, owned.Count);
        return owned;
    }

    private static async Task<bool> RenewChannelLeaseIfOwnedAsync(
        IDatabase db, string key, string serverUrl)
    {
        long renewed = (long)await db.ScriptEvaluateAsync(
            RenewChannelLeaseScript,
            new RedisKey[] { key },
            new RedisValue[] { serverUrl, (long)ChannelLeaseTtl.TotalMilliseconds });
        return renewed == 1;
    }

    /// <summary>
    /// グレースフルシャットダウン時にこのサーバーの担当チャンネルを即解放する。
    /// </summary>
    public async Task ReleaseChannelsAsync(IEnumerable<string> chanelIds, string serverUrl)
    {
        if (!_redis.IsAvailable) return;

        var db         = _redis.Db!;
        foreach (var id in chanelIds.Distinct(StringComparer.Ordinal))
        {
            await db.ScriptEvaluateAsync(
                ReleaseChannelLeaseScript,
                new RedisKey[] { string.Format(ChannelServerKey, id) },
                new RedisValue[] { serverUrl });
        }
        await db.HashSetAsync(ChannelCountKey, serverUrl, 0);
    }

    private static async Task<bool> ClaimChannelLeaseAsync(
        IDatabase db, string key, string serverUrl)
    {
        long claimed = (long)await db.ScriptEvaluateAsync(
            ClaimChannelLeaseScript,
            new RedisKey[] { key },
            new RedisValue[] { serverUrl, (long)ChannelLeaseTtl.TotalMilliseconds });
        return claimed == 1;
    }
}

