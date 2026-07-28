namespace MajakServer.Infrastructure;

/// <summary>
/// Redis SETNX を使ったプライマリリーダー選出サービス。
///
/// 仕組み:
///   - 起動時に Redis キー "majak2:primary-leader" を SETNX で取得しようとする。
///   - 取得成功 → このサーバーがプライマリ (IsLeader = true)。
///   - 取得失敗 → すでに別サーバーがプライマリ (IsLeader = false)。
///   - TTL は 30 秒。ServerStatusBackgroundService の 8 秒ごとのループで
///     リーダーが TTL を更新し続ける → サーバーが落ちると最大 30 秒後に他が昇格。
///
/// Redis 未接続 (開発環境など) の場合:
///   - appsettings の IsPrimaryServer フラグにフォールバックする。
/// </summary>
public class PrimaryLeaderService
{
    private const string LeaderKey = "majak2:primary-leader";
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromSeconds(30);

    private readonly RedisService   _redis;
    private readonly string         _serverId;   // このプロセスの識別子 (ServerUrl)
    private readonly bool           _fallback;   // Redis 未接続時のフォールバック値

    private volatile bool _isLeader;

    public bool IsLeader => _isLeader;

    public PrimaryLeaderService(RedisService redis, ChannelServerSettings settings)
    {
        _redis    = redis;
        _serverId = settings.ServerUrl;
        _fallback = settings.IsPrimaryServer;
        _isLeader = _fallback; // 初期値: Redis 接続確認前はフォールバック
    }

    /// <summary>
    /// リーダーロックの取得 / 更新。
    /// ServerStatusBackgroundService の 8 秒ごとのループから呼ぶ。
    /// </summary>
    public async Task TryAcquireOrRenewAsync()
    {
        var db = _redis.Db;
        if (db is null)
        {
            // Redis 未接続: フォールバック値を維持
            _isLeader = _fallback;
            return;
        }

        if (_isLeader)
        {
            // 自分がリーダーのとき: 自分の値のままなら TTL を延長
            var current = await db.StringGetAsync(LeaderKey);
            if (current == _serverId)
            {
                // TTL 更新
                await db.KeyExpireAsync(LeaderKey, LeaseTtl);
            }
            else
            {
                // 別サーバーに奪われた (またはキーが消えて別サーバーが取得済み)
                _isLeader = false;
            }
        }
        else
        {
            // 自分がセカンダリのとき: NX で取得を試みる
            bool acquired = await db.StringSetAsync(
                LeaderKey, _serverId,
                LeaseTtl,
                StackExchange.Redis.When.NotExists);
            if (acquired)
                _isLeader = true;
        }
    }

    /// <summary>
    /// グレースフルシャットダウン時に呼ぶ。
    /// 自分がリーダーなら即座に解放 → 別サーバーが最大 TTL 待たずに昇格できる。
    /// </summary>
    public void Release()
    {
        var db = _redis.Db;
        if (db is null || !_isLeader) return;

        try
        {
            // 自分の値の場合のみ削除 (Lua スクリプトで atomic に)
            var script = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                else
                    return 0
                end";
            db.ScriptEvaluate(script,
                new StackExchange.Redis.RedisKey[]  { LeaderKey },
                new StackExchange.Redis.RedisValue[] { _serverId });
            _isLeader = false;
        }
        catch
        {
            // シャットダウン中のエラーは無視
        }
    }
}
