using System.Text.Json;
using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using StackExchange.Redis;

namespace MajakServer.Services;

/// <summary>
/// ルーム一覧の Redis 管理サービス — AP-04 §8 参照
///
/// ゴーストルーム防止設計:
///   各ルームエントリに TTL (30 秒) を付与し、
///   ServerStatusBackgroundService が 10 秒ごとに TTL をリフレッシュする。
///   サーバーがクラッシュすると TTL 更新が止まり、最大 30 秒後に自動消滅する。
///   グレースフルシャットダウン時は即座に Redis から削除する。
///
/// Redis キー:
///   room:{roomId}              STRING  JSON  TTL=30s
///   channel:{chanelId}:rooms   SET     roomId の集合
/// </summary>
public class RoomRegistryService
{
    private static readonly TimeSpan RoomTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ChannelRoomsTtl = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ContinueRoomTtl = RoomTtl;

    private readonly RedisService _redis;

    // Redis が利用不可の場合のフォールバック (開発環境)
    // chanelId → (roomId → JSON)
    private readonly Dictionary<string, Dictionary<string, string>> _fallbackRooms = new();
    private readonly Dictionary<string, string> _fallbackContinueRooms = new();

    public RoomRegistryService(RedisService redis) => _redis = redis;

    // ── キー生成 ─────────────────────────────────────────────
    private static string RoomKey(int roomId)     => $"room:{roomId}";
    private static string ChannelKey(string cid)  => $"channel:{cid}:rooms";
    private static string ContinueRoomKey(string memberNo) => $"continue:{memberNo}:room";

    // ── ルーム登録 (CreateRoom 時) ────────────────────────────
    public async Task RegisterRoomAsync(
        int    roomId,   string chanelId, string title,
        bool   isPrivate, int    memberCnt, int memberMax,
        string serverUrl, string roomOption, int maxViewer = 12,
        int roomState = 0, int roomPlaying = 0)
    {
        var entry = new RoomRedisEntry
        {
            RoomId    = roomId,   ChanelId   = chanelId, Title      = title,
            IsPrivate = isPrivate, MemberCnt  = memberCnt, MemberMax = memberMax,
            ServerUrl = serverUrl, RoomOption = roomOption, MaxViewer = maxViewer,
            State = roomState, RoomPlaying = roomPlaying,
        };
        var json = JsonSerializer.Serialize(entry);

        if (_redis.IsAvailable)
        {
            var db = _redis.Db!;
            await db.StringSetAsync(RoomKey(roomId), json, RoomTtl);
            await db.SetAddAsync(ChannelKey(chanelId), roomId.ToString());
            await db.KeyExpireAsync(ChannelKey(chanelId), ChannelRoomsTtl);
        }
        else
        {
            if (!_fallbackRooms.TryGetValue(chanelId, out var map))
                _fallbackRooms[chanelId] = map = new();
            map[roomId.ToString()] = json;
        }
    }

    // ── メンバー数更新 (入退室のたびに呼ぶ) ──────────────────
    public async Task UpdateMemberCountAsync(int roomId, string chanelId, int memberCnt)
    {
        if (_redis.IsAvailable)
        {
            var raw = await _redis.Db!.StringGetAsync(RoomKey(roomId));
            if (!raw.HasValue) return;
            try
            {
                var entry = JsonSerializer.Deserialize<RoomRedisEntry>(raw.ToString())!;
                entry.MemberCnt = memberCnt;
                await _redis.Db!.StringSetAsync(RoomKey(roomId),
                    JsonSerializer.Serialize(entry), RoomTtl);
            }
            catch { /* 壊れたエントリはスキップ */ }
        }
        else
        {
            if (_fallbackRooms.TryGetValue(chanelId, out var map)
                && map.TryGetValue(roomId.ToString(), out var raw2))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<RoomRedisEntry>(raw2)!;
                    entry.MemberCnt = memberCnt;
                    map[roomId.ToString()] = JsonSerializer.Serialize(entry);
                }
                catch { }
            }
        }
    }

    // ── ルーム削除 (全員退室 / グレースフルシャットダウン時) ──
    public async Task RemoveRoomAsync(int roomId, string chanelId)
    {
        if (_redis.IsAvailable)
        {
            var db = _redis.Db!;
            await db.KeyDeleteAsync(RoomKey(roomId));
            await db.SetRemoveAsync(ChannelKey(chanelId), roomId.ToString());
            if (await db.SetLengthAsync(ChannelKey(chanelId)) == 0)
                await db.KeyDeleteAsync(ChannelKey(chanelId));
            else
                await db.KeyExpireAsync(ChannelKey(chanelId), ChannelRoomsTtl);
        }
        else
        {
            if (_fallbackRooms.TryGetValue(chanelId, out var map))
                map.Remove(roomId.ToString());
        }
    }

    public async Task SetContinueRoomAsync(string memberNo, GameRoom room)
    {
        if (string.IsNullOrWhiteSpace(memberNo)) return;
        var roomEntry = await GetRoomEntryAsync(room.RoomId, room.ChannelId)
            ?? new RoomRedisEntry
            {
                RoomId = room.RoomId,
                ChanelId = room.ChannelId,
                Title = room.RoomTitle,
                IsPrivate = room.IsPrivate,
                MemberCnt = room.ActivePlayerCount,
                MemberMax = room.LimitCnt,
                MaxViewer = room.MaxViewer,
                RoomOption = room.RoomOption,
            };
        var entry = new ContinueRoomRedisEntry
        {
            MemberNo = memberNo,
            RoomId = room.RoomId,
            ChanelId = room.ChannelId,
            Title = roomEntry.Title,
            ServerUrl = roomEntry.ServerUrl,
            RoomOption = roomEntry.RoomOption,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var json = JsonSerializer.Serialize(entry);

        if (_redis.IsAvailable)
        {
            await _redis.Db!.StringSetAsync(ContinueRoomKey(memberNo), json, ContinueRoomTtl);
        }
        else
        {
            _fallbackContinueRooms[memberNo] = json;
        }
    }

    public async Task ClearContinueRoomAsync(string memberNo)
    {
        if (string.IsNullOrWhiteSpace(memberNo)) return;
        if (_redis.IsAvailable)
            await _redis.Db!.KeyDeleteAsync(ContinueRoomKey(memberNo));
        else
            _fallbackContinueRooms.Remove(memberNo);
    }

    public async Task<IReadOnlyList<ContinueRoomRedisEntry>> RefreshContinueRoomsAsync(IEnumerable<GameRoom> rooms)
    {
        var entries = new List<ContinueRoomRedisEntry>();
        foreach (var room in rooms)
        {
            if (room.State != GameRoomState.Playing) continue;
            foreach (var seat in room.Seats)
            {
                if (seat?.IsOutPlayer != true) continue;
                await SetContinueRoomAsync(seat.MemberNo, room);
                var entry = await GetContinueRoomAsync(seat.MemberNo);
                if (entry != null) entries.Add(entry);
            }
        }
        return entries;
    }

    public async Task<ContinueRoomRedisEntry?> GetContinueRoomAsync(string memberNo)
    {
        if (string.IsNullOrWhiteSpace(memberNo)) return null;
        string? raw = null;
        if (_redis.IsAvailable)
        {
            var value = await _redis.Db!.StringGetAsync(ContinueRoomKey(memberNo));
            if (!value.HasValue) return null;
            raw = value.ToString();
        }
        else if (!_fallbackContinueRooms.TryGetValue(memberNo, out raw))
        {
            return null;
        }

        ContinueRoomRedisEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<ContinueRoomRedisEntry>(raw);
        }
        catch
        {
            await ClearContinueRoomAsync(memberNo);
            return null;
        }
        if (entry == null)
        {
            await ClearContinueRoomAsync(memberNo);
            return null;
        }

        var room = await GetRoomEntryAsync(entry.RoomId, entry.ChanelId);
        if (room == null)
        {
            await ClearContinueRoomAsync(memberNo);
            return null;
        }
        entry.Title = room.Title;
        entry.ServerUrl = room.ServerUrl;
        entry.RoomOption = room.RoomOption;
        return entry;
    }

    // ── TTL リフレッシュ (ServerStatusBackgroundService が定期呼び出し) ─
    public async Task RefreshTtlAsync(int roomId)
    {
        if (!_redis.IsAvailable) return;
        await _redis.Db!.KeyExpireAsync(RoomKey(roomId), RoomTtl);
    }

    // ── TTL パイプライン一括リフレッシュ (PerformanceAnalysis §2-2)
    // N ルーム分の EXPIRE を 1 往復で送信する。
    // ServerStatusBackgroundService の foreach ループから置き換えて使用する。
    public async Task RefreshTtlBatchAsync(IEnumerable<int> roomIds)
    {
        if (!_redis.IsAvailable) return;
        var db = _redis.Db!;
        var batch = db.CreateBatch();
        var tasks = roomIds.Select(id => batch.KeyExpireAsync(RoomKey(id), RoomTtl)).ToList();
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    public async Task RefreshChannelSetTtlBatchAsync(IEnumerable<string> chanelIds)
    {
        if (!_redis.IsAvailable) return;
        var ids = chanelIds.Distinct().ToArray();
        if (ids.Length == 0) return;

        var db = _redis.Db!;
        var batch = db.CreateBatch();
        var tasks = ids.Select(id => batch.KeyExpireAsync(ChannelKey(id), ChannelRoomsTtl)).ToList();
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    // ── このサーバーの全ルームを一括削除 (シャットダウン時) ──
    public async Task RemoveAllRoomsAsync(IEnumerable<(int roomId, string chanelId)> rooms)
    {
        if (!_redis.IsAvailable) return;
        var db = _redis.Db!;
        foreach (var (roomId, chanelId) in rooms)
        {
            await db.KeyDeleteAsync(RoomKey(roomId));
            await db.SetRemoveAsync(ChannelKey(chanelId), roomId.ToString());
            if (await db.SetLengthAsync(ChannelKey(chanelId)) == 0)
                await db.KeyDeleteAsync(ChannelKey(chanelId));
        }
    }

    // ── チャンネルのルーム一覧取得 ────────────────────────────
    public async Task<IReadOnlyList<RoomRedisEntry>> GetChannelRoomsAsync(string chanelId)
    {
        if (!_redis.IsAvailable)
        {
            if (!_fallbackRooms.TryGetValue(chanelId, out var map))
                return Array.Empty<RoomRedisEntry>();
            return ParseEntries(map.Values);
        }

        var db     = _redis.Db!;
        var roomIds = await db.SetMembersAsync(ChannelKey(chanelId));
        if (roomIds.Length == 0) return Array.Empty<RoomRedisEntry>();

        var keys   = roomIds.Select(id => (RedisKey)RoomKey((int)(long)id)).ToArray();
        var values = await db.StringGetAsync(keys);

        // TTL 切れで消えた roomId は SET から掃除する
        var expired = roomIds
            .Zip(values, (id, v) => (id, v))
            .Where(x => !x.v.HasValue)
            .Select(x => x.id)
            .ToArray();
        if (expired.Length > 0)
            await db.SetRemoveAsync(ChannelKey(chanelId), expired);

        if (await db.SetLengthAsync(ChannelKey(chanelId)) == 0)
            await db.KeyDeleteAsync(ChannelKey(chanelId));
        else
            await db.KeyExpireAsync(ChannelKey(chanelId), ChannelRoomsTtl);

        return ParseEntries(values.Where(v => v.HasValue).Select(v => v.ToString()));
    }

    private static List<RoomRedisEntry> ParseEntries(IEnumerable<string> jsons)
    {
        var result = new List<RoomRedisEntry>();
        foreach (var j in jsons)
        {
            try
            {
                var e = JsonSerializer.Deserialize<RoomRedisEntry>(j);
                if (e != null) result.Add(e);
            }
            catch { }
        }
        return result;
    }

    private async Task<RoomRedisEntry?> GetRoomEntryAsync(int roomId, string chanelId)
    {
        if (_redis.IsAvailable)
        {
            var raw = await _redis.Db!.StringGetAsync(RoomKey(roomId));
            if (!raw.HasValue) return null;
            try { return JsonSerializer.Deserialize<RoomRedisEntry>(raw.ToString()); }
            catch { return null; }
        }

        if (!_fallbackRooms.TryGetValue(chanelId, out var map)
            || !map.TryGetValue(roomId.ToString(), out var fallbackRaw)) return null;
        try { return JsonSerializer.Deserialize<RoomRedisEntry>(fallbackRaw); }
        catch { return null; }
    }
}

/// <summary>Redis に保存するルームエントリ</summary>
public sealed class RoomRedisEntry
{
    public int    RoomId     { get; set; }
    public string ChanelId   { get; set; } = "";
    public string Title      { get; set; } = "";
    public bool   IsPrivate  { get; set; }
    public int    MemberCnt  { get; set; }
    public int    MemberMax  { get; set; }
    public int    MaxViewer  { get; set; } = 12;
    public string ServerUrl  { get; set; } = "";
    public string RoomOption { get; set; } = "";
    public int    State      { get; set; }
    public int    RoomPlaying { get; set; }
}

public sealed class ContinueRoomRedisEntry
{
    public string MemberNo   { get; set; } = "";
    public int    RoomId     { get; set; }
    public string ChanelId   { get; set; } = "";
    public string Title      { get; set; } = "";
    public string ServerUrl  { get; set; } = "";
    public string RoomOption { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
