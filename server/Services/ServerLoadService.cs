using System.Text.Json;
using MajakServer.Infrastructure;
using StackExchange.Redis;

namespace MajakServer.Services;

/// <summary>
/// ゲームサーバー負荷管理 — Redis で各サーバーのルーム数を管理し、
/// ルーム作成時に最小ルーム数のサーバー URL を返す。
///
/// Redis キー:
///   game:servers          ZSET  member=serverUrl  score=lastSeenUnixTime
///   game:server:roomcounts HASH  field=serverUrl   value=roomCount
///
/// Redis が利用不可の場合は ChannelServerSettings.ServerUrl を返す (フォールバック)。
/// AP-04 §8 参照。
/// </summary>
public class ServerLoadService
{
    private const string ServersKey   = "game:servers";
    private const string RoomCountKey = "game:server:roomcounts";

    // サーバーが最後に報告してから何秒以内なら「生存」とみなすか
    private const int AliveThresholdSeconds = 30;

    private readonly RedisService          _redis;
    private readonly ChannelServerSettings _settings;

    public ServerLoadService(RedisService redis, ChannelServerSettings settings)
    {
        _redis    = redis;
        _settings = settings;
    }

    /// <summary>
    /// このサーバー自身の現在のルーム数を Redis に登録する。
    /// ServerStatusBackgroundService から定期的に呼ばれる。
    /// </summary>
    public async Task RegisterSelfAsync(string serverUrl, int roomCount)
    {
        if (!_redis.IsAvailable) return;

        double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var db = _redis.Db!;

        // ZADD game:servers <now> <serverUrl>
        await db.SortedSetAddAsync(ServersKey, serverUrl, now);

        // HSET game:server:roomcounts <serverUrl> <roomCount>
        await db.HashSetAsync(RoomCountKey, serverUrl, roomCount);

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
    }

    /// <summary>
    /// ルーム数が最小の生存サーバー URL を返す。
    /// Redis が利用不可または生存サーバーなし → ChannelServerSettings.ServerUrl を返す。
    /// </summary>
    public async Task<string> GetBestServerAsync()
    {
        if (!_redis.IsAvailable) return _settings.ServerUrl;

        double now   = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        double since = now - AliveThresholdSeconds;
        var    db    = _redis.Db!;

        // 生存サーバー一覧
        var aliveEntries = await db.SortedSetRangeByScoreAsync(
            ServersKey, since, double.PositiveInfinity);

        if (aliveEntries.Length == 0) return _settings.ServerUrl;

        var serverUrls  = aliveEntries.Select(e => (string)e!).ToArray();

        // ルーム数を一括取得
        var countValues = await db.HashGetAsync(
            RoomCountKey,
            serverUrls.Select(u => (RedisValue)u).ToArray());

        string bestUrl  = serverUrls[0];
        int    bestCnt  = int.MaxValue;

        for (int i = 0; i < serverUrls.Length; i++)
        {
            int cnt = countValues[i].TryParse(out int v) ? v : int.MaxValue;
            if (cnt < bestCnt)
            {
                bestCnt = cnt;
                bestUrl = serverUrls[i];
            }
        }

        return bestUrl;
    }

    // ─── チャンネル動的サーバー割り当て (Redis Lease) ─────────────────────
    //
    // Redis キー:
    //   channel:{chanelId}:server     STRING TTL=60s  このチャンネルを担当するサーバー URL
    //   game:server:channelcounts     HASH   serverUrl → 担当チャンネル数
    //
    // 割り当てアルゴリズム:
    //   1. channel:{id}:server が存在すれば即返す (キャッシュヒット)
    //   2. 存在しなければ alive サーバーのうち channelcount 最小のサーバーを選ぶ
    //   3. SET NX EX 60 でアトミックに書き込む (レースコンディション防止)
    //   4. NX 失敗 (他サーバーが先に書いた) なら GET し直して返す

    private const string ChannelServerKey  = "channel:{0}:server";
    private const string ChannelCountKey   = "game:server:channelcounts";
    private static readonly TimeSpan ChannelLeaseTtl = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 指定チャンネルを担当するサーバー URL を返す。
    /// Redis に割り当て済みなら即返し、なければ動的割り当てを行う。
    /// Redis が利用不可の場合は ChannelServerSettings.ServerUrl を返す。
    /// </summary>
    public async Task<string> ResolveChannelServerAsync(string chanelId)
    {
        if (!_redis.IsAvailable) return _settings.ServerUrl;

        var db  = _redis.Db!;
        var key = string.Format(ChannelServerKey, chanelId);

        // 1. キャッシュヒット
        var cached = await db.StringGetAsync(key);
        if (cached.HasValue) return (string)cached!;

        // 2. alive サーバー一覧を取得
        double now   = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        double since = now - AliveThresholdSeconds;

        var aliveEntries = await db.SortedSetRangeByScoreAsync(
            ServersKey, since, double.PositiveInfinity);

        if (aliveEntries.Length == 0) return _settings.ServerUrl;

        var serverUrls = aliveEntries.Select(e => (string)e!).ToArray();

        // 3. 各サーバーの担当チャンネル数を取得
        var channelCounts = await db.HashGetAsync(
            ChannelCountKey,
            serverUrls.Select(u => (RedisValue)u).ToArray());

        string selectedUrl = serverUrls[0];
        int    minCount    = int.MaxValue;

        for (int i = 0; i < serverUrls.Length; i++)
        {
            int cnt = channelCounts[i].TryParse(out int v) ? v : 0;
            if (cnt < minCount)
            {
                minCount    = cnt;
                selectedUrl = serverUrls[i];
            }
        }

        // 4. SET NX EX 60 (アトミック書き込み)
        bool claimed = await db.StringSetAsync(
            key, selectedUrl, ChannelLeaseTtl, When.NotExists);

        if (!claimed)
        {
            // 他サーバーが先に書いた → 改めて GET
            var winner = await db.StringGetAsync(key);
            return winner.HasValue ? (string)winner! : selectedUrl;
        }

        // 5. 担当チャンネル数をインクリメント
        await db.HashIncrementAsync(ChannelCountKey, selectedUrl, 1);
        return selectedUrl;
    }

    /// <summary>
    /// このサーバーがチャンネルを担当していることを Redis に登録する。
    /// EnterChannelCommand (c1e) から呼ばれる。
    /// 既に自サーバーが登録済みなら TTL を更新するだけ。
    /// </summary>
    public async Task ClaimChannelAsync(string chanelId, string serverUrl)
    {
        if (!_redis.IsAvailable) return;

        var db  = _redis.Db!;
        var key = string.Format(ChannelServerKey, chanelId);

        // SET NX (未登録なら登録)
        bool claimed = await db.StringSetAsync(key, serverUrl, ChannelLeaseTtl, When.NotExists);
        if (claimed)
        {
            await db.HashIncrementAsync(ChannelCountKey, serverUrl, 1);
        }
        else
        {
            // 自サーバー担当なら TTL だけ更新
            var current = await db.StringGetAsync(key);
            if ((string?)current == serverUrl)
            {
                await db.KeyExpireAsync(key, ChannelLeaseTtl);
            }
        }
    }

    /// <summary>
    /// このサーバーが担当するチャンネルの TTL を一括更新する (heartbeat)。
    /// ServerStatusBackgroundService から 8 秒ごとに呼ばれる。
    /// </summary>
    public async Task RefreshChannelLeasesBatchAsync(
        IEnumerable<string> chanelIds, string serverUrl)
    {
        if (!_redis.IsAvailable) return;

        var db   = _redis.Db!;
        var pipe = db.CreateBatch();
        var tasks = new List<Task>();

        foreach (var id in chanelIds)
        {
            var key = string.Format(ChannelServerKey, id);
            tasks.Add(pipe.KeyExpireAsync(key, ChannelLeaseTtl));
        }

        pipe.Execute();
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// グレースフルシャットダウン時にこのサーバーの担当チャンネルを即解放する。
    /// </summary>
    public async Task ReleaseChannelsAsync(IEnumerable<string> chanelIds, string serverUrl)
    {
        if (!_redis.IsAvailable) return;

        var db         = _redis.Db!;
        var idList     = chanelIds.ToList();
        int released   = 0;

        foreach (var id in idList)
        {
            var key     = string.Format(ChannelServerKey, id);
            var current = await db.StringGetAsync(key);
            if ((string?)current == serverUrl)
            {
                await db.KeyDeleteAsync(key);
                released++;
            }
        }

        if (released > 0)
            await db.HashDecrementAsync(ChannelCountKey, serverUrl, released);
    }
}

