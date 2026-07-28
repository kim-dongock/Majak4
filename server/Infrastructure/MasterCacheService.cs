using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Models.Player;

namespace MajakServer.Infrastructure;

/// <summary>
/// マスターデータ Redis キャッシュサービス
///
/// 設計方針:
///   - プライマリリーダーが起動時に全マスターデータを MySQL から読み込んで Redis へ書き込む。
///   - セカンダリサーバーも起動時に Redis から読み込む (DB 接続不要)。
///   - Redis 未接続 / キャッシュミスの場合は DB にフォールバックし、Redis に書き戻す。
///
/// キャッシュキー一覧:
///   majak2:mast:titles          - Dictionary&lt;string,string&gt;                  TTL 24h
///   majak2:mast:customitems     - List&lt;CustomItemMastDto&gt;                    TTL 24h
///   majak2:mast:adminids        - List&lt;AdminIdInfo&gt;                          TTL 24h
///   majak2:mast:dailymission    - List&lt;DailyMissionMastDto&gt;                  TTL 24h
///   majak2:mast:weeklyreward    - List&lt;WeeklyRewardMastDto&gt;                  TTL 1h
///   majak2:mast:grademanage     - List&lt;GradeSelectItem&gt;                      TTL 1h
///   majak2:mast:cups            - List&lt;CupConfigDto&gt;                         TTL 2min
///   majak2:mast:channels        - List&lt;ChannelInfoDto&gt;                       TTL 15min
///   majak2:ranking:grade:{date}:{kind}  - List&lt;GradeRankItem&gt;               TTL 5min
///   majak2:cup:topscore:{chId}          - int                                TTL 1min
/// </summary>
public class MasterCacheService
{
    // ── Redis キー定数 ──────────────────────────────────────────────────
    public const string KeyTitles       = "majak2:mast:titles";
    public const string KeyCustomItems  = "majak2:mast:customitems";
    public const string KeyCustomShop   = "majak2:mast:customshop";
    public const string KeyCustomSet    = "majak2:mast:customset";
    public const string KeyAdminIds     = "majak2:mast:adminids";
    public const string KeyDailyMission = "majak2:mast:dailymission";
    public const string KeyWeeklyMast   = "majak2:mast:weeklyreward";
    public const string KeyGradeManage  = "majak2:mast:grademanage";
    public const string KeyCupConfigs   = "majak2:mast:cups";
    public const string KeyChannels     = "majak2:mast:channels";
    public const string KeyProPlayers   = "majak2:mast:proplayers";

    public static string KeyGradeRankList(int rankDate, int rankKind, int maxCnt)
        => $"majak2:ranking:grade:{rankDate}:{rankKind}:{maxCnt}";
    public static string KeyGradeRankSelf(int rankDate, string memberNo, int grade)
        => $"majak2:ranking:grade:self:{rankDate}:{memberNo}:{grade}";
    public static string KeyCupTopScore(string channelId)
        => $"majak2:cup:topscore:{channelId}";

    // ── TTL 定数 ────────────────────────────────────────────────────────
    private static readonly TimeSpan TtlStatic     = TimeSpan.FromDays(1);      // 称号・カスタムアイテム・管理者ID
    private static readonly TimeSpan TtlShop       = TimeSpan.FromMinutes(5);   // カスタムショップ (販売期間が変わるため短め)
    private static readonly TimeSpan TtlHourly     = TimeSpan.FromHours(1);     // 週間報酬マスター・グレード管理
    private static readonly TimeSpan TtlCupConfigs = TimeSpan.FromMinutes(2);   // カップ設定 (頻繁に変化)
    private static readonly TimeSpan TtlChannels   = TimeSpan.FromMinutes(15);  // チャンネル一覧
    public  static readonly TimeSpan TtlRanking    = TimeSpan.FromMinutes(5);   // ランキング
    public  static readonly TimeSpan TtlCupScore   = TimeSpan.FromMinutes(1);   // カップ最高スコア

    private readonly RedisService        _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MasterCacheService> _logger;

    public MasterCacheService(
        RedisService redis,
        IServiceScopeFactory scopeFactory,
        ILogger<MasterCacheService> logger)
    {
        _redis        = redis;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ── 起動時一括ロード ────────────────────────────────────────────────

    /// <summary>
    /// 起動時に全マスターデータを Redis へ書き込む。
    /// プライマリリーダーから呼ぶが、Redis 未接続の場合は何もしない。
    /// </summary>
    public async Task BootstrapAsync()
    {
        if (!_redis.IsAvailable)
        {
            _logger.LogWarning("MasterCacheService: Redis not available, skipping bootstrap.");
            return;
        }

        _logger.LogInformation("MasterCacheService: bootstrapping master data into Redis…");
        await using var scope = _scopeFactory.CreateAsyncScope();
        var playerRepo  = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var itemRepo    = scope.ServiceProvider.GetRequiredService<ItemRepository>();
        var channelRepo = scope.ServiceProvider.GetRequiredService<ChannelRepository>();

        await Task.WhenAll(
            BootstrapTitlesAsync(playerRepo),
            BootstrapCustomItemsAsync(itemRepo),
            BootstrapCustomShopAsync(itemRepo),
            BootstrapCustomSetAsync(itemRepo),
            BootstrapAdminIdsAsync(playerRepo),
            BootstrapDailyMissionAsync(playerRepo),
            BootstrapWeeklyMastAsync(playerRepo),
            BootstrapGradeManageAsync(playerRepo),
            BootstrapCupConfigsAsync(playerRepo),
            BootstrapChannelsAsync(channelRepo)
        );
        _logger.LogInformation("MasterCacheService: bootstrap complete.");
    }

    private async Task BootstrapTitlesAsync(PlayerRepository repo)
    {
        try
        {
            var data = await repo.GetTitleMastAsync();
            await _redis.SetJsonAsync(KeyTitles, data, TtlStatic);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyTitles, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyTitles); }
    }

    private async Task BootstrapCustomItemsAsync(ItemRepository repo)
    {
        try
        {
            var raw  = await repo.GetCustomItemMastAsync();
            var data = raw.Select(x => new CustomItemMastDto(x.CustomId, x.Kind, x.Name, x.Price)).ToList();
            await _redis.SetJsonAsync(KeyCustomItems, data, TtlStatic);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyCustomItems, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyCustomItems); }
    }

    private async Task BootstrapCustomShopAsync(ItemRepository repo)
    {
        try
        {
            var data = await repo.GetCustomShopMastAsync();
            await _redis.SetJsonAsync(KeyCustomShop, data, TtlShop);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyCustomShop, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyCustomShop); }
    }

    private async Task BootstrapCustomSetAsync(ItemRepository repo)
    {
        try
        {
            var data = await repo.GetCustomSetMastAsync();
            await _redis.SetJsonAsync(KeyCustomSet, data, TtlStatic);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyCustomSet, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyCustomSet); }
    }

    private async Task BootstrapAdminIdsAsync(PlayerRepository repo)
    {
        try
        {
            var data = await repo.GetAdminIdListAsync();
            await _redis.SetJsonAsync(KeyAdminIds, data, TtlStatic);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyAdminIds, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyAdminIds); }
    }

    private async Task BootstrapDailyMissionAsync(PlayerRepository repo)
    {
        try
        {
            var raw  = await repo.GetDailyMissionMastAsync();
            var data = raw.Values.Select(v => new DailyMissionMastDto(v.MissionId, v.ConditionType, v.ConditionCnt, v.Point)).ToList();
            await _redis.SetJsonAsync(KeyDailyMission, data, TtlStatic);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyDailyMission, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyDailyMission); }
    }

    private async Task BootstrapWeeklyMastAsync(PlayerRepository repo)
    {
        try
        {
            var raw  = await repo.GetWeeklyRewardMastAsync();
            var data = raw.Values.Select(v => new WeeklyRewardMastDto(v.RewardId, v.RewardType, v.RewardCnt, v.MustPoint)).ToList();
            await _redis.SetJsonAsync(KeyWeeklyMast, data, TtlHourly);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyWeeklyMast, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyWeeklyMast); }
    }

    private async Task BootstrapGradeManageAsync(PlayerRepository repo)
    {
        try
        {
            var data = await repo.GetGradeManageListAsync();
            await _redis.SetJsonAsync(KeyGradeManage, data, TtlHourly);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyGradeManage, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyGradeManage); }
    }

    private async Task BootstrapCupConfigsAsync(PlayerRepository repo)
    {
        try
        {
            var raw  = await repo.GetCupConfigsAsync();
            var data = raw.Select(c => new CupConfigDto(
                c.ChannelId, c.ChannelName, c.DateFrom, c.DateTo, c.IsFestive,
                c.CupId, c.CupSeq, c.JudgementType, c.CupPointSumType,
                c.MaxMatchCntLimit, c.ConditionRegular,
                c.EntryLimited, c.ConditionBilling, c.MinLevel, c.MaxLevel,
                c.NormalYakuCondition, c.YakumanCondition)).ToList();
            await _redis.SetJsonAsync(KeyCupConfigs, data, TtlCupConfigs);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyCupConfigs, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyCupConfigs); }
    }

    private async Task BootstrapChannelsAsync(ChannelRepository repo)
    {
        try
        {
            var raw  = await repo.GetChannelListAsync();
            var data = raw.Select(c => new ChannelInfoDto(
                c.ChanelId, c.SubId, c.ChanelName,
                c.MaxMember, c.MaxRoom, c.ChanelType, c.UnitMoney,
                c.MemberCnt, c.UsedRoom, c.IsLocked)).ToList();
            await _redis.SetJsonAsync(KeyChannels, data, TtlChannels);
            _logger.LogDebug("MasterCache: {Key} loaded ({N} entries)", KeyChannels, data.Count);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "MasterCache: failed to load {Key}", KeyChannels); }
    }

    // ── 個別 GetXxxAsync (キャッシュアサイド) ──────────────────────────

    /// <summary>称号マスター (TITLEID → TITLENAME)</summary>
    public async Task<Dictionary<string, string>> GetTitleMastAsync()
    {
        var cached = await _redis.GetJsonAsync<Dictionary<string, string>>(KeyTitles);
        if (cached is { Count: > 0 }) return cached;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var data = await repo.GetTitleMastAsync();
        await _redis.SetJsonAsync(KeyTitles, data, TtlStatic);
        return data;
    }

    /// <summary>カスタムアイテムマスター (CustomId → (Kind, Name, Price))</summary>
    public async Task<List<(int CustomId, int Kind, string Name, long Price)>> GetCustomItemMastAsync()
    {
        var cached = await _redis.GetJsonAsync<List<CustomItemMastDto>>(KeyCustomItems);
        if (cached is { Count: > 0 })
            return cached.Select(x => (x.CustomId, x.Kind, x.Name, x.Price)).ToList();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ItemRepository>();
        var data = await repo.GetCustomItemMastAsync();
        var dto  = data.Select(x => new CustomItemMastDto(x.CustomId, x.Kind, x.Name, x.Price)).ToList();
        await _redis.SetJsonAsync(KeyCustomItems, dto, TtlStatic);
        return data;
    }

    /// <summary>カスタムショップマスター (販売期間あり、短期 TTL)</summary>
    public async Task<List<CustomShopItemInfo>> GetCustomShopMastAsync()
    {
        var cached = await _redis.GetJsonAsync<List<CustomShopItemInfo>>(KeyCustomShop);
        if (cached is not null) return cached;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ItemRepository>();
        var data = await repo.GetCustomShopMastAsync();
        await _redis.SetJsonAsync(KeyCustomShop, data, TtlShop);
        return data;
    }

    /// <summary>カスタムセットマスター (set customId → child customIds)</summary>
    public async Task<Dictionary<int, List<int>>> GetCustomSetMastAsync()
    {
        var cached = await _redis.GetJsonAsync<Dictionary<int, List<int>>>(KeyCustomSet);
        if (cached is not null) return cached;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ItemRepository>();
        var data = await repo.GetCustomSetMastAsync();
        await _redis.SetJsonAsync(KeyCustomSet, data, TtlStatic);
        return data;
    }

    /// <summary>管理者 ID リスト</summary>
    public async Task<List<AdminIdInfo>> GetAdminIdListAsync()
    {
        var cached = await _redis.GetJsonAsync<List<AdminIdInfo>>(KeyAdminIds);
        if (cached is not null) return cached;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var data = await repo.GetAdminIdListAsync();
        await _redis.SetJsonAsync(KeyAdminIds, data, TtlStatic);
        return data;
    }

    /// <summary>デイリーミッションマスター (MISSIONID → DailyMissionMastInfo)</summary>
    public async Task<Dictionary<int, DailyMissionMastInfo>> GetDailyMissionMastAsync()
    {
        var cached = await _redis.GetJsonAsync<List<DailyMissionMastDto>>(KeyDailyMission);
        if (cached is { Count: > 0 })
            return cached.ToDictionary(x => x.MissionId,
                x => new DailyMissionMastInfo { MissionId = x.MissionId, ConditionType = x.ConditionType, ConditionCnt = x.ConditionCnt, Point = x.Point });

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var data = await repo.GetDailyMissionMastAsync() ?? new Dictionary<int, DailyMissionMastInfo>();
        var dto  = data.Values.Select(v => new DailyMissionMastDto(v.MissionId, v.ConditionType, v.ConditionCnt, v.Point)).ToList();
        await _redis.SetJsonAsync(KeyDailyMission, dto, TtlStatic);
        return data;
    }

    /// <summary>週間報酬マスター (REWARDID → WeeklyRewardMastInfo)</summary>
    public async Task<Dictionary<int, WeeklyRewardMastInfo>> GetWeeklyRewardMastAsync()
    {
        var cached = await _redis.GetJsonAsync<List<WeeklyRewardMastDto>>(KeyWeeklyMast);
        if (cached is { Count: > 0 })
            return cached.ToDictionary(x => x.RewardId,
                x => new WeeklyRewardMastInfo { RewardId = x.RewardId, RewardType = x.RewardType, RewardCnt = x.RewardCnt, MustPoint = x.MustPoint });

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var data = await repo.GetWeeklyRewardMastAsync() ?? new Dictionary<int, WeeklyRewardMastInfo>();
        var dto  = data.Values.Select(v => new WeeklyRewardMastDto(v.RewardId, v.RewardType, v.RewardCnt, v.MustPoint)).ToList();
        await _redis.SetJsonAsync(KeyWeeklyMast, dto, TtlHourly);
        return data;
    }

    /// <summary>グレード管理リスト (年月選択肢)</summary>
    public async Task<List<GradeSelectItem>> GetGradeManageListAsync()
    {
        var cached = await _redis.GetJsonAsync<List<GradeSelectItem>>(KeyGradeManage);
        if (cached is not null) return cached;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var data = await repo.GetGradeManageListAsync();
        await _redis.SetJsonAsync(KeyGradeManage, data, TtlHourly);
        return data;
    }

    /// <summary>カップチャンネル設定</summary>
    public async Task<List<CupConfig>> GetCupConfigsAsync()
    {
        var cached = await _redis.GetJsonAsync<List<CupConfigDto>>(KeyCupConfigs);
        if (cached is not null)
            return cached.Select(c => new CupConfig(
                c.ChannelId, c.ChannelName, c.DateFrom, c.DateTo, c.IsFestive,
                c.CupId, c.CupSeq, c.JudgementType, c.CupPointSumType,
                c.MaxMatchCntLimit, c.ConditionRegular,
                c.EntryLimited, c.ConditionBilling, c.MinLevel, c.MaxLevel,
                c.NormalYakuCondition, c.YakumanCondition)).ToList();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var data = await repo.GetCupConfigsAsync();
        var dto  = data.Select(c => new CupConfigDto(
            c.ChannelId, c.ChannelName, c.DateFrom, c.DateTo, c.IsFestive,
            c.CupId, c.CupSeq, c.JudgementType, c.CupPointSumType,
            c.MaxMatchCntLimit, c.ConditionRegular,
            c.EntryLimited, c.ConditionBilling, c.MinLevel, c.MaxLevel,
            c.NormalYakuCondition, c.YakumanCondition)).ToList();
        await _redis.SetJsonAsync(KeyCupConfigs, dto, TtlCupConfigs);
        return data;
    }

    /// <summary>チャンネル一覧 (CHANELMAST + CHANELWT JOIN 結果)</summary>
    public async Task<IReadOnlyList<ChannelInfo>> GetChannelListAsync(string gameId = "MAJAK4")
    {
        var cached = await _redis.GetJsonAsync<List<ChannelInfoDto>>(KeyChannels);
        if (cached is not null)
            return cached.Select(c => new ChannelInfo
            {
                ChanelId   = c.ChanelId,
                SubId      = c.SubId,
                ChanelName = ChannelRepository.RepairDisplayName(c.SubId, c.ChanelName),
                MaxMember  = c.MaxMember,
                MaxRoom    = c.MaxRoom,
                ChanelType = c.ChanelType,
                UnitMoney  = c.UnitMoney,
                MemberCnt  = c.MemberCnt,
                UsedRoom   = c.UsedRoom,
                IsLocked   = c.IsLocked,
            }).ToList();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ChannelRepository>();
        var data = await repo.GetChannelListAsync(gameId);
        var dto  = data.Select(c => new ChannelInfoDto(
            c.ChanelId, c.SubId, c.ChanelName,
            c.MaxMember, c.MaxRoom, c.ChanelType, c.UnitMoney,
            c.MemberCnt, c.UsedRoom, c.IsLocked)).ToList();
        await _redis.SetJsonAsync(KeyChannels, dto, TtlChannels);
        return data;
    }

    // ── キャッシュ無効化 ─────────────────────────────────────────────────

    /// <summary>カップ設定キャッシュを無効化する (UpdateCupStatus 後に呼ぶ)</summary>
    public Task InvalidateCupConfigsAsync()
        => _redis.InvalidateAsync(KeyCupConfigs);

    /// <summary>チャンネル一覧キャッシュを無効化する</summary>
    public Task InvalidateChannelsAsync()
        => _redis.InvalidateAsync(KeyChannels);
}

// ── Redis シリアライズ用 DTO (record は System.Text.Json で安全にシリアライズできる) ──

internal record CustomItemMastDto(int CustomId, int Kind, string Name, long Price);
internal record DailyMissionMastDto(int MissionId, int ConditionType, int ConditionCnt, int Point);
internal record WeeklyRewardMastDto(int RewardId, int RewardType, int RewardCnt, int MustPoint);
internal record CupConfigDto(string ChannelId, string ChannelName, DateTime DateFrom, DateTime DateTo, bool IsFestive,
    int CupId = 0, int CupSeq = 0, int JudgementType = -1, int CupPointSumType = 0,
    int MaxMatchCntLimit = -1, int ConditionRegular = 0,
    bool EntryLimited = false, int ConditionBilling = 0, int MinLevel = 0, int MaxLevel = 0,
    string NormalYakuCondition = "", string YakumanCondition = "");
internal record ChannelInfoDto(
    string ChanelId, string SubId, string ChanelName,
    int MaxMember, int MaxRoom, int ChanelType, int UnitMoney,
    int MemberCnt, int UsedRoom, bool IsLocked);
