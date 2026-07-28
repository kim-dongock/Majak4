using System.Text.Json;
using MajakServer.Infrastructure;
using MajakServer.Models.Player;
using StackExchange.Redis;

namespace MajakServer.Services;

/// <summary>
/// Manages channel members in a Redis HASH.
///
/// Redis キー: channel:{chanelId}:members
///   HSET memberNo => JSON { memberNo, nickname, rating, sex, avatarId }
///
/// Falls back to an in-memory dictionary when Redis is unavailable in development.
/// See AP-04 §8.
/// </summary>
public class ChannelMemberService
{
    private static readonly TimeSpan MemberTtl = TimeSpan.FromSeconds(90);

    private readonly RedisService _redis;

    // Fallback when Redis is unavailable: chanelId => (memberNo => JSON).
    private readonly Dictionary<string, Dictionary<string, string>> _fallback = new();

    public ChannelMemberService(RedisService redis) => _redis = redis;

    // Key generation
    private static string Key(string chanelId) => $"channel:{chanelId}:members";

    // Enter
    public async Task EnterAsync(
        string chanelId, string memberNo, string nickname,
        double rating, string sex, string avatarId)
    {
        var json = JsonSerializer.Serialize(new
        {
            memberNo, nickname, rating, sex, avatarId,
        });

        if (_redis.IsAvailable)
        {
            var db = _redis.Db!;
            await db.HashSetAsync(Key(chanelId), memberNo, json);
            await db.KeyExpireAsync(Key(chanelId), MemberTtl);
        }
        else
        {
            if (!_fallback.TryGetValue(chanelId, out var map))
                _fallback[chanelId] = map = new();
            map[memberNo] = json;
        }
    }

    // Leave
    public async Task LeaveAsync(string chanelId, string memberNo)
    {
        if (_redis.IsAvailable)
        {
            var db = _redis.Db!;
            var key = Key(chanelId);
            await db.HashDeleteAsync(key, memberNo);
            if (await db.HashLengthAsync(key) == 0)
                await db.KeyDeleteAsync(key);
            else
                await db.KeyExpireAsync(key, MemberTtl);
        }
        else
        {
            if (_fallback.TryGetValue(chanelId, out var map))
                map.Remove(memberNo);
        }
    }

    // List
    public async Task<IReadOnlyList<ChannelMemberEntry>> GetMembersAsync(string chanelId)
    {
        IEnumerable<KeyValuePair<string, string>> pairs;

        if (_redis.IsAvailable)
        {
            var entries = await _redis.Db!.HashGetAllAsync(Key(chanelId));
            pairs = entries
                .Where(e => e.Value.HasValue)
                .Select(e => new KeyValuePair<string, string>(e.Name!, e.Value!));
        }
        else
        {
            if (!_fallback.TryGetValue(chanelId, out var map))
                return Array.Empty<ChannelMemberEntry>();
            pairs = map;
        }

        var result = new List<ChannelMemberEntry>();
        foreach (var kv in pairs)
        {
            try
            {
                var e = JsonSerializer.Deserialize<ChannelMemberEntry>(kv.Value);
                if (e != null) result.Add(e);
            }
            catch { /* Skip broken entries. */ }
        }
        return result;
    }

    public async Task RefreshTtlBatchAsync(IEnumerable<string> chanelIds)
    {
        if (!_redis.IsAvailable) return;
        var ids = chanelIds.Distinct().ToArray();
        if (ids.Length == 0) return;

        var db = _redis.Db!;
        var batch = db.CreateBatch();
        var tasks = ids.Select(id => batch.KeyExpireAsync(Key(id), MemberTtl)).ToList();
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    public async Task SyncChannelAsync(string chanelId, IEnumerable<MajakPlayer> activePlayers)
    {
        var players = activePlayers
            .Where(player => !string.IsNullOrWhiteSpace(player.MemberNo))
            .GroupBy(player => player.MemberNo)
            .Select(group => group.First())
            .ToList();

        if (_redis.IsAvailable)
        {
            var db = _redis.Db!;
            var key = Key(chanelId);

            if (players.Count == 0)
            {
                await db.KeyDeleteAsync(key);
                return;
            }

            var activeIds = players.Select(player => player.MemberNo).ToHashSet(StringComparer.Ordinal);
            var existing = await db.HashGetAllAsync(key);
            var staleFields = existing
                .Select(entry => entry.Name)
                .Where(name => !activeIds.Contains(name!))
                .ToArray();
            if (staleFields.Length > 0)
                await db.HashDeleteAsync(key, staleFields);

            var entries = players.Select(player => new HashEntry(player.MemberNo, JsonSerializer.Serialize(new
            {
                memberNo = player.Pix,
                pix = player.Pix,
                nickname = player.NickName,
                rating = player.Rating,
                sex = player.Sex,
                avatarId = player.AvatarId,
            }))).ToArray();
            await db.HashSetAsync(key, entries);
            await db.KeyExpireAsync(key, MemberTtl);
            return;
        }

        if (players.Count == 0)
        {
            _fallback.Remove(chanelId);
            return;
        }

        _fallback[chanelId] = players.ToDictionary(
            player => player.MemberNo,
            player => JsonSerializer.Serialize(new
            {
                memberNo = player.Pix,
                pix = player.Pix,
                nickname = player.NickName,
                rating = player.Rating,
                sex = player.Sex,
                avatarId = player.AvatarId,
            }));
    }
}

/// <summary>Channel member state stored in Redis.</summary>
public sealed class ChannelMemberEntry
{
    public string MemberNo { get; set; } = "";
    public string Nickname { get; set; } = "";
    public double Rating   { get; set; }
    public string Sex      { get; set; } = "male";
    public string AvatarId { get; set; } = "";
}
