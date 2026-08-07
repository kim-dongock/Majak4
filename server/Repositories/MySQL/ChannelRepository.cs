using System.Text;
using Microsoft.VisualBasic.FileIO;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using MajakServer.Utils;
using Microsoft.EntityFrameworkCore;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// チャンネル一覧取得 — CHANELMAST / CHANELWT 直接参照
/// レガシーではチャンネルサーバー経由で受信していたが、
/// 新規実装ではチャンネルサーバーを使用しないため MySQL から直接取得する。
/// Redis キャッシュ (15分 TTL) を使い、MySQL への接続を最小化する。
/// </summary>
public class ChannelRepository
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> LegacyChannelNames = new(LoadLegacyChannelNames);

    private readonly RedisService    _redis;
    private readonly GameDataContextFactory _gameDb;

    public ChannelRepository(GameDataContextFactory gameDb, RedisService redis)
    {
        _gameDb = gameDb;
        _redis = redis;
    }

    /// <summary>
    /// チャンネル一覧を CHANELMAST × CHANELWT の LEFT JOIN で取得する。
    /// CHANELMAST: 静的設定 (チャンネル名・最大人数・タイプ等)
    /// CHANELWT:   動的状態 (現在人数・使用中ルーム数等)
    /// Redis キャッシュヒット時は DB 接続を行わない。
    /// </summary>
    public virtual async Task<IReadOnlyList<ChannelInfo>> GetChannelListAsync(string gameId = "MAJAK4")
    {
        // Redis キャッシュ確認 (15分 TTL) — MasterCacheService 経由で呼ばれることが多いが、
        // Program.cs 起動時など直接呼ばれる場合もキャッシュを使う
        var cached = await _redis.GetJsonAsync<List<ChannelInfo>>(MasterCacheService.KeyChannels);
        if (cached is not null) return RepairChannelNames(cached);

        await using var db = await _gameDb.CreateAsync();
        var rows = await (
            from master in db.ChannelMasters.AsNoTracking()
            join runtime in db.ChannelRuntimes.AsNoTracking()
                on new { master.ChannelId, master.GameId } equals new { runtime.ChannelId, runtime.GameId }
                into runtimes
            from runtime in runtimes.DefaultIfEmpty()
            where master.GameId == gameId && master.IsActive
            orderby master.SubId, master.ChannelId
            select new
            {
                master.ChannelId,
                master.SubId,
                master.ChannelName,
                master.MaxMember,
                master.MaxRoom,
                master.ChannelType,
                master.UnitMoney,
                MemberCount = runtime == null ? (ushort)0 : runtime.MemberCount,
                UsedRoom = runtime == null ? (ushort)0 : runtime.UsedRoom,
                IsLocked = runtime != null && runtime.IsLocked,
            }).ToListAsync();
        var list = rows.Select(row => new ChannelInfo
        {
            ChanelId = row.ChannelId,
            SubId = row.SubId,
            ChanelName = RepairChannelName(row.SubId, row.ChannelName),
            MaxMember = checked((int)row.MaxMember),
            MaxRoom = checked((int)row.MaxRoom),
            ChanelType = row.ChannelType,
            UnitMoney = checked((int)row.UnitMoney),
            MemberCnt = row.MemberCount,
            UsedRoom = row.UsedRoom,
            IsLocked = row.IsLocked,
        }).ToList();
        await _redis.SetJsonAsync(MasterCacheService.KeyChannels, list, TimeSpan.FromMinutes(15));
        return list;
    }

    /// <summary>
    /// Redis を使わず CHANELMAST.MAXROOM だけを直接取得する。
    /// 原典: HMajChnlServer::ProcessCommand_GetRoomList の m_pChnlInfo-&gt;m_nMaxRoom。
    /// </summary>
    public async Task<int?> GetMaxRoomDirectAsync(string channelId, string gameId = "MAJAK4")
    {
        await using var db = await _gameDb.CreateAsync();
        uint? maxRoom = await db.ChannelMasters.AsNoTracking()
            .Where(channel => channel.GameId == gameId && channel.IsActive &&
                (channel.ChannelId == channelId || channel.SubId == channelId))
            .Select(channel => (uint?)channel.MaxRoom)
            .FirstOrDefaultAsync();
        return maxRoom.HasValue ? checked((int)maxRoom.Value) : null;
    }

    private static IReadOnlyList<ChannelInfo> RepairChannelNames(IEnumerable<ChannelInfo> channels)
        => channels.Select(c => new ChannelInfo
        {
            ChanelId   = c.ChanelId,
            SubId      = c.SubId,
            ChanelName = RepairChannelName(c.SubId, c.ChanelName),
            MaxMember  = c.MaxMember,
            MaxRoom    = c.MaxRoom,
            ChanelType = c.ChanelType,
            UnitMoney  = c.UnitMoney,
            MemberCnt  = c.MemberCnt,
            UsedRoom   = c.UsedRoom,
            IsLocked   = c.IsLocked,
        }).ToList();

    public static string RepairDisplayName(string subId, string chanelName)
    {
        string name = NeedsLegacyName(subId, chanelName) && LegacyChannelNames.Value.TryGetValue(subId, out var legacyName)
            ? legacyName
            : chanelName;

        return NormalizeLegacyChannelName(name);
    }

    private static string RepairChannelName(string subId, string chanelName)
        => RepairDisplayName(subId, chanelName);

    private static string NormalizeLegacyChannelName(string name)
        => name.Replace("ﾛﾋﾞｰ", "広場", StringComparison.Ordinal)
               .Replace("ロビー", "広場", StringComparison.Ordinal);

    private static bool NeedsLegacyName(string subId, string value)
        => string.IsNullOrWhiteSpace(value)
            || value.Contains('\uFFFD')
            || string.Equals(value.Trim(), subId, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> LoadLegacyChannelNames()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = FindLegacyChannelMastPath();
        if (path is null) return new Dictionary<string, string>();

        using var parser = new TextFieldParser(path, Encoding.GetEncoding(932));
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        var header = parser.ReadFields();
        if (header is null) return new Dictionary<string, string>();
        int subIdIdx = Array.IndexOf(header, "SUBID");
        int nameIdx  = Array.IndexOf(header, "CHANELNAME");
        if (subIdIdx < 0 || nameIdx < 0) return new Dictionary<string, string>();

        var names = new Dictionary<string, string>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields();
            if (row is null || row.Length <= Math.Max(subIdIdx, nameIdx)) continue;
            names[row[subIdIdx]] = row[nameIdx];
        }
        return names;
    }

    private static string? FindLegacyChannelMastPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "DB", "CHANELMAST.csv"),
            Path.Combine(AppContext.BaseDirectory, "DB", "CHANELMAST.csv"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DB", "CHANELMAST.csv")),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    // ── CHANELMAST の数値列は VARCHAR2 で定義されているため安全にパースする
    private static int ParseInt(string? s, int defaultVal)
        => int.TryParse(s, out int v) ? v : defaultVal;
}

/// <summary>チャンネル一覧エントリ (CHANELMAST + CHANELWT 結合結果)</summary>
public sealed class ChannelInfo
{
    /// <summary>ロビー一意 ID (例: MAJAK20090A001)</summary>
    public string ChanelId   { get; init; } = "";
    /// <summary>チャンネルグループ (例: 0090A / 1090A / 00R3A)</summary>
    public string SubId      { get; init; } = "";
    /// <summary>表示名</summary>
    public string ChanelName { get; init; } = "";
    /// <summary>ロビー定員</summary>
    public int    MaxMember  { get; init; }
    /// <summary>ルーム上限数</summary>
    public int    MaxRoom    { get; init; }
    /// <summary>チャンネルタイプ (0=一般/1=初心者/3=段位 等)</summary>
    public int    ChanelType { get; init; }
    /// <summary>単位マネー (室料計算用)</summary>
    public int    UnitMoney  { get; init; }
    /// <summary>現在接続人数 (CHANELWT.MEMBERCNT)</summary>
    public int    MemberCnt  { get; init; }
    /// <summary>使用中ルーム数 (CHANELWT.USEDROOM)</summary>
    public int    UsedRoom   { get; init; }
    /// <summary>ロック中フラグ (LOCKSTATE != 0)</summary>
    public bool   IsLocked   { get; init; }
}
