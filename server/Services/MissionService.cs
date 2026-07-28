using MajakServer.Infrastructure;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// ミッション/週間報酬サービス — HMajDBObject GetMissionList / RcvWeeklyReward 移植
/// </summary>
public class MissionService
{
    private readonly LogRepository      _logRepo;
    private readonly PlayerRepository   _playerRepo;
    private readonly MasterCacheService _masterCache;
    private readonly RatingService      _ratingService;

    public MissionService(LogRepository logRepo, PlayerRepository playerRepo, MasterCacheService masterCache,
        RatingService? ratingService = null)
    {
        _logRepo       = logRepo;
        _playerRepo    = playerRepo;
        _masterCache   = masterCache;
        _ratingService = ratingService ?? new RatingService();
    }

    /// <summary>
    /// ミッションリスト照会 — ProcessCommand_GetMissionInfo 移植
    /// 原典: HMajChnlServer::ProcessCommand_GetMissionInfo
    ///   SelectMjkDailyMissionList → MJK_DAILYMISSIONLIST
    ///   GetWeeklyPoint → 今週の達成ポイント合計
    ///   SelectMjkWeeklyRewardList → MJK_WEEKLYREWARDLIST
    /// </summary>
    public async Task<MissionListResult> GetMissionListAsync(MajakPlayer player)
    {
        var dailyMap = await _playerRepo.GetDailyMissionListForTodayAsync(player.MemberNo) ?? new Dictionary<int, int>();
        var dailyMast = await _masterCache.GetDailyMissionMastAsync() ?? new Dictionary<int, DailyMissionMastInfo>();

        int pointDayOwn = 0;
        int pointDayMax = 0;
        var missionStateMap = new Dictionary<int, int>();
        foreach (var entry in dailyMast.OrderBy(x => x.Key))
        {
            pointDayMax += entry.Value.Point;
            int missionState = dailyMap.TryGetValue(entry.Value.MissionId, out int state) ? state : 0;
            if (missionState != 0)
            {
                pointDayOwn += entry.Value.Point;
                missionStateMap[entry.Key] = missionState;
            }
        }

        int weeklyPoint = await _playerRepo.GetWeeklyPointForWeekAsync(player.MemberNo);
        var weeklyMap = await _playerRepo.GetWeeklyRewardListForWeekAsync(player.MemberNo) ?? new Dictionary<int, int>();
        var weeklyMast = await _masterCache.GetWeeklyRewardMastAsync() ?? new Dictionary<int, WeeklyRewardMastInfo>();

        int[] dailyMissions = Enumerable.Range(1, 11)
            .Select(i => missionStateMap.TryGetValue(i, out int s) ? s : 0)
            .ToArray();

        int[] weeklyRewards = Enumerable.Range(1, 8).Select(i =>
        {
            if (!weeklyMast.TryGetValue(i, out var mastInfo))
                return 0;
            if (weeklyPoint < mastInfo.MustPoint)
                return 1;
            return weeklyMap.TryGetValue(i, out int s) ? s : 0;
        }).ToArray();

        return new MissionListResult
        {
            PointDayOwn   = pointDayOwn,
            PointDayMax   = pointDayMax,
            PointWeekOwn  = weeklyPoint,
            PointWeekMax  = pointDayMax * 7,
            DailyMissions = dailyMissions,
            WeeklyRewards = weeklyRewards,
        };
    }

    /// <summary>
    /// 週間報酬受取 — ProcessCommand_RcvWeeklyReward 移植
    /// 原典ロジック:
    ///   1. MUSTPOINT チェック、受取済みチェック (MJK_WEEKLYREWARDLIST 参照)
    ///   2. MSN_RT_COIN → GamMoney 付与、MSN_RT_GEM → GemCount 付与
    ///   3. MJK_WEEKLYREWARDLIST に RECIEVESTATUS=1 を MERGE
    /// </summary>
    public async Task<(bool Ok, long NewMoney, int GemCount, string Message)> ReceiveWeeklyRewardAsync(
        MajakPlayer player, int rewardId, GameMoneyService moneyService)
    {
        // 1. 今週の受取状態を取得。未存在なら MSN_RS_NOTRCV として扱う。
        int? receiveStatus;
        int weeklyPoint;
        Dictionary<int, WeeklyRewardMastInfo> mast;
        try
        {
            receiveStatus = await _playerRepo.GetWeeklyRewardStatusForWeekAsync(player.MemberNo, rewardId);
            weeklyPoint = await _playerRepo.GetWeeklyPointForWeekAsync(player.MemberNo);
            mast = await _masterCache.GetWeeklyRewardMastAsync() ?? new Dictionary<int, WeeklyRewardMastInfo>();
        }
        catch
        {
            return (false, player.GamMoney, player.GemCount, "エラーが発生しました。");
        }

        if (receiveStatus == 1)
            return (false, player.GamMoney, player.GemCount, "受け取り済みです。");

        // 2. 週間ポイント不足チェック (MSN_MUSTPOINT)
        if (!mast.TryGetValue(rewardId, out var mastInfo))
            return (false, player.GamMoney, player.GemCount, "");

        if (weeklyPoint < mastInfo.MustPoint)
            return (false, player.GamMoney, player.GemCount, "ポイントが足りません。");

        // 4. 報酬付与 (REWARDTYPE: 1=コイン, 2=ジェム)
        string rewardName;
        switch (mastInfo.RewardType)
        {
            case 1: // MSN_RT_COIN
                rewardName = "無料コイン";
                player.GamMoney = Math.Max(0, player.GamMoney + mastInfo.RewardCnt);
                _ratingService.UpdatePlayerLevel(player);
                break;
            case 2: // MSN_RT_GEM
                rewardName = "龍宝石";
                player.GemCount += mastInfo.RewardCnt;
                break;
            default:
                return (false, player.GamMoney, player.GemCount, "");
        }

        // 5. Reflect: MJK_WEEKLYREWARDLIST + MJKCOMMONRAT を反映
        bool reflected = await _playerRepo.ReflectWeeklyRewardAsync(player, rewardId, receiveStatus: 1, DateTime.Now);
        if (!reflected)
            return (false, player.GamMoney, player.GemCount, "エラーが発生しました。");

        try
        {
            await _logRepo.InsertWeeklyRewardHistAsync(player.MemberNo, rewardId, receiveStatus: 1);
        }
        catch
        {
            return (false, player.GamMoney, player.GemCount, "エラーが発生しました。");
        }

        return (true, player.GamMoney, player.GemCount, $"「{rewardName}」を受け取りました。");
    }

    /// <summary>
    /// シリアルボーナス受取 — ProcessCommand_RcvSerialBonus 移植
    /// 原典: HMajChnlServer::ProcessCommand_RcvSerialBonus
    ///   SerialMastMgr::GetSerialMast → EVTCODEMAST/EVTGIFTMAST を走査
    ///   → EVTNO に応じて RcvCommonSerialBonus / RcvUniqueSerialBonus
    ///   → AddPlayerResource でコイン/ジェム/アイテムを付与
    /// </summary>
    public async Task<(int Result, long NewMoney, string Message)> ReceiveSerialBonusAsync(
        MajakPlayer player, string serialCode, GameMoneyService moneyService)
    {
        if (string.IsNullOrWhiteSpace(serialCode))
            return (0, player.GamMoney, "");

        var serialMasts = await _playerRepo.GetSerialMastsAsync();
        foreach (var mast in serialMasts)
        {
            SerialCodeResult result = mast.EvtNo switch
            {
                1 => await ReceiveCommonSerialBonusAsync(player, serialCode, mast),
                2 => await ReceiveUniqueSerialBonusAsync(player, serialCode, mast),
                _ => SerialCodeResult.OtherError,
            };

            switch (result)
            {
                case SerialCodeResult.Ok:
                    return (1, player.GamMoney, mast.GiftMessage);
                case SerialCodeResult.NoGift:
                case SerialCodeResult.NoCoupon:
                    continue;
                case SerialCodeResult.Received:
                    return (0, player.GamMoney, "受け取り済みです。");
                case SerialCodeResult.UsedCoupon:
                    return (0, player.GamMoney, "使用済みです。");
                default:
                    return (0, player.GamMoney, "エラーが発生しました。");
            }
        }

        return (0, player.GamMoney, "シリアルコードが存在しないか間違っています。");
    }

    private async Task<SerialCodeResult> ReceiveCommonSerialBonusAsync(
        MajakPlayer player, string serialCode, SerialMastInfo mast)
    {
        if (mast.GiftCode != serialCode)
            return SerialCodeResult.NoGift;

        bool exists = await _playerRepo.SerialExchangeItemExistsAsync(
            mast.EvtCode, mast.EvtNo, player.MemberNo, serialCode);
        if (exists) return SerialCodeResult.Received;

        if (!await AddPlayerResourceAsync(player, mast))
            return SerialCodeResult.OtherError;

        if (!await _playerRepo.InsertSerialExchangeItemAsync(
            mast.EvtCode, mast.EvtNo, player.MemberNo, mast.GiftCode, mast.GiftValue))
        {
            await CutPlayerResourceAsync(player, mast);
            return SerialCodeResult.OtherError;
        }

        await _playerRepo.UpdateCommonRatSerialResourceAsync(player);
        return SerialCodeResult.Ok;
    }

    private async Task<SerialCodeResult> ReceiveUniqueSerialBonusAsync(
        MajakPlayer player, string serialCode, SerialMastInfo mast)
    {
        var coupon = await _playerRepo.GetSerialCouponAsync(
            mast.EvtCode, mast.EvtNo, mast.MissionNo, serialCode);
        if (coupon == null) return SerialCodeResult.NoCoupon;
        if (!string.IsNullOrEmpty(coupon.MemberNo)) return SerialCodeResult.UsedCoupon;

        if (!await AddPlayerResourceAsync(player, mast))
            return SerialCodeResult.OtherError;

        if (!await _playerRepo.UpdateSerialCouponMemberAsync(
            mast.EvtCode, mast.EvtNo, mast.MissionNo, serialCode, player.MemberNo))
        {
            await CutPlayerResourceAsync(player, mast);
            return SerialCodeResult.OtherError;
        }

        await _playerRepo.UpdateCommonRatSerialResourceAsync(player);
        return SerialCodeResult.Ok;
    }

    private async Task<bool> AddPlayerResourceAsync(MajakPlayer player, SerialMastInfo mast)
    {
        switch (mast.MissionNo)
        {
            case 1:
                player.GamMoney += mast.GiftValue;
                _ratingService.UpdatePlayerLevel(player);
                return true;
            case 2:
                player.GemCount += mast.GiftValue;
                return true;
            case 3:
                return await _playerRepo.AddSerialBonusItemAsync(player);
            default:
                return false;
        }
    }

    private async Task CutPlayerResourceAsync(MajakPlayer player, SerialMastInfo mast)
    {
        switch (mast.MissionNo)
        {
            case 1:
                player.GamMoney -= mast.GiftValue;
                break;
            case 2:
                player.GemCount -= mast.GiftValue;
                break;
            case 3:
                await _playerRepo.UpdateItemQuantityAsync(player, player.MemberNo, "MJ20", -12);
                break;
        }
    }
}

public enum SerialCodeResult
{
    Ok,
    OtherError,
    Received,
    NoGift,
    NoCoupon,
    UsedCoupon,
}

public class MissionListResult
{
    public int   PointDayOwn   { get; set; }
    public int   PointDayMax   { get; set; }
    public int   PointWeekOwn  { get; set; }
    public int   PointWeekMax  { get; set; }
    public int[] DailyMissions { get; set; } = Array.Empty<int>();
    public int[] WeeklyRewards { get; set; } = Array.Empty<int>();
}