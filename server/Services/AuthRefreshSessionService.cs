using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MajakServer.Infrastructure;
using StackExchange.Redis;

namespace MajakServer.Services;

public sealed class AuthRefreshSessionService
{
    public const string CookieName = "majak2_refresh";
    private const string KeyPrefix = "auth:refresh:";
    private readonly RedisService _redis;
    private readonly TimeSpan _ttl;

    public AuthRefreshSessionService(RedisService redis, IConfiguration config)
    {
        _redis = redis;
        var days = config.GetValue<int?>("AuthRefresh:TokenDays") ?? 30;
        _ttl = TimeSpan.FromDays(Math.Clamp(days, 1, 365));
    }

    public TimeSpan Ttl => _ttl;

    public async Task<string?> IssueAsync(string memberNo, HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(memberNo) || !_redis.IsAvailable) return null;

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var session = new RefreshSession(
            memberNo,
            now,
            now.Add(_ttl),
            HashText(ctx.Request.Headers.UserAgent.ToString()),
            IpPrefix(ctx.Connection.RemoteIpAddress?.ToString() ?? string.Empty));

        var json = JsonSerializer.Serialize(session);
        var ok = await (_redis.Db?.StringSetAsync(Key(HashToken(token)), json, _ttl, When.NotExists)
            ?? Task.FromResult(false));
        return ok ? token : null;
    }

    public async Task<string?> ValidateAsync(string? token, HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(token) || !_redis.IsAvailable) return null;

        var raw = await (_redis.Db?.StringGetAsync(Key(HashToken(token)))
            ?? Task.FromResult(RedisValue.Null));
        if (raw.IsNullOrEmpty) return null;

        RefreshSession? session;
        try
        {
            session = JsonSerializer.Deserialize<RefreshSession>(raw!);
        }
        catch
        {
            return null;
        }

        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow) return null;
        return session.MemberNo;
    }

    public async Task RevokeAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_redis.IsAvailable) return;
        if (_redis.Db is not { } db) return;
        await db.KeyDeleteAsync(Key(HashToken(token)));
    }

    private static string Key(string tokenHash) => KeyPrefix + tokenHash;

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string HashText(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string IpPrefix(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return string.Empty;
        var parts = ip.Split('.');
        return parts.Length == 4 ? string.Join('.', parts.Take(3)) : ip;
    }

    private sealed record RefreshSession(
        string MemberNo,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt,
        string UserAgentHash,
        string IpPrefix);
}
