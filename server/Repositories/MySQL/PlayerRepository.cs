using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL.Entities;
using MajakServer.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// Player login, rating, and reward repository based on HMajDBObject::GetMemberInfo flows.
/// </summary>
public class PlayerRepository
{
    public static bool IsSkinAttachFlagSet(string? attachFlag)
        => !string.IsNullOrEmpty(attachFlag) && (attachFlag[0] == 'Y' || attachFlag[0] == 'y');

    private static readonly long[] MoneyLevelThresholds =
    [
        0L, 1L, 500L, 1500L, 3000L, 10000L, 30000L, 100000L, 500000L, 1000000L, 5000000L,
    ];

    private static readonly string[] MoneyLevelNames =
    [
        "一文無し", "貧乏", "庶民", "小金持ち", "一般人", "中流", "上流", "金持ち", "富豪", "大富豪", "大金持ち",
    ];

    private readonly RedisService? _redis;
    private readonly GameDataContextFactory? _gameDb;
    private readonly LogRepository? _log;

    private static ulong ParseMemberNo(string memberNo)
        => MemberNoIds.Parse(memberNo);

    private async Task ExecuteGameTransactionAsync(
        Func<GameDataContext, IDbContextTransaction, Task> operation)
    {
        await using var strategyDb = await RequireGameDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireGameDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                await operation(db, tx);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    private async Task<TResult> ExecuteGameTransactionAsync<TResult>(
        Func<GameDataContext, IDbContextTransaction, Task<TResult>> operation)
    {
        await using var strategyDb = await RequireGameDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireGameDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                return await operation(db, tx);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    public PlayerRepository()
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public PlayerRepository(
        RedisService redis,
        GameDataContextFactory gameDb,
        LogRepository log)
    {
        _redis = redis;
        _gameDb = gameDb;
        _log = log;
    }

    // Player cache TTL helpers.

    /// <summary>Returns the TTL until midnight for daily mission caches.</summary>
    private static TimeSpan TtlUntilMidnight()
        => DateTime.Today.AddDays(1) - DateTime.Now;

    /// <summary>Returns the TTL until next Monday for weekly reward caches.</summary>
    private static TimeSpan TtlUntilNextMonday()
    {
        var today   = DateTime.Today;
        int daysToMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysToMonday == 0) daysToMonday = 7;   // If today is Monday, use the next Monday.
        return today.AddDays(daysToMonday) - DateTime.Now;
    }

    /// <summary>Returns this week's Monday as yyyyMMdd for weekly reward cache keys.</summary>
    private static string WeekStartKey()
    {
        var today = DateTime.Today;
        int offset = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return today.AddDays(-offset).ToString("yyyyMMdd");
    }

    /// <summary>
    /// Loads MJKCOMMONRAT state used by GetMemberInfo.
    /// </summary>
    public virtual async Task<bool> LoadCommonRatAsync(MajakPlayer player)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var state = await (
            from wallet in db.PlayerWallets.AsNoTracking()
            join profile in db.PlayerProfiles.AsNoTracking()
                on wallet.MemberNo equals profile.MemberNo
            where wallet.MemberNo == memberNoValue
            select new { Wallet = wallet, Profile = profile })
            .SingleOrDefaultAsync();
        if (state is null) return false;

        player.Rating = state.Profile.CommonRating;
        player.GamMoney = state.Wallet.GameMoney;
        player.LastGameDate = state.Profile.LastPlayedAt?.ToString("yyyy/MM/dd HH:mm:ss") ?? string.Empty;
        player.EarnedMoney = state.Wallet.EarnedGameMoney;
        player.GamMoneyU = state.Wallet.PendingGameMoney;
        player.Experience = state.Profile.Experience;
        player.ContWinDefeat = state.Profile.ConsecutiveWinLoss;
        player.AllinCnt = checked((int)state.Profile.AllInCount);
        player.LastAllinDt = state.Profile.LastAllInAt;
        player.GemCount = state.Wallet.GemCount;
        player.CashCount = state.Wallet.CashCount;
        player.PaidCashCount = state.Wallet.PaidCashCount;
        player.FreeCashCount = state.Wallet.FreeCashCount;
        player.TrickTitle = state.Profile.TrickTitleCode ?? string.Empty;
        player.MajakTitle = state.Profile.MajakTitleCode ?? string.Empty;
        UpdateMoneyLevel(player);

        // Parse TRICKTITLE: "mjks001" => TrickTitleId=1.
        if (int.TryParse(player.TrickTitle.Replace("mjks", ""), out int t))
            player.TrickTitleId = t;

        // Parse MAJAKTITLE: "mjkt001" => MajakTitleId=1, "mjkc001" => 1001.
        if (player.MajakTitle.StartsWith("mjkt") && int.TryParse(player.MajakTitle[4..], out int m))
            player.MajakTitleId = m;
        else if (player.MajakTitle.StartsWith("mjkc") && int.TryParse(player.MajakTitle[4..], out int mc))
            player.MajakTitleId = mc + 1000;

        return true;
    }

    /// <summary>
    /// Checks whether the MJKCOMMONRAT records already exist.
    /// </summary>
    public virtual async Task<bool> ExistsCommonRatAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        return await db.PlayerWallets.AnyAsync(item => item.MemberNo == memberNoValue)
            && await db.PlayerProfiles.AnyAsync(item => item.MemberNo == memberNoValue);
    }

    /// <summary>
    /// Creates new MJKCOMMONRAT records for CreateMemberGameRecord.
    /// </summary>
    public virtual async Task CreateCommonRatAsync(string memberNo, long initialMoney)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        bool hasAccount = await db.PlayerAccounts.AnyAsync(item => item.MemberNo == memberNoValue);
        if (!hasAccount)
            throw new InvalidOperationException($"Player account does not exist: {memberNo}");

        var now = DateTime.Now;
        if (!await db.PlayerWallets.AnyAsync(item => item.MemberNo == memberNoValue))
            db.PlayerWallets.Add(new PlayerWalletEntity
            {
                MemberNo = memberNoValue,
                GameMoney = initialMoney,
                CreatedAt = now,
                UpdatedAt = now,
            });
        if (!await db.PlayerProfiles.AnyAsync(item => item.MemberNo == memberNoValue))
            db.PlayerProfiles.Add(new PlayerProfileEntity
            {
                MemberNo = memberNoValue,
                JoinedAt = now,
                WeeklyTargetDate = DateOnly.FromDateTime(now),
                CreatedAt = now,
                UpdatedAt = now,
            });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Clears existing guest game records before HMajDBObject::OnSvcGetMemberInfoSuccess migration.
    /// </summary>
    public virtual async Task ResetGuestGameRecordsAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await ExecuteGameTransactionAsync(async (db, tx) =>
        {
        await db.PlayerHighClassYaku.Where(item => item.MemberNo == memberNoValue).ExecuteDeleteAsync();
        await db.PlayerHighClassSummaries.Where(item => item.MemberNo == memberNoValue).ExecuteDeleteAsync();
        await db.PlayerModeStats.Where(item => item.MemberNo == memberNoValue).ExecuteDeleteAsync();
        await db.CupPlayerRatings.Where(item => item.MemberNo == memberNoValue).ExecuteDeleteAsync();
        await db.PlayerFunctionItems.Where(item => item.MemberNo == memberNoValue).ExecuteDeleteAsync();
        await db.PlayerWallets.Where(item => item.MemberNo == memberNoValue).ExecuteDeleteAsync();
        await db.PlayerProfiles.Where(item => item.MemberNo == memberNoValue).ExecuteDeleteAsync();
        await tx.CommitAsync();
        });
    }

    /// <summary>
    /// Ensures channel-specific sub records for HMajDBObject::CreateSubRecord.
    /// </summary>
    public virtual async Task EnsureSubRecordAsync(string memberNo, bool isGradeChannel, bool isCompeteChannel, bool isHiClassChannel, int? cupId = null)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var strategyDb = await RequireGameDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireGameDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var now = DateTime.Now;
            if (isGradeChannel)
            {
                await EnsureModeStatsAsync(db, memberNoValue, "grade", now);
                bool hasTitle = await db.PlayerTitles
                    .AnyAsync(item => item.MemberNo == memberNoValue && item.TitleId == GameConst.RatingTitle10Kyu);
                if (!hasTitle)
                    db.PlayerTitles.Add(new PlayerTitleEntity
                    {
                        MemberNo = memberNoValue,
                        TitleId = GameConst.RatingTitle10Kyu,
                        AcquiredAt = now,
                    });
            }
            else if (isCompeteChannel)
            {
                await EnsureModeStatsAsync(db, memberNoValue, "compete", now);
            }
            else
            {
                await EnsureModeStatsAsync(db, memberNoValue, "regular", now);
                int cupRatId = cupId.GetValueOrDefault();
                if (cupRatId > 0 && !await db.CupPlayerRatings
                        .AnyAsync(item => item.MemberNo == memberNoValue && item.CupId == (uint)cupRatId))
                    db.CupPlayerRatings.Add(new CupPlayerRatingEntity
                    {
                        CupId = checked((uint)cupRatId),
                        MemberNo = memberNoValue,
                        JoinedAt = now,
                        LastPlayedAt = now,
                    });
                if (isHiClassChannel)
                    await EnsureModeStatsAsync(db, memberNoValue, "high_class", now);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    private static async Task EnsureModeStatsAsync(GameDataContext db, ulong memberNoValue, string modeCode, DateTime now)
    {
        if (await db.PlayerModeStats.AnyAsync(item => item.MemberNo == memberNoValue && item.ModeCode == modeCode)) return;
        db.PlayerModeStats.Add(new PlayerModeStatsEntity
        {
            MemberNo = memberNoValue,
            ModeCode = modeCode,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    /// <summary>
    /// Loads MJKHANGERAT state for regular channels.
    /// </summary>
    public virtual async Task<bool> LoadHangeRatAsync(MajakPlayer player)
    {
        return await LoadModeStatsAsync(player.RegularRecord, player.MemberNo, "regular");
    }

    /// <summary>
    /// Loads MJKCOMPETERAT state.
    /// </summary>
    public virtual async Task<bool> LoadCompeteRatAsync(MajakPlayer player)
    {
        return await LoadModeStatsAsync(player.CompeteRecord, player.MemberNo, "compete");
    }

    /// <summary>
    /// Loads MJK_HICLASSRAT state for high-class channels.
    /// </summary>
    public virtual async Task<bool> LoadHiClassRatAsync(MajakPlayer player)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var stats = await db.PlayerModeStats.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue && item.ModeCode == "high_class");
        if (stats is null) return false;

        CopyModeStats(stats, player.HiClassRecord);
        var summary = await db.PlayerHighClassSummaries.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
        if (summary is not null)
        {
            player.H_ContTopMax = checked((int)summary.ConsecutiveTopMax);
            player.H_ContTopNow = checked((int)summary.ConsecutiveTopCurrent);
            player.HoraDoraMax = summary.WinHandDoraMax;
        }

        Array.Clear(player.YakuCount);
        Array.Clear(player.YmanCount);
        var yakuCounts = await db.PlayerHighClassYaku.AsNoTracking()
            .Where(item => item.MemberNo == memberNoValue)
            .ToListAsync();
        foreach (var yaku in yakuCounts)
        {
            if (yaku.YakuId < player.YakuCount.Length)
                player.YakuCount[yaku.YakuId] = checked((int)yaku.Count);
            else if (yaku.YakuId >= 100 && yaku.YakuId - 100 < player.YmanCount.Length)
                player.YmanCount[yaku.YakuId - 100] = checked((int)yaku.Count);
        }
        return true;
    }

    /// <summary>
    /// Loads MJK_GRADERAT state for grade mode.
    /// </summary>
    public virtual async Task<bool> LoadGradeRatAsync(MajakPlayer player)
    {
        return await LoadModeStatsAsync(player.GradeRecord, player.MemberNo, "grade");
    }

    private async Task<bool> LoadModeStatsAsync(RatingRecord record, string memberNo, string modeCode)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var stats = await db.PlayerModeStats.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue && item.ModeCode == modeCode);
        if (stats is null) return false;

        CopyModeStats(stats, record);
        return true;
    }

    private static void CopyModeStats(PlayerModeStatsEntity source, RatingRecord target)
    {
        target.Rating = source.Rating;
        target.MatchCnt = checked((int)source.MatchCount);
        target.WinCnt = checked((int)source.WinCount);
        target.DefeatCnt = checked((int)source.DefeatCount);
        target.DrawCnt = checked((int)source.DrawCount);
        target.Grade1 = checked((int)source.FirstCount);
        target.Grade2 = checked((int)source.SecondCount);
        target.Grade3 = checked((int)source.ThirdCount);
        target.Grade4 = checked((int)source.FourthCount);
        target.TurnCnt = checked((int)source.TurnCount);
        target.DaidaCnt = checked((int)source.DealerCount);
        target.PointSum = checked((int)source.PointSum);
        target.KyokuCnt = checked((int)source.RoundCount);
        target.HoraCnt = checked((int)source.WinHandCount);
        target.HoraPoint = checked((int)source.WinHandPoints);
        target.HojuCnt = checked((int)source.DealInCount);
        target.HojuPoint = checked((int)source.DealInPoints);
        target.RichiCnt = checked((int)source.RiichiCount);
        target.FuroCnt = checked((int)source.MeldCount);
        target.TipPoint = checked((int)source.TipPoint);
        target.TipMatchCnt = checked((int)source.TipMatchCount);
        target.TobiCnt = checked((int)source.BustCount);
        target.TobashiCnt = checked((int)source.BustOtherCount);
        target.DoraCnt = checked((int)source.DoraCount);
        target.UraDoraCnt = checked((int)source.UraDoraCount);
        target.RichiHoraCnt = checked((int)source.RiichiWinCount);
        target.DisconnCnt = checked((int)source.DisconnectCount);
        target.LastDisconn = source.LastDisconnectAt;
        target.ChannelId = source.LastChannelId ?? string.Empty;
        target.Grade = source.GradeLevel;
        target.GradePoint = source.GradePoint;
        target.TotExtraCount = checked((int)source.ExtraCount);
        target.LastExtraDate = source.LastExtraAt;
    }

    /// <summary>
    /// Loads MAJAKCUPRAT state for cup mode.
    /// </summary>
    public virtual async Task<bool> LoadCupRatAsync(MajakPlayer player, int cupId)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var db = await RequireGameDb().CreateAsync();
        uint targetCupId = checked((uint)cupId);
        var rating = await db.CupPlayerRatings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue && item.CupId == targetCupId);
        if (rating is null) return false;

        player.CupRec.CupPoint = rating.CupPoint;
        player.CupRec.CupMatchCnt = rating.MatchCount;
        return true;
    }

    /// <summary>
    /// Updates MAJAKCUPRAT via the HMajDBObject::UpdateResult_GambleType CUPRAT path.
    /// </summary>
    public virtual async Task UpdateCupRatAsync(MajakPlayer player, int cupId)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var db = await RequireGameDb().CreateAsync();
        uint targetCupId = checked((uint)cupId);
        var rating = await db.CupPlayerRatings
            .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue && item.CupId == targetCupId);
        if (rating is null) return;

        rating.CupPoint = checked(rating.CupPoint + player.CupPointGain);
        rating.MatchCount = rating.MatchCount == ushort.MaxValue
            ? ushort.MaxValue
            : checked((ushort)(rating.MatchCount + 1));
        rating.LastPlayedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Updates MJKCOMMONRAT rating and coin state for UpdateRefillData.
    /// </summary>
    public virtual async Task UpdateCommonRatAsync(MajakPlayer player)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var strategyDb = await RequireGameDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireGameDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                var profile = await db.PlayerProfiles.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                if (wallet is null || profile is null) return;

                wallet.GameMoney = player.GamMoney;
                wallet.EarnedGameMoney = checked(wallet.EarnedGameMoney - player.EarnedMoney);
                wallet.PendingGameMoney = 0;
                wallet.UpdatedAt = DateTime.Now;
                profile.CommonRating = player.Rating;
                profile.BestMoneyLevel = checked((byte)player.NLevel);
                profile.AllInCount = checked((uint)player.AllinCnt);
                profile.LastAllInAt = player.LastAllinDt;
                profile.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// MJKCOMMONRAT earned money reload ? HMajDBObject::GetEarnedMoney.
    /// </summary>
    public virtual async Task<(long EarnedMoney, int Experience)?> GetEarnedMoneyAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var state = await (
            from wallet in db.PlayerWallets.AsNoTracking()
            join profile in db.PlayerProfiles.AsNoTracking()
                on wallet.MemberNo equals profile.MemberNo
            where wallet.MemberNo == memberNoValue
            select new { wallet.EarnedGameMoney, profile.Experience })
            .SingleOrDefaultAsync();
        return state is null ? null : (state.EarnedGameMoney, state.Experience);
    }

    /// <summary>
    /// Adds earned game money and writes GAMEMONEYHIST.
    /// Legacy reference: PC_MAJAK2_HIST.
    /// </summary>
    public virtual async Task<int> AddEarnedGameMoneyAsync(
        string memberNo,
        long amount,
        string eventCode,
        string eventTitle,
        string orderNumber,
        string remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(memberNo) || string.IsNullOrWhiteSpace(eventCode))
            return 1;

        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            await using var db = await RequireGameDb().CreateAsync();
            var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
            if (wallet is null) return 2;

            long before = checked(wallet.GameMoney + wallet.PendingGameMoney + wallet.EarnedGameMoney);
            wallet.EarnedGameMoney = checked(wallet.EarnedGameMoney + amount);
            wallet.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            long after = checked(wallet.GameMoney + wallet.PendingGameMoney + wallet.EarnedGameMoney);

            try
            {
                await InsertGameMoneyHistFromTransactionCodeAsync(
                    memberNo, eventCode, amount, before, after, remoteAddress, orderNumber);
            }
            catch
            {
            }

            return 0;
        }
        catch
        {
            return 3;
        }
    }

    /// <summary>
    /// Updates MJKCOMMONRAT after a game result via the COMMON path.
    /// </summary>
    public virtual async Task UpdateResultCommonRatAsync(MajakPlayer player, bool isOutPlayer, long moneyChange, int experienceGain, int gemCount, int ranking)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var strategyDb = await RequireGameDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireGameDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                var profile = await db.PlayerProfiles.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                if (wallet is null || profile is null) return;

                var now = DateTime.Now;
                if (!isOutPlayer)
                {
                    wallet.GameMoney = player.GamMoney;
                    profile.CommonRating = player.Rating;
                    profile.BestMoneyLevel = checked((byte)player.NLevel);
                }
                wallet.PendingGameMoney = checked(wallet.PendingGameMoney + moneyChange);
                wallet.GemCount = checked(wallet.GemCount + gemCount);
                wallet.UpdatedAt = now;
                profile.Experience = checked(profile.Experience + experienceGain);
                profile.ConsecutiveWinLoss = ranking switch
                {
                    1 => checked(Math.Max(profile.ConsecutiveWinLoss, 0) + 1),
                    4 => checked(Math.Min(profile.ConsecutiveWinLoss, 0) - 1),
                    _ => 0,
                };
                profile.LastPlayedAt = now;
                profile.UpdatedAt = now;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                player.ContWinDefeat = profile.ConsecutiveWinLoss;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// MJKCOMMONRAT free-money charge update ? HMajDBObject::UpdateChargeFreeMoney.
    /// </summary>
    public virtual async Task<bool> UpdateChargeFreeMoneyAsync(MajakPlayer player)
    {
        try
        {
            var memberNoValue = ParseMemberNo(player.MemberNo);
            await using var strategyDb = await RequireGameDb().CreateAsync();
            var strategy = strategyDb.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var db = await RequireGameDb().CreateAsync();
                await using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                    var profile = await db.PlayerProfiles.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                    if (wallet is null || profile is null) return;

                    var now = DateTime.Now;
                    wallet.GameMoney = player.GamMoney;
                    wallet.UpdatedAt = now;
                    profile.CommonRating = player.Rating;
                    profile.BestMoneyLevel = checked((byte)player.NLevel);
                    profile.AllInCount = checked((uint)player.AllinCnt);
                    profile.LastAllInAt = player.LastAllinDt;
                    profile.LastPlayedAt = now;
                    profile.UpdatedAt = now;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Updates MJKCOMPETERAT for compete channels.
    /// Legacy reference: HMajDBObject::UpdateResult_Compete.
    /// </summary>
    public virtual async Task UpdateCompeteRatAsync(MajakPlayer player)
    {
        await UpdateModeStatsAsync(player.MemberNo, "compete", player.ChannelId, player.CompeteRecord, false, false);
    }

    /// <summary>
    /// Updates MJKHANGERAT for regular channels.
    /// Legacy reference: HMajDBObject::UpdateResult_Regular.
    /// </summary>
    public virtual async Task UpdateRegularRatAsync(MajakPlayer player)
    {
        await UpdateModeStatsAsync(player.MemberNo, "regular", player.ChannelId, player.RegularRecord, true, true);
    }

    /// <summary>
    /// Updates MJK_HICLASSRAT for high-class channels.
    /// Legacy reference: HMajDBObject::UpdateResult_HiClass.
    /// </summary>
    public virtual async Task UpdateHiClassRatAsync(MajakPlayer player, int gameScore, long moneyChange)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var strategyDb = await RequireGameDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireGameDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var stats = await db.PlayerModeStats
                    .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue && item.ModeCode == "high_class");
                if (stats is null) return;

                var now = DateTime.Now;
                ApplyModeStats(stats, player.ChannelId, player.HiClassRecord, true, true, false, now);
                var summary = await db.PlayerHighClassSummaries
                    .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                if (summary is null)
                {
                    summary = new PlayerHighClassSummaryEntity
                    {
                        MemberNo = memberNoValue,
                        CreatedAt = now,
                    };
                    db.PlayerHighClassSummaries.Add(summary);
                }
                summary.ScoreMax = !summary.ScoreMax.HasValue || gameScore > summary.ScoreMax ? gameScore : summary.ScoreMax;
                summary.ScoreMin = !summary.ScoreMin.HasValue || gameScore < summary.ScoreMin ? gameScore : summary.ScoreMin;
                summary.MoneyMax = !summary.MoneyMax.HasValue || moneyChange > summary.MoneyMax ? moneyChange : summary.MoneyMax;
                summary.MoneyMin = !summary.MoneyMin.HasValue || moneyChange < summary.MoneyMin ? moneyChange : summary.MoneyMin;
                summary.WinHandDoraMax = Math.Max(summary.WinHandDoraMax, player.HoraDoraMax);
                summary.ConsecutiveTopCurrent = player.HiClassRecord.Grade1 != 0
                    ? checked(summary.ConsecutiveTopCurrent + 1)
                    : 0;
                summary.ConsecutiveTopMax = Math.Max(summary.ConsecutiveTopMax, summary.ConsecutiveTopCurrent);
                summary.UpdatedAt = now;
                player.H_ContTopNow = checked((int)summary.ConsecutiveTopCurrent);
                player.H_ContTopMax = checked((int)summary.ConsecutiveTopMax);

                var persistedYaku = await db.PlayerHighClassYaku
                    .Where(item => item.MemberNo == memberNoValue)
                    .ToDictionaryAsync(item => item.YakuId);
                for (ushort index = 0; index < player.YakuCount.Length; index++)
                    SetYakuCount(db, persistedYaku, memberNoValue, index, player.YakuCount[index], now);
                for (ushort index = 0; index < player.YmanCount.Length; index++)
                    SetYakuCount(db, persistedYaku, memberNoValue, checked((ushort)(index + 100)), player.YmanCount[index], now);

                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    private static void SetYakuCount(
        GameDataContext db,
        Dictionary<ushort, PlayerHighClassYakuEntity> persisted,
        ulong memberNoValue,
        ushort yakuId,
        int count,
        DateTime now)
    {
        if (!persisted.TryGetValue(yakuId, out var yaku))
        {
            yaku = new PlayerHighClassYakuEntity { MemberNo = memberNoValue, YakuId = yakuId };
            db.PlayerHighClassYaku.Add(yaku);
        }
        yaku.Count = checked((uint)count);
        yaku.UpdatedAt = now;
    }

    /// <summary>
    /// Updates MJK_GRADERAT for grade channels.
    /// Legacy reference: HMajDBObject::UpdateResult_GradeMode.
    /// </summary>
    public virtual async Task UpdateGradeRatAsync(MajakPlayer player)
    {
        var rec = player.GradeRecord;
        await UpdateModeStatsAsync(player.MemberNo, "grade", player.ChannelId, rec, true, false, true);
    }

    private async Task UpdateModeStatsAsync(
        string memberNo,
        string modeCode,
        string channelId,
        RatingRecord record,
        bool includeNormalStats,
        bool includeTipStats,
        bool replaceRating = false)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var stats = await db.PlayerModeStats
            .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue && item.ModeCode == modeCode);
        if (stats is null) return;

        ApplyModeStats(stats, channelId, record, includeNormalStats, includeTipStats, replaceRating, DateTime.Now);
        await db.SaveChangesAsync();
    }

    private static void ApplyModeStats(
        PlayerModeStatsEntity stats,
        string channelId,
        RatingRecord record,
        bool includeNormalStats,
        bool includeTipStats,
        bool replaceRating,
        DateTime now)
    {
        stats.Rating = replaceRating ? record.Rating : checked(stats.Rating + record.Rating);
        stats.LastDisconnectAt = now;
        stats.LastChannelId = channelId;
        stats.MatchCount = AddUnsigned(stats.MatchCount, record.MatchCnt);
        stats.WinCount = AddUnsigned(stats.WinCount, record.WinCnt);
        stats.DefeatCount = AddUnsigned(stats.DefeatCount, record.DefeatCnt);
        stats.DrawCount = AddUnsigned(stats.DrawCount, record.DrawCnt);
        stats.FirstCount = AddUnsigned(stats.FirstCount, record.Grade1);
        stats.SecondCount = AddUnsigned(stats.SecondCount, record.Grade2);
        stats.ThirdCount = AddUnsigned(stats.ThirdCount, record.Grade3);
        stats.FourthCount = AddUnsigned(stats.FourthCount, record.Grade4);
        stats.TurnCount = AddUnsigned(stats.TurnCount, record.TurnCnt);
        stats.DealerCount = AddUnsigned(stats.DealerCount, record.DaidaCnt);
        stats.PointSum = checked(stats.PointSum + record.PointSum);
        stats.RoundCount = AddUnsigned(stats.RoundCount, record.KyokuCnt);
        stats.WinHandCount = AddUnsigned(stats.WinHandCount, record.HoraCnt);
        stats.WinHandPoints = checked(stats.WinHandPoints + record.HoraPoint);
        stats.DealInCount = AddUnsigned(stats.DealInCount, record.HojuCnt);
        stats.DealInPoints = checked(stats.DealInPoints + record.HojuPoint);
        stats.RiichiCount = AddUnsigned(stats.RiichiCount, record.RichiCnt);
        stats.MeldCount = AddUnsigned(stats.MeldCount, record.FuroCnt);
        if (includeNormalStats)
        {
            stats.BustCount = AddUnsigned(stats.BustCount, record.TobiCnt);
            stats.BustOtherCount = AddUnsigned(stats.BustOtherCount, record.TobashiCnt);
            stats.DoraCount = AddUnsigned(stats.DoraCount, record.DoraCnt);
            stats.UraDoraCount = AddUnsigned(stats.UraDoraCount, record.UraDoraCnt);
            stats.RiichiWinCount = AddUnsigned(stats.RiichiWinCount, record.RichiHoraCnt);
        }
        if (includeTipStats)
        {
            stats.TipPoint = checked(stats.TipPoint + record.TipPoint);
            stats.TipMatchCount = AddUnsigned(stats.TipMatchCount, record.TipMatchCnt);
        }
        if (replaceRating)
        {
            stats.GradeLevel = record.Grade;
            stats.GradePoint = record.GradePoint;
            stats.ExtraCount = AddUnsigned(stats.ExtraCount, record.TotExtraCount);
            if (record.TotExtraCount == 1) stats.LastExtraAt = now;
        }
        stats.UpdatedAt = now;
    }

    private static uint AddUnsigned(uint current, int delta)
        => checked((uint)(current + (long)delta));

    /// <summary>
    /// Loads owned titles from MJK_TITLELIST.
    /// </summary>
    public virtual async Task<List<string>> GetTitleListAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        return await db.PlayerTitles.AsNoTracking()
            .Where(item => item.MemberNo == memberNoValue)
            .Select(item => item.TitleId)
            .ToListAsync();
    }

    private const string SelectSerialMastsSql = @"
        SELECT /*+ INDEX(A EVTCODEMAST_IX1) */
               A.EVTCODE, A.EVTNO, A.EVTSTARTDT, A.EVTENDDT,
               B.GIFTCODE, NVL(B.GIFTVAL, 0), NVL(B.MISSIONNO, 0), NVL(B.GIFTMSG, ''),
               RAWTOHEX(UTL_RAW.CAST_TO_RAW(NVL(B.GIFTMSG, ''))) AS GIFTMSG_HEX
        FROM   EVTCODEMAST A
        JOIN   EVTGIFTMAST B ON B.EVTCODE = A.EVTCODE AND B.EVTNO = A.EVTNO
        WHERE  A.SVCID = :svcId
        AND    A.EVTSTARTDT <= SYSDATE + 1
        AND    A.EVTENDDT >= SYSDATE
        ORDER BY A.EVTCODE, A.EVTNO";

    private const string CallCasualPointUpdMissionSql = @"
        CALL CASUALPOINT.PC_UPDMISSION
        (:oszGameId, :onCondType, :onCondSubType, :oszMemberNo, :onCnt, :odtProcDt, :onRtnVal)";

    private const string SelectEvtCodeMastSql = @"
        SELECT EVTCODE, EVTNO, EVTNAME, EVTDESC, SVCID, EVTTBLINFO, EVTSTARTDT, EVTENDDT
        FROM   EVTCODEMAST
        WHERE  EVTCODE = :vcEvtCode
        AND    EVTNO = :inEvtNo";

    /// <summary>
    /// MJK_TITLELIST active-title existence check ? HMajDBObject::HaveTitle.
    /// </summary>
    public virtual async Task<bool> HasActiveTitleAsync(string memberNo, string titleId)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        return await db.PlayerTitles.AsNoTracking()
            .AnyAsync(item => item.MemberNo == memberNoValue && item.TitleId == titleId);
    }

    /// <summary>
    /// Inserts a MJK_TITLELIST row or enables an existing title for InsertMajakTitle.
    /// </summary>
    public virtual async Task InsertOrEnableTitleAsync(string memberNo, string titleId)
    {
        await using var db = await RequireGameDb().CreateAsync();
        await InsertMissingTitlesAsync(db, memberNo, [titleId]);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// MJK_TITLELIST batch insert/update ? HMajDBObject::MissionClear.
    /// </summary>
    public virtual async Task InsertOrEnableTitlesAsync(string memberNo, IEnumerable<string> titleIds)
    {
        await using var db = await RequireGameDb().CreateAsync();
        await InsertMissingTitlesAsync(db, memberNo, titleIds);
        await db.SaveChangesAsync();
    }

    private static async Task InsertMissingTitlesAsync(GameDataContext db, string memberNo, IEnumerable<string> titleIds)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        string[] requested = titleIds.Distinct().ToArray();
        if (requested.Length == 0) return;
        var existing = (await db.PlayerTitles
            .Where(item => item.MemberNo == memberNoValue && requested.Contains(item.TitleId))
            .Select(item => item.TitleId)
            .ToListAsync()).ToHashSet();
        var now = DateTime.Now;
        foreach (string titleId in requested.Where(titleId => !existing.Contains(titleId)))
            db.PlayerTitles.Add(new PlayerTitleEntity
            {
                MemberNo = memberNoValue,
                TitleId = titleId,
                AcquiredAt = now,
            });
    }

    /// <summary>
    /// Loads all MJK_TITLEMAST rows for GetMajTitleInfo.
    /// </summary>
    public virtual async Task<Dictionary<string, string>> GetTitleMastAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        return await db.TitleMasters.AsNoTracking()
            .ToDictionaryAsync(title => title.TitleId, title => title.TitleName);
    }

    /// <summary>
    /// Loads MJKUSERSKINLIST state for GetUserSkinList.
    /// </summary>
    public virtual async Task LoadSkinListAsync(MajakPlayer player)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var now = DateTime.Now;
        var skins = await db.PlayerSkins.AsNoTracking()
            .Where(skin => skin.MemberNo == memberNoValue && skin.ExpiresAt > now)
            .OrderBy(skin => skin.SkinNo)
            .ToListAsync();
        player.SkinList.Clear();
        foreach (var skin in skins)
        {
            player.SkinList.Add(new SkinInfo
            {
                SkinNo = checked((int)skin.SkinNo),
                AttachFlag = skin.IsAttached,
                EndDate = skin.ExpiresAt,
            });
        }
    }

    /// <summary>
    /// MJKUSERSHOPLIST MERGE ? UpdateMemorialShopRat
    /// </summary>
    public virtual async Task UpsertShopListAsync(string memberNo, int shopId)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        ushort normalizedShopId = checked((ushort)shopId);
        var shop = await db.PlayerShops.FindAsync(memberNoValue, normalizedShopId);
        if (shop is null)
        {
            var now = DateTime.Now;
            db.PlayerShops.Add(new PlayerShopEntity
            {
                MemberNo = memberNoValue,
                ShopId = normalizedShopId,
                CreatedAt = now,
                OpenedAt = now,
            });
        }
        else
            shop.OpenedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// GAMEICON award hook ? HMajRoomServer::GetGameIcon.
    /// Legacy source in this archive exposes the award thresholds but not the backing table SQL.
    /// </summary>
    public virtual Task GrantGameIconAsync(string memberNo, string iconCode)
        => Task.CompletedTask;

    // Item lookup

    /// <summary>Looks up an item by sell code and item code from MJK_ITEMLIST for GetGem.</summary>
    public async Task<ItemInfo?> GetItemInfoAsync(string sellCode, string itemCode)
        => await Task.FromResult<ItemInfo?>(null);

    // Grade ranking

    /// <summary>
    /// Loads the MJK_GRADERAT ranking list up to maxCnt rows.
    /// </summary>
    public virtual async Task<List<GradeRankItem>> GetGradeRankListAsync(int rankDate, int rankKind, int maxCnt)
    {
        // Check the Redis cache with a five-minute TTL.
        string cacheKey = MasterCacheService.KeyGradeRankList(rankDate, rankKind, maxCnt);
        var cached = await Redis.GetJsonAsync<List<GradeRankItem>>(cacheKey);
        if (cached is { Count: > 0 }) return cached;

        await using var db = await RequireGameDb().CreateAsync();
        var month = RankMonth(rankDate);
        var query = db.PlayerGradeRanks.AsNoTracking().Where(rank => rank.RankDate == month);
        if (rankKind > 0) query = query.Where(rank => rank.GradeLevel == rankKind);
        var rows = await query.OrderByDescending(rank => rank.Rating)
            .ThenBy(rank => rank.LastPlayedAt)
            .Take(maxCnt)
            .ToListAsync();
        var list = rows.Select((rank, index) => new GradeRankItem
        {
            MemberNo = MemberNoIds.Format(rank.MemberNo),
            AvatarId = rank.AvatarId,
            Rating = rank.Rating,
            Grade = rank.GradeLevel,
            LastDate = rank.LastPlayedAt?.ToString("yyyy/MM/dd HH:mm:ss") ?? "",
            ExtraCount = checked((int)rank.ExtraCount),
            Rank = index + 1,
        }).ToList();

        await Redis.SetJsonAsync(cacheKey, list, MasterCacheService.TtlRanking);
        return list;
    }

    /// <summary>Loads the current player's grade rank.</summary>
    public virtual async Task<GradeRankItem?> GetGradeRankSelfAsync(string memberNo, int rankDate, int grade)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        // Check the Redis cache with a five-minute TTL.
        string cacheKey = MasterCacheService.KeyGradeRankSelf(rankDate, memberNo, grade);
        var cached = await Redis.GetJsonAsync<GradeRankItem>(cacheKey);
        if (cached is not null) return cached;

        await using var db = await RequireGameDb().CreateAsync();
        var month = RankMonth(rankDate);
        var rank = await db.PlayerGradeRanks.AsNoTracking()
            .FirstOrDefaultAsync(item => item.MemberNo == memberNoValue && item.RankDate == month);
        if (rank is null) return null;
        var item = new GradeRankItem
        {
            MemberNo = MemberNoIds.Format(rank.MemberNo),
            AvatarId = rank.AvatarId,
            Rating = rank.Rating,
            Grade = rank.GradeLevel,
            LastDate = rank.LastPlayedAt?.ToString("yyyy/MM/dd HH:mm:ss") ?? "",
            ExtraCount = checked((int)rank.ExtraCount),
            Rank = rank.RankPosition ?? 0,
        };
        await Redis.SetJsonAsync(cacheKey, item, MasterCacheService.TtlRanking);
        return item;
    }

    /// <summary>Loads the grade management list of selectable rank periods.</summary>
    public virtual async Task<List<GradeSelectItem>> GetGradeManageListAsync()
    {
        // Check the Redis cache with a one-hour TTL. This is normally called through MasterCacheService,
        // but direct command calls can reuse the same cache key.
        var cached = await Redis.GetJsonAsync<List<GradeSelectItem>>(MasterCacheService.KeyGradeManage);
        if (cached is not null) return cached;

        await using var db = await RequireGameDb().CreateAsync();
        var rankDates = await db.GradeRankSchedules.AsNoTracking()
            .OrderByDescending(schedule => schedule.RankDate)
            .Select(schedule => schedule.RankDate)
            .ToListAsync();
        var list = rankDates.Select((rankDate, index) => new GradeSelectItem
        {
            DispOrder = index + 1,
            YearMonth = rankDate.Year * 100 + rankDate.Month,
            YearMonthStr = rankDate.ToString("yyyyMM"),
        }).ToList();
        await Redis.SetJsonAsync(MasterCacheService.KeyGradeManage, list, TimeSpan.FromHours(1));
        return list;
    }

    /// <summary>
    /// MJK_GRADERANK MERGE ? HMajDBObject::UpdateResult_GradeMode rank rows.
    /// </summary>
    public virtual async Task MergeGradeRankAsync(IEnumerable<GradeRankUpdateItem> rows)
    {
        await ExecuteGameTransactionAsync(async (db, tx) =>
        {
        var now = DateTime.Now;
        foreach (var row in rows)
        {
            var month = RankMonth(row.RankDate);
            byte kind = checked((byte)row.RankKind);
            var memberNoValue = ParseMemberNo(row.MemberNo);
            var rank = await db.PlayerGradeRanks.FindAsync(month, kind, memberNoValue);
            if (rank is null)
            {
                rank = new PlayerGradeRankEntity
                {
                    RankDate = month,
                    RankKind = kind,
                    MemberNo = memberNoValue,
                    CreatedAt = now,
                };
                db.PlayerGradeRanks.Add(rank);
            }
            rank.Rating = row.Rating;
            rank.GradeLevel = row.Grade;
            rank.LastPlayedAt = now;
            rank.ExtraCount = checked(rank.ExtraCount + (uint)row.ExtraCount);
            if (row.ExtraCount == 1) rank.LastExtraAt = now;
            rank.AvatarId = row.AvatarId;
            rank.DisplayFlag = checked((byte)row.DispFlag);
            rank.UpdatedAt = now;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        });
    }

    /// <summary>
    /// EVTUSERMAST \u7167\u4f1a \u2014 GetMemberEventInfo \u76f8\u5f53
    /// \u539f\u5178: ProcessCommand_EventInfo \u2192 HMajDBObject::GetMemberEventInfo
    /// \u30d7\u30ec\u30a4\u30e4\u30fc\u306e\u30a4\u30d9\u30f3\u30c8\u9032\u6357 (EVTCODE, EVTNO, LASTDT, EXTRAUSEVAL1) \u3092\u8fd4\u3059
    /// </summary>
    public virtual async Task<List<EventInfo>> GetMemberEventInfoAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var events = await db.EventUsers.AsNoTracking()
            .Where(evt => evt.MemberNo == memberNoValue)
            .OrderBy(evt => evt.EventCode).ThenBy(evt => evt.EventNo)
            .ToListAsync();
        return events.Select(evt => new EventInfo
        {
            EvtCode = evt.EventCode,
            EvtNo = checked((int)evt.EventNo),
            ExtraVal1 = checked((int)evt.ExtraValue1),
            ExtraVal2 = checked((int)evt.ExtraValue2),
            ExtraVal3 = checked((int)evt.ExtraValue3),
            ExtraVal4 = checked((int)evt.ExtraValue4),
            ExtraVal5 = checked((int)evt.ExtraValue5),
            LastDt = evt.LastActivityAt?.ToString("yyyyMMdd") ?? "",
            RegDt = evt.RegisteredAt.ToString("yyyyMMdd"),
        }).ToList();
    }

    /// <summary>
    /// MJKUSERSHOPLIST \u7167\u4f1a \u2014 \u8a18\u5ff5\u30b7\u30e7\u30c3\u30d7\u6240\u6301\u4e00\u89a7
    /// \u539f\u5178: UpdateMemorialShopRat \u5bfe\u5fdc (SELECT \u7d71)
    /// </summary>
    public async Task<List<int>> GetUserShopListAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        return await db.PlayerShops.AsNoTracking()
            .Where(shop => shop.MemberNo == memberNoValue)
            .OrderBy(shop => shop.ShopId)
            .Select(shop => checked((int)shop.ShopId))
            .ToListAsync();
    }

    /// <summary>
    /// Looks up today's daily mission completion state from MJK_DAILYMISSIONLIST.
    /// Legacy reference: HMajDBObject::SelectMjkDailyMissionList.
    /// </summary>
    public virtual async Task<Dictionary<int, int>> GetDailyMissionListAsync(string memberNo)
    {
        // Check the Redis cache until midnight. SetDailyMissionAsync invalidates related state.
        string cacheKey = $"majak2:player:{memberNo}:daily:{DateTime.Today:yyyyMMdd}";
        var cached = await Redis.GetJsonAsync<Dictionary<int, int>>(cacheKey);
        if (cached is not null) return cached;

        var map = await GetDailyMissionListForTodayAsync(memberNo);
        await Redis.SetJsonAsync(cacheKey, map, TtlUntilMidnight());
        return map;
    }

    /// <summary>MJK_DAILYMISSIONMAST lookup ? HMajDBObject::SelectMjkDailyMissionMast.</summary>
    public virtual async Task<Dictionary<int, DailyMissionMastInfo>> GetDailyMissionMastAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        return await db.DailyMissionMasters.AsNoTracking()
            .Select(mission => new DailyMissionMastInfo
            {
                MissionId = mission.MissionId,
                ConditionType = mission.ConditionType,
                ConditionCnt = mission.ConditionCount,
                Point = mission.Point,
            })
            .ToDictionaryAsync(mission => mission.MissionId);
    }

    /// <summary>MJK_DAILYMISSIONLIST lookup for mjkc32e, bypassing Redis.</summary>
    public virtual async Task<Dictionary<int, int>> GetDailyMissionListForTodayAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var today = DateTime.Today;
        return await db.PlayerDailyMissions.AsNoTracking()
            .Where(mission => mission.MemberNo == memberNoValue && mission.UpdatedAt >= today)
            .ToDictionaryAsync(mission => (int)mission.MissionId, mission => (int)mission.MissionState);
    }

    /// <summary>
    /// Looks up this week's reward receive state from MJK_WEEKLYREWARDLIST.
    /// Legacy reference: HMajDBObject::SelectMjkWeeklyRewardList.
    /// </summary>
    public virtual async Task<Dictionary<int, int>> GetWeeklyRewardListAsync(string memberNo)
    {
        // Check the Redis cache until next Monday. TryReceiveWeeklyRewardAsync invalidates related state.
        string cacheKey = $"majak2:player:{memberNo}:weekly:{WeekStartKey()}";
        var cached = await Redis.GetJsonAsync<Dictionary<int, int>>(cacheKey);
        if (cached is not null) return cached;

        var map = await GetWeeklyRewardListForWeekAsync(memberNo);
        await Redis.SetJsonAsync(cacheKey, map, TtlUntilNextMonday());
        return map;
    }

    /// <summary>MJK_WEEKLYREWARDLIST lookup for mjkc32e, bypassing Redis.</summary>
    public virtual async Task<Dictionary<int, int>> GetWeeklyRewardListForWeekAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var week = CurrentWeekStart();
        return await db.PlayerWeeklyRewards.AsNoTracking()
            .Where(reward => reward.MemberNo == memberNoValue && reward.RewardWeek == week)
            .ToDictionaryAsync(reward => (int)reward.RewardId, reward => (int)reward.ReceiveStatus);
    }

    /// <summary>MJK_WEEKLYREWARDLIST single lookup for mjkc33e, bypassing Redis.</summary>
    public virtual async Task<int?> GetWeeklyRewardStatusForWeekAsync(string memberNo, int rewardId)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        byte id = checked((byte)rewardId);
        var week = CurrentWeekStart();
        return await db.PlayerWeeklyRewards.AsNoTracking()
            .Where(reward => reward.MemberNo == memberNoValue && reward.RewardWeek == week && reward.RewardId == id)
            .Select(reward => (int?)reward.ReceiveStatus)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Looks up the weekly point total from completed daily missions this week.
    /// Legacy reference: HMajChnlServer::GetWeeklyPoint.
    /// </summary>
    public virtual async Task<int> GetWeeklyPointAsync(string memberNo)
    {
        // Check the Redis cache until next Monday. SetDailyMissionAsync invalidates related state.
        string cacheKey = $"majak2:player:{memberNo}:weeklypoint:{WeekStartKey()}";
        var cachedPt = await Redis.GetJsonAsync<int?>(cacheKey);
        if (cachedPt.HasValue) return cachedPt.Value;

        int point = await GetWeeklyPointForWeekAsync(memberNo);
        await Redis.SetJsonAsync(cacheKey, (int?)point, TtlUntilNextMonday());
        return point;
    }

    /// <summary>MJKCOMMONRAT.WEEKLYPOINT lookup for mjkc32e, bypassing Redis.</summary>
    public virtual async Task<int> GetWeeklyPointForWeekAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var week = CurrentWeekStart();
        return await db.PlayerProfiles.AsNoTracking()
            .Where(profile => profile.MemberNo == memberNoValue && profile.WeeklyTargetDate >= week)
            .Select(profile => profile.WeeklyPoint)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Loads weekly reward master rows from MJK_WEEKLYREWARDMAST.
    /// Legacy reference: HMajDBObject::SelectMjkWeeklyRewardMast.
    /// </summary>
    public virtual async Task<Dictionary<int, WeeklyRewardMastInfo>> GetWeeklyRewardMastAsync()
    {
        // Use the same one-hour Redis cache key as MasterCacheService.
        // Direct calls can reuse this cache without going through the master cache service.
        var cachedDto = await Redis.GetJsonAsync<List<WeeklyRewardMastSimpleDto>>(MasterCacheService.KeyWeeklyMast);
        if (cachedDto is { Count: > 0 })
            return cachedDto.ToDictionary(x => x.RewardId,
                x => new WeeklyRewardMastInfo { RewardId = x.RewardId, RewardType = x.RewardType, RewardCnt = x.RewardCnt, MustPoint = x.MustPoint });

        await using var db = await RequireGameDb().CreateAsync();
        var map = await db.WeeklyRewardMasters.AsNoTracking()
            .OrderBy(reward => reward.RewardId)
            .Select(reward => new WeeklyRewardMastInfo
            {
                RewardId = reward.RewardId,
                RewardType = reward.RewardType,
                RewardCnt = checked((int)reward.RewardCount),
                MustPoint = reward.RequiredPoint,
            })
            .ToDictionaryAsync(reward => reward.RewardId);
        // Store in Redis.
        var dto = map.Values.Select(v => new WeeklyRewardMastSimpleDto(v.RewardId, v.RewardType, v.RewardCnt, v.MustPoint)).ToList();
        await Redis.SetJsonAsync(MasterCacheService.KeyWeeklyMast, dto, TimeSpan.FromHours(1));
        return map;
    }

    /// <summary>MJK_WEEKLYREWARDMAST lookup for mjkc32e, bypassing Redis.</summary>
    public virtual async Task<Dictionary<int, WeeklyRewardMastInfo>> GetWeeklyRewardMastDirectAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        return await db.WeeklyRewardMasters.AsNoTracking()
            .Select(reward => new WeeklyRewardMastInfo
            {
                RewardId = reward.RewardId,
                RewardType = reward.RewardType,
                RewardCnt = checked((int)reward.RewardCount),
                MustPoint = reward.RequiredPoint,
            }).ToDictionaryAsync(reward => reward.RewardId);
    }

    // DTO used by PlayerRepository to serialize weekly reward master cache entries.
    private record WeeklyRewardMastSimpleDto(int RewardId, int RewardType, int RewardCnt, int MustPoint);

    private GameDataContextFactory RequireGameDb()
        => _gameDb ?? throw new InvalidOperationException("MySQL GameDataContextFactory is not configured.");

    private LogRepository RequireLog()
        => _log ?? throw new InvalidOperationException("MySQL LogRepository is not configured.");

    private RedisService Redis
        => _redis ?? throw new InvalidOperationException("RedisService is not configured.");

    private async Task InsertGameMoneyHistFromTransactionCodeAsync(
        string memberNo,
        string eventCode,
        long eventMoney,
        long preMoney,
        long afterMoney,
        string remoteAddress,
        string? orderNumber = null)
    {
        if (_gameDb is null || _log is null) return;

        var metadata = await TransactionCodeMetadataResolver.ResolveAsync(_gameDb, eventCode);
        if (metadata is null) return;

        await _log.InsertGameMoneyHistAsync(
            memberNo, eventCode, eventMoney, preMoney, afterMoney, remoteAddress,
            metadata.EventTitle, orderNumber, metadata.GameId, metadata.IsHistoryEnabled);
    }

    /// <summary>
    /// Marks a MJK_WEEKLYREWARDLIST row as received.
    /// Legacy reference: HMajDBObject::Reflect / ReflectWeeklyReward.
    /// Returns true for newly received rewards, false for already received rewards or errors.
    /// </summary>
    public virtual async Task<bool> TryReceiveWeeklyRewardAsync(string memberNo, int rewardId)
    {
        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            await using var db = await RequireGameDb().CreateAsync();
            byte targetRewardId = checked((byte)rewardId);
            var week = CurrentWeekStart();
            var reward = await db.PlayerWeeklyRewards
                .SingleOrDefaultAsync(r => r.MemberNo == memberNoValue && r.RewardWeek == week && r.RewardId == targetRewardId);
            bool isNew = reward is null;
            bool wasNotReceived = reward is not null && reward.ReceiveStatus == 0;
            if (reward is null)
            {
                reward = new PlayerWeeklyRewardEntity
                {
                    MemberNo = memberNoValue,
                    RewardWeek = week,
                    RewardId = targetRewardId,
                    ReceiveStatus = 1,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                };
                db.PlayerWeeklyRewards.Add(reward);
            }
            else if (reward.ReceiveStatus == 0)
            {
                reward.ReceiveStatus = 1;
                reward.UpdatedAt = DateTime.Now;
            }
            await db.SaveChangesAsync();
            if (isNew || wasNotReceived)
            {
                // Invalidate the weekly reward cache after DB state is committed.
                await Redis.InvalidateAsync($"majak2:player:{memberNo}:weekly:{WeekStartKey()}");
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>
    /// HMajDBObject::Reflect(HMajPlayer, MJK_WEEKLYREWARDLIST) equivalent.
    /// player_weekly_reward and player_wallet are updated in one MySQL transaction.
    /// </summary>
    public virtual async Task<bool> ReflectWeeklyRewardAsync(MajakPlayer player, int rewardId, int receiveStatus, DateTime modiDt)
    {
        try
        {
            var memberNoValue = ParseMemberNo(player.MemberNo);
            bool reflected = await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            byte targetRewardId = checked((byte)rewardId);
            var week = CurrentWeekStart();
            var reward = await db.PlayerWeeklyRewards
                .SingleOrDefaultAsync(r => r.MemberNo == memberNoValue && r.RewardWeek == week && r.RewardId == targetRewardId);
            if (reward is null)
            {
                reward = new PlayerWeeklyRewardEntity
                {
                    MemberNo = memberNoValue,
                    RewardWeek = week,
                    RewardId = targetRewardId,
                    ReceiveStatus = checked((byte)receiveStatus),
                    CreatedAt = modiDt,
                    UpdatedAt = modiDt,
                };
                db.PlayerWeeklyRewards.Add(reward);
            }
            else
            {
                reward.ReceiveStatus = checked((byte)receiveStatus);
                reward.UpdatedAt = modiDt;
            }
            var wallet = await db.PlayerWallets.SingleOrDefaultAsync(w => w.MemberNo == memberNoValue);
            if (wallet is null)
            {
                await tx.RollbackAsync();
                return false;
            }
            wallet.GameMoney = player.GamMoney;
            wallet.GemCount = player.GemCount;
            wallet.UpdatedAt = DateTime.Now;
            var profile = await db.PlayerProfiles.SingleOrDefaultAsync(p => p.MemberNo == memberNoValue);
            if (profile is not null)
            {
                profile.BestMoneyLevel = checked((byte)player.NLevel);
                profile.UpdatedAt = DateTime.Now;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
            });
            if (reflected)
                await Redis.InvalidateAsync($"majak2:player:{player.MemberNo}:weekly:{WeekStartKey()}");
            return reflected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads currently active serial master rows from EVTCODEMAST and EVTGIFTMAST.
    /// Legacy reference: SerialMastMgr::GetSerialMast via SelectEvtCodeMast(nEvtStartDtGap=1) and SelectEvtGiftMast.
    /// </summary>
    public virtual async Task<List<SerialMastInfo>> GetSerialMastsAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        var now = DateTime.Now;
        var tomorrow = now.AddDays(1);
        var query = from evt in db.EventMasters.AsNoTracking()
                    join gift in db.EventGiftMasters.AsNoTracking()
                        on new { evt.EventCode, evt.EventNo } equals new { gift.EventCode, gift.EventNo }
                    where evt.ServiceId == GameConst.ServiceId
                       && evt.StartsAt <= tomorrow
                       && evt.EndsAt >= now
                    orderby evt.EventCode, evt.EventNo
                    select new SerialMastInfo
                    {
                        EvtCode = evt.EventCode,
                        EvtNo = checked((int)evt.EventNo),
                        EvtStartDt = evt.StartsAt ?? DateTime.MinValue,
                        EvtEndDt = evt.EndsAt ?? DateTime.MinValue,
                        GiftCode = gift.GiftCode,
                        GiftValue = checked((int)(gift.GiftValue ?? 0)),
                        MissionNo = gift.MissionNo,
                        GiftMessage = gift.GiftMessage ?? "",
                    };
        return await query.ToListAsync();
    }

    public virtual async Task<bool> SerialExchangeItemExistsAsync(
        string evtCode, int evtNo, string memberNo, string giftCode)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        uint eventNo = checked((uint)evtNo);
        return await db.SerialExchangeItems.AsNoTracking()
            .AnyAsync(item => item.EventCode == evtCode
                           && item.EventNo == eventNo
                           && item.ServiceId == GameConst.ServiceId
                           && item.MemberNo == memberNoValue
                           && item.GiftCode == giftCode);
    }

    public virtual async Task<bool> InsertSerialExchangeItemAsync(
        string evtCode, int evtNo, string memberNo, string giftCode, int giftValue)
    {
        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            await using var db = await RequireGameDb().CreateAsync();
            db.SerialExchangeItems.Add(new SerialExchangeItemEntity
            {
                EventCode = evtCode,
                EventNo = checked((uint)evtNo),
                ServiceId = GameConst.ServiceId,
                MemberNo = memberNoValue,
                GiftCode = giftCode,
                GiftValue = giftValue,
                UpdatedAt = DateTime.Now,
            });
            await db.SaveChangesAsync();
            return true;
        }
        catch { return false; }
    }

    public virtual async Task<SerialCouponInfo?> GetSerialCouponAsync(
        string evtCode, int evtNo, int missionNo, string couponNo)
    {
        await using var db = await RequireGameDb().CreateAsync();
        uint eventNo = checked((uint)evtNo);
        var coupon = await db.SerialCoupons.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventCode == evtCode
                                    && c.EventNo == eventNo
                                    && c.MissionNo == missionNo
                                    && c.CouponNo == couponNo);
        return coupon is null ? null : new SerialCouponInfo { MemberNo = MemberNoIds.Format(coupon.MemberNo) };
    }

    public virtual async Task<bool> UpdateSerialCouponMemberAsync(
        string evtCode, int evtNo, int missionNo, string couponNo, string memberNo)
    {
        await using var db = await RequireGameDb().CreateAsync();
        var memberNoValue = ParseMemberNo(memberNo);
        uint eventNo = checked((uint)evtNo);
        int rows = await db.SerialCoupons
            .Where(c => c.EventCode == evtCode
                     && c.EventNo == eventNo
                     && c.MissionNo == missionNo
                     && c.CouponNo == couponNo
                     && c.MemberNo == null)
            .ExecuteUpdateAsync(setters => setters
                  .SetProperty(c => c.MemberNo, memberNoValue)
                .SetProperty(c => c.UpdatedAt, DateTime.Now));
        return rows > 0;
    }

    public virtual async Task UpdateCommonRatSerialResourceAsync(MajakPlayer player)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var db = await RequireGameDb().CreateAsync();
        await db.PlayerWallets
            .Where(w => w.MemberNo == memberNoValue)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(w => w.GameMoney, player.GamMoney)
                .SetProperty(w => w.GemCount, player.GemCount)
                .SetProperty(w => w.UpdatedAt, DateTime.Now));
        await db.PlayerProfiles
            .Where(p => p.MemberNo == memberNoValue)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.BestMoneyLevel, checked((byte)player.NLevel))
                .SetProperty(p => p.UpdatedAt, DateTime.Now));
    }

    public virtual async Task<bool> AddSerialBonusItemAsync(MajakPlayer player, string itemCode = "MJ20", int validDays = 0, int quantity = 12)
        => await MergeMajItemAndRefreshAsync(player, itemCode, validDays, quantity);

    private async Task<bool> MergeMajItemAndRefreshAsync(
        MajakPlayer player,
        string itemCode,
        int validDays,
        int quantity)
    {
        if (string.IsNullOrEmpty(player.MemberNo) || string.IsNullOrEmpty(itemCode)) return false;

        try
        {
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var memberNoValue = ParseMemberNo(player.MemberNo);
            var now = DateTime.Now;
            var item = await db.PlayerFunctionItems.FindAsync(memberNoValue, itemCode);
            if (item is null)
            {
                item = new PlayerFunctionItemEntity
                {
                    MemberNo = memberNoValue,
                    ItemCode = itemCode,
                    BoughtAt = now,
                    ExpiresAt = validDays > 0 ? now.AddDays(validDays) : new DateTime(2037, 1, 1),
                    Quantity = checked((uint)Math.Max(quantity, 1)),
                    IsEquipped = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.PlayerFunctionItems.Add(item);
            }
            else
            {
                bool active = item.ExpiresAt.HasValue && item.ExpiresAt.Value > now;
                if (!active) item.BoughtAt = now;
                var expiryBase = active ? item.ExpiresAt!.Value : now;
                item.ExpiresAt = validDays > 0 ? expiryBase.AddDays(validDays) : new DateTime(2037, 1, 1);
                item.Quantity = checked(item.Quantity + (uint)quantity);
                item.IsEquipped = true;
                item.UpdatedAt = now;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var playerItem = new MajItemInfo
            {
                ItemCode = item.ItemCode,
                BuyDt = item.BoughtAt,
                EndDt = item.ExpiresAt ?? new DateTime(2037, 1, 1),
                Qty = checked((int)item.Quantity),
                UseFlag = item.IsEquipped,
            };
            int index = player.MajItems.FindIndex(existing => existing.ItemCode == itemCode);
            if (index >= 0) player.MajItems[index] = playerItem;
            else player.MajItems.Add(playerItem);
            return true;
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads an event row by EVTCODE and EVTNO from EVTCODEMAST.
    /// Legacy reference: HMajDBObject::SelectEvtCodeMast (HMajDBObject.cpp:5303).
    /// HMajRootServer preloads EVTCODE='5221' EVTNO=0 for _LIMIT_PLAY_TIME.
    /// </summary>
    public virtual async Task<EvtCodeMastInfo?> GetEvtCodeMastAsync(string evtCode, int evtNo)
    {
        await using var db = await RequireGameDb().CreateAsync();
        uint eventNo = checked((uint)evtNo);
        var evt = await db.EventMasters.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EventCode == evtCode && item.EventNo == eventNo);
        return evt is null ? null : new EvtCodeMastInfo
        {
            EvtCode = evt.EventCode,
            EvtNo = checked((int)evt.EventNo),
            EvtName = evt.EventName,
            EvtDesc = evt.Description,
            SvcId = evt.ServiceId,
            EvtTblInfo = evt.TableInfo,
            EvtStartDt = evt.StartsAt ?? DateTime.MinValue,
            EvtEndDt = evt.EndsAt ?? DateTime.MinValue,
        };
    }

    /// <summary>
    /// Loads admin ID list support.
    /// Legacy reference: HMajDBObject::LoadAdminIdInfo / HMajAdminId.h (_USE_ADMIN_ID).
    /// Returns an empty list if the backing table has no rows.
    /// </summary>
    public virtual async Task<List<AdminIdInfo>> GetAdminIdListAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        var admins = await db.GameAdminMembers.AsNoTracking()
            .Where(admin => admin.IsActive)
            .OrderBy(admin => admin.MemberNo)
            .ToListAsync();
        return admins.Select(admin => new AdminIdInfo
        {
            MemberNo = MemberNoIds.Format(admin.MemberNo),
            AdminSts = checked((int)admin.AdminStatus),
        }).ToList();
    }

    /// <summary>
    /// Loads cup channel config support.
    /// Legacy reference: GetMajCupChannelConfig (MAJAKCUPMAST).
    /// Returns an empty list if the backing tables have no rows.
    /// </summary>
    public virtual async Task<List<MajakServer.Infrastructure.CupConfig>> GetCupConfigsAsync()
    {
        // Check the Redis cache with a two-minute TTL. CupChannelBackgroundService uses this through MasterCacheService.
        var cachedDto = await Redis.GetJsonAsync<List<CupConfigSimpleDto>>(MasterCacheService.KeyCupConfigs);
        if (cachedDto is not null)
            return cachedDto.Select(c => new MajakServer.Infrastructure.CupConfig(
                c.ChannelId, c.ChannelName, c.DateFrom, c.DateTo, c.IsFestive,
                c.CupId, c.CupSeq, c.JudgementType, c.CupPointSumType,
                c.MaxMatchCntLimit, c.ConditionRegular,
                c.EntryLimited, c.ConditionBilling, c.MinLevel, c.MaxLevel,
                c.NormalYakuCondition, c.YakumanCondition)).ToList();

        await using var db = await RequireGameDb().CreateAsync();
        var now = DateTime.Now;
        var regularCups = await (
            from cup in db.CupMasters.AsNoTracking()
            join link in db.CupChannels.AsNoTracking() on cup.CupId equals link.CupId
            join channel in db.ChannelMasters.AsNoTracking() on link.ChannelId equals channel.ChannelId
            join rule in db.RuleMasters.AsNoTracking() on cup.RuleId equals rule.RuleId
            where cup.IsActive && cup.StartAt <= now && cup.EndAt >= now && cup.Status <= 1
            select new { cup, channel, rule })
            .ToListAsync();

        var result = regularCups.Select(row => new MajakServer.Infrastructure.CupConfig(
            ChannelId: row.channel.ChannelId,
            ChannelName: string.IsNullOrEmpty(row.cup.ShortCupName) ? row.channel.ChannelId : row.cup.ShortCupName,
            DateFrom: row.cup.StartAt,
            DateTo: row.cup.EndAt,
            IsFestive: row.channel.SubId.Length >= 5 && row.channel.SubId[2] == 'C' && row.channel.SubId[4] == 'A',
            CupId: checked((int)row.cup.CupId),
            CupSeq: 0,
            JudgementType: row.rule.JudgementType,
            CupPointSumType: 0,
            MaxMatchCntLimit: row.cup.ConditionMatchCount,
            ConditionRegular: row.cup.ConditionRegular,
            EntryLimited: false,
            ConditionBilling: 0,
            MinLevel: 0,
            MaxLevel: 0,
            NormalYakuCondition: row.rule.NormalYakuCondition,
            YakumanCondition: row.rule.YakumanCondition)).ToList();

        var eventCups = await (
            from plan in db.TournamentPlanMasters.AsNoTracking()
            join rule in db.RuleMasters.AsNoTracking() on plan.RuleId equals rule.RuleId
            where plan.IsValid && plan.StartAt <= now && plan.EndAt >= now && plan.Status <= 1
            select new { plan, rule })
            .ToListAsync();
        var festiveChannels = await db.ChannelMasters.AsNoTracking()
            .Where(channel => channel.GameId == GameConst.ServiceId && channel.IsActive && channel.SubId.Length >= 5 && channel.SubId.Substring(4, 1) == "F")
            .Select(channel => channel.ChannelId)
            .ToListAsync();
        foreach (var row in eventCups)
        foreach (string channelId in festiveChannels)
        {
            result.Add(new MajakServer.Infrastructure.CupConfig(
                ChannelId: channelId,
                ChannelName: string.IsNullOrEmpty(row.plan.CupName) ? channelId : row.plan.CupName,
                DateFrom: row.plan.StartAt,
                DateTo: row.plan.EndAt,
                IsFestive: true,
                CupId: checked((int)row.plan.CupId),
                CupSeq: checked((int)row.plan.Sequence),
                JudgementType: 8,
                CupPointSumType: checked((int)(row.rule.EventSumType ?? 0)),
                MaxMatchCntLimit: row.plan.MaxMatchCount,
                ConditionRegular: 0,
                EntryLimited: row.plan.IsFinal,
                ConditionBilling: row.plan.BillingStatus,
                MinLevel: row.plan.MinLevel,
                MaxLevel: row.plan.MaxLevel,
                NormalYakuCondition: row.rule.NormalYakuCondition,
                YakumanCondition: row.rule.YakumanCondition));
        }
        // Store in Redis with a two-minute TTL.
        var dto = result.Select(c => new CupConfigSimpleDto(
            c.ChannelId, c.ChannelName, c.DateFrom, c.DateTo, c.IsFestive,
            c.CupId, c.CupSeq, c.JudgementType, c.CupPointSumType,
            c.MaxMatchCntLimit, c.ConditionRegular,
            c.EntryLimited, c.ConditionBilling, c.MinLevel, c.MaxLevel,
            c.NormalYakuCondition, c.YakumanCondition)).ToList();
        await Redis.SetJsonAsync(MasterCacheService.KeyCupConfigs, dto, TimeSpan.FromMinutes(2));
        return result;
    }

    // DTO used for cup config cache entries.
    private record CupConfigSimpleDto(
        string ChannelId, string ChannelName, DateTime DateFrom, DateTime DateTo, bool IsFestive,
        int CupId, int CupSeq, int JudgementType, int CupPointSumType,
        int MaxMatchCntLimit = -1, int ConditionRegular = 0,
        bool EntryLimited = false, int ConditionBilling = 0, int MinLevel = 0, int MaxLevel = 0,
        string NormalYakuCondition = "", string YakumanCondition = "");

    /// <summary>
    /// Updates cup status.
    /// Legacy reference: HMajDBObject::UpdateCupStatus.
    /// Updates STATUS in MAJAKCUPMAST / MJK_EVTMAST.
    /// status: 0=ST_STANBY / 1=ST_RUN / 2=ST_STOP
    /// </summary>
    public virtual async Task UpdateCupStatusAsync(string channelId, int status)
        => await UpdateCupStatusAsync(channelId, status, null, null);

    public virtual async Task UpdateCupStatusAsync(string channelId, int status, int? cupId, int? cupSeq)
    {
        try
        {
            await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var linkedCupIds = db.CupChannels.AsNoTracking()
                .Where(link => link.ChannelId == channelId)
                .Select(link => link.CupId);
            var regular = db.CupMasters.Where(cup => linkedCupIds.Contains(cup.CupId));
            if (cupId.HasValue)
            {
                uint targetCupId = checked((uint)cupId.Value);
                regular = regular.Where(cup => cup.CupId == targetCupId);
            }
            await regular.ExecuteUpdateAsync(setters => setters.SetProperty(cup => cup.Status, checked((byte)status)));

            bool festive = await db.ChannelMasters.AsNoTracking()
                .AnyAsync(channel => channel.ChannelId == channelId
                    && channel.GameId == GameConst.ServiceId
                    && channel.SubId.Length >= 5
                    && channel.SubId.Substring(4, 1) == "F");
            if (festive)
            {
                var events = db.TournamentPlanMasters.Where(plan => plan.IsValid);
                if (cupId.HasValue)
                {
                    uint targetCupId = checked((uint)cupId.Value);
                    events = events.Where(plan => plan.CupId == targetCupId);
                }
                if (cupSeq.HasValue)
                {
                    uint targetSequence = checked((uint)cupSeq.Value);
                    events = events.Where(plan => plan.Sequence == targetSequence);
                }
                await events.ExecuteUpdateAsync(setters => setters.SetProperty(plan => plan.Status, checked((byte)status)));
            }
            await tx.CommitAsync();
            });
        }
        catch { }
    }

    /// <summary>
    /// Loads cup status for HMajChnlInfo::m_nStatus / HMajDBObject::UpdateCupStatus checks.
    /// status: 0=ST_STANBY / 1=ST_RUN / 2=ST_STOP
    /// </summary>
    public virtual async Task<int?> GetCupStatusAsync(string channelId)
    {
        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            var linkedCupIds = db.CupChannels.AsNoTracking()
                .Where(link => link.ChannelId == channelId)
                .Select(link => link.CupId);
            int? regularStatus = await db.CupMasters.AsNoTracking()
                .Where(cup => linkedCupIds.Contains(cup.CupId))
                .Select(cup => (int?)cup.Status)
                .MaxAsync();
            bool festive = await db.ChannelMasters.AsNoTracking()
                .AnyAsync(channel => channel.ChannelId == channelId
                    && channel.GameId == GameConst.ServiceId
                    && channel.SubId.Length >= 5
                    && channel.SubId.Substring(4, 1) == "F");
            int? eventStatus = festive
                ? await db.TournamentPlanMasters.AsNoTracking().Where(plan => plan.IsValid)
                    .Select(plan => (int?)plan.Status).MaxAsync()
                : null;
            return regularStatus.HasValue || eventStatus.HasValue
                ? Math.Max(regularStatus ?? int.MinValue, eventStatus ?? int.MinValue)
                : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Loads the current top cup score.
    /// Legacy reference: HMajDBObject::GetCupTopScore.
    /// Returns the maximum CUPPOINT from MAJAKCUPRAT for the target channels.
    /// </summary>
    public virtual async Task<int> GetCupTopScoreAsync(string channelId)
    {
        // Check the Redis cache with a one-minute TTL.
        string cacheKey = MasterCacheService.KeyCupTopScore(channelId);
        var cached = await Redis.GetJsonAsync<int?>(cacheKey);
        if (cached.HasValue) return cached.Value;

        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            var cupIds = await db.CupChannels.AsNoTracking()
                .Where(link => link.ChannelId == channelId)
                .Select(link => link.CupId)
                .ToListAsync();
            bool festive = await db.ChannelMasters.AsNoTracking()
                .AnyAsync(channel => channel.ChannelId == channelId
                    && channel.GameId == GameConst.ServiceId
                    && channel.SubId.Length >= 5
                    && channel.SubId.Substring(4, 1) == "F");
            if (festive)
            {
                var now = DateTime.Now;
                cupIds.AddRange(await db.TournamentPlanMasters.AsNoTracking()
                    .Where(plan => plan.IsValid && plan.StartAt <= now && plan.EndAt >= now && plan.Status <= 1)
                    .Select(plan => plan.CupId)
                    .ToListAsync());
            }
            int score = await db.CupPlayerRatings.AsNoTracking()
                .Where(rating => cupIds.Contains(rating.CupId))
                .Select(rating => (int?)rating.CupPoint)
                .MaxAsync() ?? 0;
            await Redis.SetJsonAsync(cacheKey, (int?)score, MasterCacheService.TtlCupScore);
            return score;
        }
        catch { return 0; }
    }
    /// <summary>
    /// Resets the member and room counters in CHANELWT.
    /// </summary>
    public virtual async Task ResetCupMemberCountAsync(string channelId)
    {
        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            await db.ChannelRuntimes.Where(channel => channel.ChannelId == channelId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(channel => channel.MemberCount, (ushort)0)
                    .SetProperty(channel => channel.UsedRoom, (ushort)0)
                    .SetProperty(channel => channel.UpdatedAt, DateTime.Now));
        }
        catch { }
    }

    // MJK_EVTRAT cup event score support for CUP_JTID_GAME_SUM.
    // Legacy reference: HMajDBObject::GetMemberEventInfo / UpdateMemberEventInfo (HMajEventMasterCup.cpp).

    /// <summary>
    /// Loads the current player's cup event score from MJK_EVTRAT.
    /// Legacy reference: HMajDBObject::GetMemberEventInfo, SELECT TOTPOINT, MATCHCNT, POINT1-7.
    /// </summary>
    public virtual async Task LoadCupEvtRatAsync(MajakPlayer player, int cupId, bool entryLimited = false)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        player.CupEvtRec.EntryTitle = 0;
        player.CupEvtRec.BuyItem = false;

        if (entryLimited)
        {
            await using var db = await RequireGameDb().CreateAsync();
            var hasTitle = await db.PlayerTitles.AsNoTracking()
                .Where(t => t.MemberNo == memberNoValue
                         && (t.TitleId == "mjkt201" || t.TitleId == "mjkt202" || t.TitleId == "mjkt203"
                          || t.TitleId == "mjkt301" || t.TitleId == "mjkt302" || t.TitleId == "mjkt303"
                          || t.TitleId.StartsWith("mjkc")))
                .AnyAsync();
            if (hasTitle)
            {
                bool hasMjkt202 = await db.PlayerTitles.AsNoTracking()
                    .AnyAsync(t => t.MemberNo == memberNoValue && t.TitleId == "mjkt202");
                player.CupEvtRec.EntryTitle = hasMjkt202 ? 202 : 201;
            }
        }

        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            uint targetCupId = checked((uint)cupId);
            var rating = await db.TournamentPlayerRatings.AsNoTracking()
                .SingleOrDefaultAsync(r => r.CupId == targetCupId && r.MemberNo == memberNoValue);
            if (rating is null) return;

            var r = player.CupEvtRec;
            r.TotalPoint = checked((int)rating.TotalPoint);
            r.MatchCnt = rating.MatchCount;
            r.Points[0] = rating.Point1.HasValue ? checked((int)rating.Point1.Value) : 0;
            r.Points[1] = rating.Point2.HasValue ? checked((int)rating.Point2.Value) : 0;
            r.Points[2] = rating.Point3.HasValue ? checked((int)rating.Point3.Value) : 0;
            r.Points[3] = rating.Point4.HasValue ? checked((int)rating.Point4.Value) : 0;
            r.Points[4] = rating.Point5.HasValue ? checked((int)rating.Point5.Value) : 0;
            r.Points[5] = rating.Point6.HasValue ? checked((int)rating.Point6.Value) : 0;
            r.Points[6] = rating.Point7.HasValue ? checked((int)rating.Point7.Value) : 0;
            r.BuyItem = rating.BoughtAt.HasValue;
        }
        catch { }
    }

    /// <summary>
    /// Merges cup event score state into MJK_EVTRAT.
    /// Legacy reference: HMajDBObject::UpdateMemberEventInfo (HMajEventMasterCup.cpp).
    /// </summary>
    public virtual async Task UpdateCupEvtRatAsync(MajakPlayer player, int cupId, int cupSeq)
    {
        try
        {
            var memberNoValue = ParseMemberNo(player.MemberNo);
            await using var db = await RequireGameDb().CreateAsync();
            var r = player.CupEvtRec;
            uint targetCupId = checked((uint)cupId);
            uint targetSeq = checked((uint)cupSeq);
            var rating = await db.TournamentPlayerRatings
                .SingleOrDefaultAsync(item => item.CupId == targetCupId && item.Sequence == targetSeq && item.MemberNo == memberNoValue);
            var now = DateTime.Now;
            if (rating is null)
            {
                rating = new TournamentPlayerRatingEntity
                {
                    CupId = targetCupId,
                    Sequence = targetSeq,
                    MemberNo = memberNoValue,
                    JoinedAt = now,
                    CreatedAt = now,
                };
                db.TournamentPlayerRatings.Add(rating);
            }
            rating.TotalPoint = r.TotalPoint;
            rating.MatchCount = checked((ushort)r.MatchCnt);
            rating.Point1 = r.Points[0];
            rating.Point2 = r.Points[1];
            rating.Point3 = r.Points[2];
            rating.Point4 = r.Points[3];
            rating.Point5 = r.Points[4];
            rating.Point6 = r.Points[5];
            rating.Point7 = r.Points[6];
            if (rating.JoinedAt is null) rating.JoinedAt = now;
            rating.UpdatedAt = now;
            await db.SaveChangesAsync();
        }
        catch { }
    }

    // UserPresent personal notification support (MJK_USERPRESENT).
    // Legacy reference: HMajDBObject::SelectUserPresent / UpdateUserPresentRecieved.

    /// <summary>
    /// Loads unreceived UserPresent rows.
    /// </summary>
    public virtual async Task<List<UserPresentRecord>> GetUserPresentAsync(string memberNo)
    {
        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            await using var db = await RequireGameDb().CreateAsync();
            var presents = await db.PlayerPresents.AsNoTracking()
                .Where(p => p.MemberNo == memberNoValue && p.ReceiveStatus == 0)
                .ToListAsync();
            return presents.Select(p => new UserPresentRecord
            {
                SeqNo = checked((long)p.PresentId),
                MemberNo = MemberNoIds.Format(p.MemberNo),
                RecvStatus = p.ReceiveStatus,
                PresentNum = p.PresentAmount,
                PresentKbn = p.PresentType,
                PresentKind = p.PresentKind,
                PresentInfo = p.PresentInfo ?? string.Empty,
                PresentId = p.PresentRefId ?? string.Empty,
            }).ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Loads unreceived UserPresent rows and applies the same side effects as legacy GetUserPresent.
    /// Legacy reference: HMajDBObject::GetUserPresent.
    /// </summary>
    public virtual async Task<List<UserPresentRecord>> GetUserPresentAsync(MajakPlayer player)
    {
        var presents = await GetUserPresentAsync(player.MemberNo);
        if (presents.Count == 0) return presents;

        foreach (var present in presents)
        {
            switch (present.PresentKind)
            {
                case 1: // CMN_PRESENT_KIND_MONEY
                    string eventCode = TournamentTables.GetProCodeForMoneyLog(present.PresentKbn);
                    if (!await AddEarnedMoneyAsync(player.MemberNo, present.PresentNum, eventCode, player.GamMoney))
                        throw new InvalidOperationException("GetUserPresent AddEarnedMoney failed.");
                    player.EarnedMoney += present.PresentNum;
                    break;

                case 2: // CMN_PRESENT_KIND_MJKTITLE
                    if (!string.IsNullOrEmpty(present.PresentId))
                        await InsertOrEnableTitleAsync(player.MemberNo, present.PresentId);
                    break;
            }
        }

        if (!await UpdateUserPresentReceivedAsync(presents.Select(p => p.SeqNo)))
            throw new InvalidOperationException("GetUserPresent UpdateUserPresentReceived failed.");
        return presents;
    }

    /// <summary>
    /// Marks presents as received (RECVSTATUS = 1, MODIDT = SYSDATE).
    /// UPDATE MJK_USERPRESENT SET RECVSTATUS=1, MODIDT=SYSDATE WHERE SEQNO=?
    /// Legacy reference: HMajDBObject::UpdateUserPresentRecieved.
    /// </summary>
    public virtual async Task<bool> UpdateUserPresentReceivedAsync(IEnumerable<long> seqNos)
    {
        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            var seqList = seqNos.ToList();
            if (seqList.Count == 0) return true;
            var now = DateTime.Now;
            foreach (var seqNo in seqList)
            {
                ulong presentId = checked((ulong)seqNo);
                await db.PlayerPresents
                    .Where(p => p.PresentId == presentId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.ReceiveStatus, (byte)1)
                        .SetProperty(p => p.ReceivedAt, now));
            }
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Updates MJKCOMMONRAT.GEMCNT for gem game rewards.
    /// Legacy reference: HMajRoomServer::GetGemCountToGet and MJKCOMMONRAT UPDATE.
    /// </summary>
    public virtual async Task UpdateGemCountAsync(string memberNo, int gemCount)
    {
        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            await using var db = await RequireGameDb().CreateAsync();
            await db.PlayerWallets
                .Where(w => w.MemberNo == memberNoValue)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(w => w.GemCount, gemCount)
                    .SetProperty(w => w.UpdatedAt, DateTime.Now));
        }
        catch { }
    }

    /// <summary>
    /// Adds to MJKCOMMONRAT.GEMCNT after game results.
    /// Legacy reference: HMajDBObject::UpdateResult_GambleType.
    ///   GEMCNT = NVL(GEMCNT, 0) + :a_onGemCount
    /// </summary>
    public virtual async Task IncrementGemCountAsync(string memberNo, int gemCount)
    {
        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            await using var db = await RequireGameDb().CreateAsync();
            var wallet = await db.PlayerWallets.SingleOrDefaultAsync(w => w.MemberNo == memberNoValue);
            if (wallet is not null)
            {
                wallet.GemCount = checked(wallet.GemCount + gemCount);
                wallet.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
            }
        }
        catch { }
    }

    /// <summary>
    /// Adds yakuman bonus money to MJKCOMMONRAT.EARNEDMONEY.
    /// Legacy reference: HMajDBObject::UpdateEarnedMoneyByYakumanBonus.
    ///   UPDATE MJKCOMMONRAT SET EARNEDMONEY = NVL(EARNEDMONEY,0) + :onEarnedMoney
    /// </summary>
    public async Task UpdateEarnedMoneyBonusAsync(string memberNo, long bonusMoney)
    {
        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            await using var db = await RequireGameDb().CreateAsync();
            var wallet = await db.PlayerWallets.SingleOrDefaultAsync(w => w.MemberNo == memberNoValue);
            if (wallet is not null)
            {
                wallet.EarnedGameMoney = checked(wallet.EarnedGameMoney + bonusMoney);
                wallet.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
            }
        }
        catch { }
    }

    /// <summary>
    /// HMajDBObject::UpdateEarnedMoneyByYakumanBonus(vector&lt;string&gt;) equivalent.
    /// Updates EARNEDMONEY and inserts GAMEMONEYHIST in one transaction.
    /// </summary>
    public virtual async Task<bool> UpdateEarnedMoneyByYakumanBonusAsync(IEnumerable<string> memberNos)
    {
        var ids = memberNos
            .Where(id => !string.IsNullOrEmpty(id) && MemberNoIds.TryParse(id, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0) return false;

        try
        {
            bool updated = await ExecuteGameTransactionAsync(async (db, tx) =>
            {
                foreach (string memberNo in ids)
                {
                    var memberNoValue = ParseMemberNo(memberNo);
                    var wallet = await db.PlayerWallets.SingleOrDefaultAsync(w => w.MemberNo == memberNoValue);
                    if (wallet is not null)
                    {
                        wallet.EarnedGameMoney = checked(wallet.EarnedGameMoney + GameConst.YakumanBonusMoney);
                        wallet.UpdatedAt = DateTime.Now;
                    }
                }
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            });
            if (!updated) return false;

            if (_log is not null)
            {
                foreach (string memberNo in ids)
                    await InsertGameMoneyHistFromTransactionCodeAsync(
                        memberNo, GameConst.EvtCodeYakumanBonus, GameConst.YakumanBonusMoney,
                        0, 0, string.Empty);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Adds to MJKCOMMONRAT.EARNEDMONEY and writes GAMEMONEYHIST.
    /// Legacy reference: HMajDBObject::AddEarnedMoney.
    /// </summary>
    public virtual async Task<bool> AddEarnedMoneyAsync(string memberNo, long amount, string eventCode, long preMoney)
    {
        if (string.IsNullOrEmpty(memberNo)) return false;

        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            bool updated = await ExecuteGameTransactionAsync(async (db, tx) =>
            {
                var wallet = await db.PlayerWallets.SingleOrDefaultAsync(w => w.MemberNo == memberNoValue);
                if (wallet is null)
                {
                    await tx.RollbackAsync();
                    return false;
                }
                wallet.EarnedGameMoney = checked(wallet.EarnedGameMoney + amount);
                wallet.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            });
            if (!updated) return false;

            if (_log is not null)
                await InsertGameMoneyHistFromTransactionCodeAsync(
                    memberNo, eventCode, amount, preMoney, checked(preMoney + amount), string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// PlayPark mission progress procedure ? HMajDBObject::CallPlayParkMission.
    /// External legacy stored procedure; replaced with neutral stub preserving success semantics.
    /// </summary>
    public virtual async Task<(bool Ok, int RetCount)> CallPlayParkMissionAsync(
        string memberNo,
        int missionType,
        int missionNo,
        int procType,
        int missionValue)
    {
        // External stored procedure PC_PLAYPARK_MISPRGUPD not present in new architecture.
        // Return neutral success to preserve callers' control flow.
        await Task.CompletedTask;
        return (true, 0);
    }

    /// <summary>
    /// Generic login event gift receive ? HMajDBObject::RecvEventGift.
    /// </summary>
    public virtual async Task<bool> ReceiveGeneralEventGiftAsync(MajakPlayer player)
    {
        if (string.IsNullOrEmpty(player.MemberNo)) return false;

        try
        {
            var memberNoValue = ParseMemberNo(player.MemberNo);
            long preMoney = player.GamMoney;
            var result = await ExecuteGameTransactionAsync<(
                bool Success,
                long NextMoney,
                int NextLevel,
                IReadOnlyList<(string GiftCode, long GiftValue)> Gifts)>(async (db, tx) =>
            {
                uint eventNo = checked((uint)GameConst.LoginGiftEventNo);
                var gifts = await db.SerialExchangeItems
                    .Where(item => item.EventCode == GameConst.LoginGiftEventCode
                        && item.EventNo == eventNo
                        && item.ServiceId == GameConst.ServiceId
                        && item.MemberNo == memberNoValue
                        && item.GiftValue > 0)
                    .ToListAsync();

                if (gifts.Count == 0)
                {
                    await tx.CommitAsync();
                    return (Success: true, NextMoney: preMoney, NextLevel: player.NLevel,
                        Gifts: Array.Empty<(string GiftCode, long GiftValue)>());
                }

                var awardedGifts = gifts.Select(gift => (gift.GiftCode, gift.GiftValue)).ToList();
                long nextMoney = preMoney;
                foreach (var gift in gifts)
                {
                    if (!int.TryParse(gift.GiftCode, out int giftCode) || giftCode != GameConst.LoginGiftCodeCoin)
                    {
                        await tx.RollbackAsync();
                        return (Success: false, NextMoney: preMoney, NextLevel: player.NLevel,
                            Gifts: Array.Empty<(string GiftCode, long GiftValue)>());
                    }

                    nextMoney = checked(nextMoney + gift.GiftValue);
                }

                int nextLevel = 0;
                for (int level = MoneyLevelThresholds.Length - 1; level >= 0; level--)
                {
                    if (nextMoney >= MoneyLevelThresholds[level])
                    {
                        nextLevel = level;
                        break;
                    }
                }

                var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                var profile = await db.PlayerProfiles.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                if (wallet is null || profile is null)
                {
                    await tx.RollbackAsync();
                    return (Success: false, NextMoney: preMoney, NextLevel: player.NLevel,
                        Gifts: Array.Empty<(string GiftCode, long GiftValue)>());
                }
                wallet.GameMoney = nextMoney;
                wallet.GemCount = player.GemCount;
                wallet.UpdatedAt = DateTime.Now;
                profile.BestMoneyLevel = checked((byte)nextLevel);
                profile.UpdatedAt = DateTime.Now;
                foreach (var gift in gifts)
                {
                    gift.GiftValue = 0;
                    gift.UpdatedAt = DateTime.Now;
                }
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return (Success: true, NextMoney: nextMoney, NextLevel: nextLevel,
                    Gifts: awardedGifts);
            });
            if (!result.Success) return false;

            player.GamMoney = result.NextMoney;
            player.NLevel = result.NextLevel;
            player.SLevel = MoneyLevelNames[result.NextLevel];
            if (_log is not null)
            {
                foreach (var gift in result.Gifts)
                    await InsertGameMoneyHistFromTransactionCodeAsync(
                        player.MemberNo, GameConst.EvtCodeLoginGiftMoney, gift.GiftValue,
                        preMoney, result.NextMoney, player.IpAddress);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void UpdateMoneyLevel(MajakPlayer player)
    {
        for (int level = MoneyLevelThresholds.Length - 1; level >= 0; level--)
        {
            if (player.GamMoney >= MoneyLevelThresholds[level])
            {
                player.NLevel = level;
                player.SLevel = MoneyLevelNames[level];
                return;
            }
        }

        player.NLevel = 0;
        player.SLevel = MoneyLevelNames[0];
    }

    /// <summary>
    /// MJK_ITEMLIST quantity update ? HMajDBObject::UpdateItemQuantity.
    /// </summary>
    public virtual async Task<bool> UpdateItemQuantityAsync(MajakPlayer player, string memberNo, string itemCode, int delta)
    {
        if (string.IsNullOrEmpty(memberNo) || memberNo != player.MemberNo) return false;
        if (string.IsNullOrEmpty(itemCode)) return false;
        if (delta == 0) return true;

        try
        {
            var memberNoValue = ParseMemberNo(memberNo);
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var item = await db.PlayerFunctionItems.FindAsync(memberNoValue, itemCode);
            if (item is null)
            {
                await tx.RollbackAsync();
                return false;
            }
            long nextQuantity = item.Quantity + (long)delta;
            if (nextQuantity < 0 || nextQuantity > uint.MaxValue)
            {
                await tx.RollbackAsync();
                return false;
            }
            item.Quantity = checked((uint)nextQuantity);
            item.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            int index = player.MajItems.FindIndex(existing => existing.ItemCode == itemCode);
            if (index >= 0)
                player.MajItems[index] = player.MajItems[index] with { Qty = checked((int)item.Quantity) };
            return true;
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Casual-point mission progress procedure ? HMajDBObject::CallCasualPointUpdMission.
    /// External legacy stored procedure; replaced with neutral stub preserving success semantics.
    /// </summary>
    public virtual async Task<bool> CallCasualPointUpdMissionAsync(
        string memberNo,
        int conditionType,
        int conditionSubType,
        int count,
        DateTime procDt)
    {
        // External stored procedure CASUALPOINT.PC_UPDMISSION not present in new architecture.
        // Return neutral success to preserve callers' control flow.
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Hangame CMS mission event procedure ? HMajDBObject::CallPcMissionEventCMS.
    /// External legacy stored procedure; replaced with neutral stub preserving documented failure (code == -5) as success.
    /// </summary>
    public virtual async Task<bool> CallPcMissionEventCmsAsync(string memberNo, string eventCode, int missionNo)
    {
        // External stored procedure PC_MISSIONEVENTCMS not present in new architecture.
        // Legacy source treats code == -5 as success; return success to preserve semantics.
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Updates daily mission progress.
    /// Legacy reference: HMajDBObject::SetDailyMission.
    /// Adds nProgressCnt to PROGRESSCNT for missions matching CONDITIONTYPE, and sets MISSIONSTATE to 1 (CLR)
    /// when the progress reaches CONDITIONCNT.
    /// Note: legacy behavior treats member_no + mission_id as the natural key and updates MODIDT for idempotency.
    ///

    ///   0=NON  1=LOGIN  2=PLAY  3=TOP  4=EXCGEM  5=GETGEM  6=USEHANC
    /// </summary>
    public async Task SetDailyMissionAsync(string memberNo, int conditionType, int progressIncrement)
    {
        try
        {
            await UpdateDailyMissionsAsync(memberNo, conditionType, progressIncrement);
            await Redis.InvalidateAsync($"majak2:player:{memberNo}:daily:{DateTime.Today:yyyyMMdd}");
            await Redis.InvalidateAsync($"majak2:player:{memberNo}:weeklypoint:{WeekStartKey()}");
            await Redis.InvalidateAsync($"majak2:player:{memberNo}:dailypoint:{DateTime.Today:yyyyMMdd}");
        }
        catch { }
    }

    /// <summary>SetDailyMission for command paths that must bypass Redis.</summary>
    public virtual async Task SetDailyMissionDirectAsync(string memberNo, int conditionType, int progressIncrement)
        => await UpdateDailyMissionsAsync(memberNo, conditionType, progressIncrement);

    private async Task UpdateDailyMissionsAsync(string memberNo, int conditionType, int progressIncrement)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await ExecuteGameTransactionAsync(async (db, tx) =>
        {
        var now = DateTime.Now;
        var today = now.Date;
        byte targetCondition = checked((byte)conditionType);
        var masters = await db.DailyMissionMasters
            .Where(mission => mission.ConditionType == targetCondition)
            .ToListAsync();
        foreach (var master in masters)
        {
            var state = await db.PlayerDailyMissions.FindAsync(memberNoValue, master.MissionId);
            if (state is not null && state.UpdatedAt >= today && state.MissionState != 0)
                continue;

            if (state is null)
            {
                state = new PlayerDailyMissionEntity
                {
                    MemberNo = memberNoValue,
                    MissionId = master.MissionId,
                    CreatedAt = now,
                };
                db.PlayerDailyMissions.Add(state);
            }
            bool sameDay = state.UpdatedAt >= today;
            int progress = sameDay ? state.ProgressCount + progressIncrement : progressIncrement;
            state.ProgressCount = checked((ushort)Math.Clamp(progress, 0, master.ConditionCount));
            state.MissionState = state.ProgressCount >= master.ConditionCount ? (byte)1 : (byte)0;
            state.UpdatedAt = now;

            var targetDate = DateOnly.FromDateTime(today);
            if (state.MissionState > 0 && !await db.PlayerDailyMissionHistory.AnyAsync(history =>
                history.MemberNo == memberNoValue && history.TargetDate == targetDate && history.MissionId == master.MissionId))
            {
                db.PlayerDailyMissionHistory.Add(new PlayerDailyMissionHistoryEntity
                {
                    MemberNo = memberNoValue,
                    TargetDate = targetDate,
                    MissionId = master.MissionId,
                    ProgressCount = state.ProgressCount,
                    MissionState = state.MissionState,
                    CreatedAt = now,
                });
            }
        }
        await db.SaveChangesAsync();

        var week = CurrentWeekStart();
        var weekStart = week.ToDateTime(TimeOnly.MinValue);
        var weekEnd = weekStart.AddDays(7);
        int weeklyPoint = await (
            from state in db.PlayerDailyMissions
            join master in db.DailyMissionMasters on state.MissionId equals master.MissionId
            where state.MemberNo == memberNoValue && state.MissionState > 0
                && state.UpdatedAt >= weekStart && state.UpdatedAt < weekEnd
            select (int)master.Point).SumAsync();
        var profile = await db.PlayerProfiles.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
        if (profile is not null)
        {
            profile.WeeklyPoint = weeklyPoint;
            profile.WeeklyTargetDate = week;
            profile.UpdatedAt = now;
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        });
    }



    /// <summary>



    /// </summary>
    public virtual async Task<(int DayOwn, int DayMax)> GetDailyPointAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        string cacheKey = $"majak2:player:{memberNo}:dailypoint:{DateTime.Today:yyyyMMdd}";
        var cached = await Redis.GetJsonAsync<int[]>(cacheKey);
        if (cached is { Length: 2 }) return (cached[0], cached[1]);

        await using var db = await RequireGameDb().CreateAsync();
        var today = DateTime.Today;
        int max = await db.DailyMissionMasters.SumAsync(mission => (int)mission.Point);
        int own = await (
            from mission in db.DailyMissionMasters
            join state in db.PlayerDailyMissions on mission.MissionId equals state.MissionId
            where state.MemberNo == memberNoValue && state.UpdatedAt >= today && state.MissionState > 0
            select (int)mission.Point).SumAsync();
        await Redis.SetJsonAsync(cacheKey, new[] { own, max }, TtlUntilMidnight());
        return (own, max);
    }

    /// <summary>



    /// </summary>
    public virtual async Task UpdateGameClearCntAsync(long clearCnt)
    {
        await using var db = await RequireGameDb().CreateAsync();
        var now = DateTime.Now;
        var boundary = now.TimeOfDay >= TimeSpan.FromHours(9)
            ? now.Date.AddHours(9)
            : now.Date.AddDays(-1).AddHours(9);
            var counter = await db.GameClearCounts.FindAsync(GameConst.ServiceId);
        if (counter is null)
            db.GameClearCounts.Add(new GameClearCountEntity { GameId = GameConst.ServiceId, Count = clearCnt, UpdatedAt = now });
        else
        {
            counter.Count = counter.UpdatedAt < boundary ? clearCnt : checked(counter.Count + clearCnt);
            counter.UpdatedAt = now;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>



    /// </summary>
    public virtual async Task<List<ProPlayerInfo>> GetProPlayerListAsync()
    {

        const string proPlayerKey = MasterCacheService.KeyProPlayers;
        var cached = await Redis.GetJsonAsync<List<ProPlayerInfo>>(proPlayerKey);
        if (cached is not null) return cached;

        await using var db = await RequireGameDb().CreateAsync();
        var list = await db.EventUsers.AsNoTracking()
            .Where(evt => evt.EventCode == "5333" && evt.EventNo == 0)
            .Select(evt => new ProPlayerInfo { MemberNo = MemberNoIds.Format(evt.MemberNo), PictureUrl = evt.ExtraInfo1 })
            .ToListAsync();
        await Redis.SetJsonAsync(proPlayerKey, list, TimeSpan.FromHours(1));
        return list;
    }

    /// <summary>

    /// </summary>
    public virtual async Task<bool> SaveGradeModeProDataAsync(MajakPlayer player, GameReport.UserResult user, DateTime now)
    {
        try
        {
            var memberNoValue = ParseMemberNo(player.MemberNo);
            await using var db = await RequireGameDb().CreateAsync();
            var eventNo = await db.EventMasters.AsNoTracking()
                .Where(evt => evt.EventCode == GameConst.GradeModeProSaveEventCode
                    && evt.StartsAt <= now && evt.EndsAt >= now)
                .OrderBy(evt => evt.EventNo)
                .Select(evt => (uint?)evt.EventNo)
                .FirstOrDefaultAsync();
            if (!eventNo.HasValue) return false;
            var evtUser = await db.EventUsers.FindAsync(GameConst.GradeModeProSaveEventCode, eventNo.Value, memberNoValue);
            if (evtUser is null)
            {
                evtUser = new EventUserEntity
                {
                    EventCode = GameConst.GradeModeProSaveEventCode,
                    EventNo = eventNo.Value,
                    MemberNo = memberNoValue,
                    RegisteredAt = now,
                };
                db.EventUsers.Add(evtUser);
            }
            evtUser.LastActivityAt = now;
            evtUser.ExtraValue1++;
            if (user.Ranking == 1) evtUser.ExtraValue2++;
            if (user.Ranking == 2) evtUser.ExtraValue3++;
            if (user.Ranking == 3) evtUser.ExtraValue4++;
            if (user.Ranking == 4) evtUser.ExtraValue5++;
            evtUser.ExtraInfo1 = player.ProPictureUrl;
            await db.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>




    /// </summary>
    public virtual async Task<List<GradeRankConfirmItem>> LoadGradeRankForConfirmAsync(int rankDate, int rankKind)
    {
        const int GRADE_10_DANI      = 19;
        const int RATING_RANK_BEGINNER = 98;
        await using var db = await RequireGameDb().CreateAsync();
        var month = RankMonth(rankDate);
        byte kind = checked((byte)rankKind);
        var query = db.PlayerGradeRanks.AsNoTracking()
            .Where(rank => rank.RankDate == month && rank.RankKind == kind);
        var rows = rankKind == GRADE_10_DANI
            ? await query.OrderByDescending(rank => rank.ExtraCount).ThenBy(rank => rank.LastExtraAt).ToListAsync()
            : rankKind == RATING_RANK_BEGINNER
                ? await query.OrderByDescending(rank => rank.GradeLevel).ThenByDescending(rank => rank.Rating).ThenBy(rank => rank.CreatedAt).ToListAsync()
                : await query.OrderByDescending(rank => rank.Rating).ThenBy(rank => rank.LastPlayedAt).ToListAsync();
        return rows.Select((rank, index) => new GradeRankConfirmItem
        {
            MemberNo = MemberNoIds.Format(rank.MemberNo),
            Rating = rank.Rating,
            Grade = rank.GradeLevel,
            ExtraCnt = checked((int)rank.ExtraCount),
            Rank = index + 1,
        }).ToList();
    }

    /// <summary>


    /// </summary>
    public virtual async Task UpdateGradeRankConfirmAsync(
        int rankDate, int rankKind,
        IReadOnlyList<GradeRankConfirmItem> rows,
        DateTime now)
    {
        if (rows.Count == 0) return;
        await ExecuteGameTransactionAsync(async (db, tx) =>
        {
        var month = RankMonth(rankDate);
        byte kind = checked((byte)rankKind);
        foreach (var row in rows)
        {
            var memberNoValue = ParseMemberNo(row.MemberNo);
            await db.PlayerGradeRanks
                .Where(rank => rank.RankDate == month && rank.RankKind == kind && rank.MemberNo == memberNoValue)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rank => rank.RankPosition, row.Rank)
                    .SetProperty(rank => rank.UpdatedAt, now));
        }
        await tx.CommitAsync();
        });
    }

    /// <summary>


    /// </summary>
    public virtual async Task<int> UpdateGradeManageStatusAsync(int rankDate, int preStatus, int aftStatus)
    {
        await using var db = await RequireGameDb().CreateAsync();
        var month = RankMonth(rankDate).ToDateTime(TimeOnly.MinValue);
        return await db.GradeRankSchedules
            .Where(schedule => schedule.RankDate == month && schedule.BatchFlag == preStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(schedule => schedule.BatchFlag, checked((byte)aftStatus))
                .SetProperty(schedule => schedule.UpdatedAt, DateTime.Now));
    }

    /// <summary>


    /// </summary>
    public virtual async Task InsertGradeManageAsync(int rankDate, DateTime now)
    {
        await using var db = await RequireGameDb().CreateAsync();
        var month = RankMonth(rankDate).ToDateTime(TimeOnly.MinValue);
        if (!await db.GradeRankSchedules.AnyAsync(schedule => schedule.RankDate == month))
        {
            db.GradeRankSchedules.Add(new GradeRankScheduleEntity
            {
                RankDate = month,
                BatchFlag = 2,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>



    /// </summary>
    public virtual async Task<Dictionary<int, int>> GetGradeRankCountsAsync(int rankDate)
    {

        string cacheKey = $"majak2:graderank:counts:{rankDate}";
        var cachedRaw = await Redis.GetJsonAsync<Dictionary<string, int>>(cacheKey);
        if (cachedRaw is { Count: > 0 })
            return cachedRaw.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);

        await using var db = await RequireGameDb().CreateAsync();
        var month = RankMonth(rankDate);
        var result = await db.PlayerGradeRanks.AsNoTracking()
            .Where(rank => rank.RankDate == month && rank.RankKind != 98 && rank.RankKind != 99)
            .GroupBy(rank => rank.RankKind)
            .ToDictionaryAsync(group => (int)group.Key, group => group.Count());
        int total = result.Values.Sum();
        result[99] = total; // RATING_RANK_ALL

        await Redis.SetJsonAsync(cacheKey,
            result.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            MasterCacheService.TtlRanking);
        return result;
    }

    /// <summary>

    ///


    ///

    /// </summary>
    public virtual async Task<Dictionary<string, string>> GetCircleInfoAsync(string memberNo)
        => await Task.FromResult(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static DateOnly RankMonth(int rankDate)
        => new(rankDate / 100, rankDate % 100, 1);

    private static DateOnly CurrentWeekStart()
    {
        var today = DateTime.Today;
        int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        return DateOnly.FromDateTime(today.AddDays(-daysSinceMonday));
    }
}



public record ItemInfo
{
    public string ItemCode  { get; init; } = "";
    public long   Price     { get; init; }
    public int    GemAmount { get; init; }
}

public record GradeRankItem
{
    public string MemberNo   { get; init; } = "";
    public string AvatarId   { get; init; } = "";
    public int    Rating     { get; init; }
    public int    Grade      { get; init; }
    public string LastDate   { get; init; } = "";
    public int    ExtraCount { get; init; }
    public int    Rank       { get; init; }
}

public record GradeRankUpdateItem
{
    public int    RankDate   { get; init; }
    public int    RankKind   { get; init; }
    public string MemberNo   { get; init; } = "";
    public int    Rating     { get; init; }
    public int    Grade      { get; init; }
    public int    ExtraCount { get; init; }
    public string AvatarId   { get; init; } = "";
    public int    DispFlag   { get; init; }
}

public record GradeSelectItem
{
    public int    DispOrder    { get; init; }
    public int    YearMonth    { get; init; }
    public string YearMonthStr { get; init; } = "";
}


public record EventInfo
{
    public string EvtCode   { get; init; } = "";
    public int    EvtNo     { get; init; }
    public int    ExtraVal1 { get; init; }
    public int    ExtraVal2 { get; init; }
    public int    ExtraVal3 { get; init; }
    public int    ExtraVal4 { get; init; }
    public int    ExtraVal5 { get; init; }
    public string LastDt    { get; init; } = "";
    public string RegDt     { get; init; } = "";
}


public record WeeklyRewardMastInfo
{
    public int RewardId   { get; init; }
    public int RewardType { get; init; }  // MSN_RT_COIN=1, MSN_RT_GEM=2, MSN_RT_ITEM=3
    public int RewardCnt  { get; init; }
    public int MustPoint  { get; init; }
}


public record DailyMissionMastInfo
{
    public int MissionId     { get; init; }
    public int ConditionType { get; init; }
    public int ConditionCnt  { get; init; }
    public int Point         { get; init; }
}


public record SerialMastInfo
{
    public string   EvtCode     { get; init; } = "";
    public int      EvtNo       { get; init; }
    public DateTime EvtStartDt  { get; init; }
    public DateTime EvtEndDt    { get; init; }
    public string   GiftCode    { get; init; } = "";
    public int      GiftValue   { get; init; }
    public int      MissionNo   { get; init; }
    public string   GiftMessage { get; init; } = "";
}


public record SerialCouponInfo
{
    public string MemberNo { get; init; } = "";
}


public record EvtCodeMastInfo
{
    public string   EvtCode    { get; init; } = "";
    public int      EvtNo      { get; init; }
    public string   EvtName    { get; init; } = "";
    public string   EvtDesc    { get; init; } = "";
    public string   SvcId      { get; init; } = "";
    public string   EvtTblInfo { get; init; } = "";
    public DateTime EvtStartDt { get; init; }
    public DateTime EvtEndDt   { get; init; }
}


public record AdminIdInfo
{
    public string MemberNo { get; init; } = "";
    public int    AdminSts { get; init; }
}


public record ProPlayerInfo
{
    public string MemberNo   { get; init; } = "";
    public string PictureUrl { get; init; } = "";
}


public record GradeRankConfirmItem
{
    public string MemberNo { get; init; } = "";
    public int    Rating   { get; init; }
    public int    Grade    { get; init; }
    public int    ExtraCnt { get; init; }
    public int    Rank     { get; init; }
}

/// <summary>


/// </summary>
public record UserPresentRecord
{
    public long   SeqNo       { get; init; }
    public string MemberNo    { get; init; } = "";
    public int    RecvStatus  { get; init; }
    public long   PresentNum  { get; init; }
    public int    PresentKbn  { get; init; }
    public int    PresentKind { get; init; }
    public string PresentInfo { get; init; } = "";
    public string PresentId   { get; init; } = "";
}
