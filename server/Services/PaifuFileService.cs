using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL.Entities;

namespace MajakServer.Services;

public sealed class PaifuFileService
{
    private const int MaxCompressedBytes = 8 * 1024 * 1024;
    private const int MaxPlaintextBytes = 16 * 1024 * 1024;
    private const int MaxPackets = 3_000;
    private readonly LogDataContextFactory _db;
    private readonly PaifuObjectStore _objects;
    private readonly int _retentionDays;

    public PaifuFileService(LogDataContextFactory db, PaifuObjectStore objects, IConfiguration configuration)
    {
        _db = db;
        _objects = objects;
        _retentionDays = Math.Clamp(configuration.GetValue("Paifu:RetentionDays", 90), 1, 365);
    }

    public static PaifuArchiveWorkItem CreateCompletedGameWorkItem(
        GameRoom room,
        IReadOnlyList<object> packets,
        IReadOnlyList<MajakPlayer> players,
        IReadOnlyDictionary<string, object?>? report)
        => new(
            DateTimeOffset.UtcNow,
            room.ChannelId,
            room.RoomId,
            room.RoomTitle,
            room.RoomOption,
            packets.Select(ToJsonElement).ToArray(),
            players.Select(player => new PaifuArchiveParticipant(player.MemberNo, player.NickName, player.SLevel, player.Rating)).ToArray(),
            ReadResult(report));

    public Task StoreCompletedGameAsync(GameRoom room, IReadOnlyList<object> packets, IReadOnlyList<MajakPlayer> players, IReadOnlyDictionary<string, object?>? report)
        => StoreCompletedGameAsync(CreateCompletedGameWorkItem(room, packets, players, report));

    public async Task StoreCompletedGameAsync(PaifuArchiveWorkItem item, CancellationToken cancellationToken = default)
    {
        if (!_objects.IsConfigured || item.Packets.Count == 0 || item.Members.Count == 0) return;
        var payload = new PaifuPayload(
            Version: 1,
            PlayedAt: item.PlayedAt,
            ChannelId: item.ChannelId,
            RoomId: item.RoomId,
            RoomName: item.RoomName,
            RoomOption: item.RoomOption,
            Packets: item.Packets,
            Members: item.Members.Select(member => new PaifuMember(member.Name, member.Title, member.Rating, "")).ToArray(),
            Result: item.Result);
        var plain = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (plain.Length > MaxPlaintextBytes || item.Packets.Count > MaxPackets) return;
        var compressed = Compress(plain);
        if (compressed.Length > MaxCompressedBytes) return;

        var members = item.Members.Where(member => ulong.TryParse(member.MemberNo, out _)).ToArray();
        if (members.Length == 0) return;
        var objectKey = _objects.CreateObjectKey(payload.PlayedAt);
        await _objects.PutAsync(objectKey, compressed, cancellationToken);
        try
        {
            await using var db = await _db.CreateAsync();
            var archive = new PaifuArchiveLogEntity
            {
                PlayedAt = payload.PlayedAt.UtcDateTime,
                ChannelId = room.ChannelId,
                RoomId = checked((uint)room.RoomId),
                RoomName = room.RoomTitle,
                RoomOption = room.RoomOption,
                ResultText = payload.Result,
                MembersJson = JsonSerializer.Serialize(payload.Members),
                PacketCount = item.Packets.Count,
                S3ObjectKey = objectKey,
                ExpiresAt = payload.PlayedAt.UtcDateTime.AddDays(_retentionDays),
                CreatedAt = DateTime.UtcNow,
            };
            db.PaifuArchives.Add(archive);
            await db.SaveChangesAsync();
            foreach (var member in members)
                db.PaifuArchiveMembers.Add(new PaifuArchiveMemberLogEntity { PaifuArchiveId = archive.PaifuArchiveId, MemberNo = ulong.Parse(member.MemberNo) });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _objects.DeleteAsync(objectKey);
            throw;
        }
    }

    public async Task<IReadOnlyList<PaifuArchiveSummary>> ListAsync(string memberNo, PaifuArchiveQuery? query = null)
    {
        if (!ulong.TryParse(memberNo, out var id)) return [];
        query ??= PaifuArchiveQuery.Empty;
        await using var db = await _db.CreateAsync();
        var archives = from archive in db.PaifuArchives.AsNoTracking()
                   join member in db.PaifuArchiveMembers.AsNoTracking() on archive.PaifuArchiveId equals member.PaifuArchiveId
                   where member.MemberNo == id && archive.ExpiresAt > DateTime.UtcNow
                   select archive;
        if (query.PlayedFrom is { } playedFrom) archives = archives.Where(item => item.PlayedAt >= playedFrom);
        if (query.PlayedToExclusive is { } playedToExclusive) archives = archives.Where(item => item.PlayedAt < playedToExclusive);
        if (!string.IsNullOrWhiteSpace(query.RoomName)) archives = archives.Where(item => item.RoomName.Contains(query.RoomName));
        if (!string.IsNullOrWhiteSpace(query.Result)) archives = archives.Where(item => item.ResultText.Contains(query.Result));
        if (!string.IsNullOrWhiteSpace(query.MemberName)) archives = archives.Where(item => item.MembersJson.Contains(query.MemberName));
        if (query.MatchKind == PaifuMatchKind.Normal) archives = archives.Where(item => !EF.Functions.Like(item.RoomOption, "________1%"));
        if (query.MatchKind == PaifuMatchKind.Tournament) archives = archives.Where(item => EF.Functions.Like(item.RoomOption, "________1%"));
        var rows = await archives
            .OrderByDescending(item => item.PlayedAt)
            .Take(Math.Clamp(query.Limit, 1, 100))
            .Select(item => new { item.PaifuArchiveId, item.PlayedAt, item.RoomName, item.RoomOption, item.ResultText, item.MembersJson, item.PacketCount })
            .ToListAsync();
        return rows.Select(item => new PaifuArchiveSummary(item.PaifuArchiveId, item.PlayedAt, item.RoomName, item.RoomOption, item.ResultText, ReadMembers(item.MembersJson), item.PacketCount)).ToArray();
    }

    public async Task<PaifuReplaySource?> GetReplaySourceAsync(string memberNo, ulong archiveId)
    {
        var archive = await GetOwnedArchiveAsync(memberNo, archiveId);
        return archive is null ? null : new PaifuReplaySource(_objects.CreateReplayUrl(archive.S3ObjectKey), archive.ExpiresAt);
    }

    public async Task RemoveExpiredAsync(CancellationToken cancellationToken)
    {
        if (!_objects.IsConfigured) return;
        await using var db = await _db.CreateAsync();
        var expired = await db.PaifuArchives.Where(item => item.ExpiresAt <= DateTime.UtcNow).ToListAsync(cancellationToken);
        foreach (var archive in expired)
        {
            await _objects.DeleteAsync(archive.S3ObjectKey, cancellationToken);
            db.PaifuArchiveMembers.RemoveRange(db.PaifuArchiveMembers.Where(member => member.PaifuArchiveId == archive.PaifuArchiveId));
        }
        db.PaifuArchives.RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static byte[] Compress(byte[] plain)
    {
        using var output = new MemoryStream();
        using (var stream = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true)) stream.Write(plain);
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var stream = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        if (output.Length > MaxPlaintextBytes) throw new InvalidDataException("Paifu payload exceeds the limit.");
        return output.ToArray();
    }

    private static string ReadResult(IReadOnlyDictionary<string, object?>? report)
        => report is not null && report.TryGetValue("result", out var value) ? Convert.ToString(value) ?? "" : "";

    private static JsonElement ToJsonElement(object value)
        => JsonSerializer.SerializeToElement(value);

    private static IReadOnlyList<PaifuMember> ReadMembers(string membersJson)
    {
        try { return JsonSerializer.Deserialize<PaifuMember[]>(membersJson) ?? []; }
        catch (JsonException) { return []; }
    }

    private async Task<PaifuArchiveLogEntity?> GetOwnedArchiveAsync(string memberNo, ulong archiveId)
    {
        if (!ulong.TryParse(memberNo, out var id)) return null;
        await using var db = await _db.CreateAsync();
        return await (from archive in db.PaifuArchives.AsNoTracking()
                      join member in db.PaifuArchiveMembers.AsNoTracking() on archive.PaifuArchiveId equals member.PaifuArchiveId
                      where archive.PaifuArchiveId == archiveId && member.MemberNo == id && archive.ExpiresAt > DateTime.UtcNow
                      select archive).SingleOrDefaultAsync();
    }
}

public sealed record PaifuArchiveSummary(ulong ArchiveId, DateTime PlayedAt, string RoomName, string RoomOption, string Result, IReadOnlyList<PaifuMember> Members, int PacketCount);
public sealed record PaifuMember(string Name, string Title, int Rating, string Result);
public sealed record PaifuArchiveParticipant(string MemberNo, string Name, string Title, int Rating);
public sealed record PaifuArchiveWorkItem(DateTimeOffset PlayedAt, string ChannelId, int RoomId, string RoomName, string RoomOption, IReadOnlyList<JsonElement> Packets, IReadOnlyList<PaifuArchiveParticipant> Members, string Result);
public sealed record PaifuPayload(int Version, DateTimeOffset PlayedAt, string ChannelId, int RoomId, string RoomName, string RoomOption, IReadOnlyList<JsonElement> Packets, IReadOnlyList<PaifuMember> Members, string Result);
public sealed record PaifuReplaySource(string Url, DateTime ExpiresAt);
public enum PaifuMatchKind { All, Normal, Tournament }
public sealed record PaifuArchiveQuery(DateTime? PlayedFrom, DateTime? PlayedToExclusive, string? RoomName, string? MemberName, string? Result, PaifuMatchKind MatchKind, int Limit)
{
    public static readonly PaifuArchiveQuery Empty = new(null, null, null, null, null, PaifuMatchKind.All, 100);
}