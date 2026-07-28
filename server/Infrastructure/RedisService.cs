using System.Text.Json;
using StackExchange.Redis;

namespace MajakServer.Infrastructure;

/// <summary>
/// Redis 接続ラッパー — StackExchange.Redis の IConnectionMultiplexer をシングルトンで保持する。
/// appsettings の "Redis:ConnectionString" を使用する。
/// 接続失敗時は null Db を返すため、Redis が起動していない開発環境でも起動できる。
/// </summary>
public class RedisService
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = null, IncludeFields = false };

    private readonly IConnectionMultiplexer? _mux;

    public RedisService(IConfiguration config)
    {
        var cs = config["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(cs)) return;

        try
        {
            _mux = ConnectionMultiplexer.Connect(cs);
        }
        catch
        {
            // Redis が起動していない場合はスキップ (開発環境)
        }
    }

    public bool IsAvailable => _mux is { IsConnected: true };

    public IDatabase? Db => _mux?.GetDatabase();

    // ── JSON キャッシュヘルパー ──────────────────────────────────────────

    /// <summary>キーに対応する JSON 値をデシリアライズして返す。未存在 / Redis 未接続なら default。</summary>
    public async Task<T?> GetJsonAsync<T>(string key)
    {
        var db = Db;
        if (db is null) return default;
        try
        {
            var raw = await db.StringGetAsync(key);
            if (raw.IsNullOrEmpty) return default;
            return JsonSerializer.Deserialize<T>(raw!, JsonOpts);
        }
        catch { return default; }
    }

    /// <summary>値を JSON 文字列にシリアライズして Redis に格納する。Redis 未接続なら何もしない。</summary>
    public async Task SetJsonAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var db = Db;
        if (db is null) return;
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOpts);
            await db.StringSetAsync(key, json, ttl);
        }
        catch { /* Redis 書き込みエラーは無視 */ }
    }

    /// <summary>キーを削除する (キャッシュ無効化)。Redis 未接続なら何もしない。</summary>
    public async Task InvalidateAsync(string key)
    {
        var db = Db;
        if (db is null) return;
        try { await db.KeyDeleteAsync(key); }
        catch { }
    }

    /// <summary>プレフィックスに一致するキーをすべて削除する (SCAN 使用)。</summary>
    public async Task InvalidatePrefixAsync(string prefix)
    {
        var db = Db;
        if (db is null || _mux is null) return;
        try
        {
            var server = _mux.GetServer(_mux.GetEndPoints()[0]);
            await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
                await db.KeyDeleteAsync(key);
        }
        catch { }
    }
}
