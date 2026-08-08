using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// コイン関連ビジネスロジック — HMajDBObject コイン関連メソッドの移植
/// </summary>
public class GameMoneyService
{
    private readonly PlayerRepository          _playerRepo;
    private readonly RatingService             _ratingService;
    private readonly HistoryRepository?        _historyRepo;

    public GameMoneyService(
        PlayerRepository          playerRepo,
        RatingService             ratingService,
        HistoryRepository?        historyRepo = null)
    {
        _playerRepo  = playerRepo;
        _ratingService = ratingService;
        _historyRepo = historyRepo;
    }

    /// <summary>
    /// 新規 MJKCOMMONRAT 作成 — HMajDBObject::CreateMemberGameRecord.
    /// </summary>
    public async Task CreateCommonRatWithDefaultMoneyHistAsync(string memberNo, long initialMoney, string remoteAddr)
    {
        await _playerRepo.CreateCommonRatAsync(memberNo, initialMoney);

        if (_historyRepo == null) return;
        try
        {
            await _historyRepo.InsertGameMoneyHistAsync(
                memberNo,
                GameConst.EvtCodeDefaultMoney,
                initialMoney,
                0,
                initialMoney,
                remoteAddr);
        }
        catch
        {
        }
    }

    /// <summary>
    /// コイン増減処理 + GAMEMONEYHIST 記録
    /// 原典: WriteGameMoneyHist → PC_MAJAK2_HIST 呼び出し
    ///
    /// PC_MAJAK2_HIST パラメータ:
    ///   IN_ORDERNO   = "" (空文字)
    ///   IN_CUMCODE   = eventCode       (GAMEMONEYHIST.EVENTCODE 列)
    ///   IN_EVENTCODE = eventTitle      (GAMEMONEYHIST.EVENTITLE 列、イベントタイトル)
    ///   IN_MONEY     = delta の絶対値   (プロシージャ内で EARNEDMONEY に加算)
    ///   IN_COMMITCHK = 'Y'             (プロシージャ内で COMMIT)
    /// </summary>
    public async Task AddMoneyAsync(MajakPlayer player, long delta,
        string eventCode, string remoteAddr = "", string eventTitle = "")
    {
        // メモリ上のコインを先に更新 (原典と同様)
        player.GamMoney = Math.Max(0, player.GamMoney + delta);
        _ratingService.UpdatePlayerLevel(player);

        // PC_MAJAK2_HIST でコミットまで行う
        // IN_MONEY は符号付き値をそのまま渡す (プロシージャ側で EARNEDMONEY += IN_MONEY)
        await _playerRepo.AddEarnedGameMoneyAsync(
            memberNo: player.MemberNo,
            amount: delta,
            eventCode: eventCode,
            eventTitle: string.IsNullOrEmpty(eventTitle) ? eventCode : eventTitle,
            orderNumber: "",
            remoteAddress: remoteAddr);
    }

    /// <summary>
    /// 無料GP補充。公式Webマニュアル 5_3 に従い、1日1回、1,000 GP未満を1,000 GPまで補充する。
    /// </summary>
    public async Task<(bool Ok, long NewMoney, long LentMoney, int RestAllIn, int ReplenishmentType)>
        ReplenishAsync(MajakPlayer player, int repType)
    {
        long originalMoney = player.GamMoney;
        int originalAllinCnt = player.AllinCnt;
        DateTime? originalLastAllinDt = player.LastAllinDt;

        DateTime currentTime = DateTime.Now;
        RefreshReplenishmentDay(player, currentTime);

        long allinTarget = GameConst.AllinMoney;
        int maxCount = GameConst.AllinCountMax;

        int restAllIn = Math.Max(0, maxCount - player.AllinCnt);

        // 原典: if (GamMoney < allInMoney) のみ補充
        if (player.GamMoney >= allinTarget)
            return (false, player.GamMoney, 0, restAllIn, 0);

        // 原典: m_nAllinCnt < ALLINCOUNT_MAX
        if (player.AllinCnt >= maxCount)
            return (false, player.GamMoney, 0, 0, 3);

        // ★ 差分のみ補充 (原典: llAllinMoney = allInMoney - GamMoney)
        long delta = allinTarget - player.GamMoney;
        long preMoney = player.GamMoney;
        int preAllinCnt = player.AllinCnt;
        DateTime? preLastAllinDt = player.LastAllinDt;

        player.GamMoney  = allinTarget;   // 目標額ピッタリにする
        player.AllinCnt++;
        player.LastAllinDt = currentTime;

        _ratingService.UpdatePlayerLevel(player);

        // DB 永続化 — 原典: UpdateChargeFreeMoney → MJKCOMMONRAT UPDATE
        if (!await _playerRepo.UpdateChargeFreeMoneyAsync(player))
        {
            player.GamMoney = originalMoney;
            player.AllinCnt = originalAllinCnt;
            player.LastAllinDt = originalLastAllinDt;
            _ratingService.UpdatePlayerLevel(player);
            return (false, player.GamMoney, 0, Math.Max(0, maxCount - player.AllinCnt), 0);
        }

        // ゲームマネー変動ログ — 原典: WriteGameMoneyHist (GAMEMONEYHIST INSERT のみ)
        if (_historyRepo != null)
        {
            try
            {
                await _historyRepo.InsertGameMoneyHistAsync(
                    player.MemberNo,
                    GameConst.EvtCodeFreeMoney,
                    delta,
                    preMoney,
                    player.GamMoney,
                    player.IpAddress);
            }
            catch
            {
            }
        }

        restAllIn = Math.Max(0, maxCount - player.AllinCnt);
        return (true, player.GamMoney, 0L, restAllIn, 2);
    }

    public static void RefreshReplenishmentDay(MajakPlayer player, DateTime currentTime)
    {
        DateTime nowTime = currentTime.AddHours(-6);
        DateTime? lastAllin = player.LastAllinDt?.AddHours(-6);
        if (lastAllin == null || lastAllin.Value.Date != nowTime.Date)
            player.AllinCnt = 0;
        player.LastAllinDt = currentTime;
    }

    /// <summary>
    /// 獲得コイン適用 (ApplyEarnedMoney)
    /// — ProcessCommand_ApplyEarnedMoney 移植
    ///
    /// 原典ロジック:
    ///   1. GAMMONEY_U == 0 確認 (現金充尾「あり」なら不可)
    ///   2. DBから EARNEDMONEY 取得 (GetEarnedMoney)
    ///   3. GamMoney += EarnedMoney
    ///   4. UpdateRefillData: GAMMONEY更新 + EARNEDMONEY減算 + GAMMONEY_U=0
    ///   5. メモリ上の EarnedMoney = 0 小小化
    /// </summary>
    public async Task<(bool Ok, long NewMoney)> ApplyEarnedMoneyAsync(MajakPlayer player)
    {
        var earned = await _playerRepo.GetEarnedMoneyAsync(player.MemberNo);
        if (earned is null)
            return (false, player.GamMoney);

        player.EarnedMoney = earned.Value.EarnedMoney;
        player.Experience = earned.Value.Experience;

        if (player.EarnedMoney <= 0)
            return (false, player.GamMoney);

        // 原典: GetEarnedMoney 後に GAMMONEY_U == 0 を確認する。
        if (player.GamMoneyU != 0)
            return (false, player.GamMoney);

        // ★ 重要: earnedMoney は DB呼び出し前に保存する
        // UpdateCommonRatAsync の SQL: EARNEDMONEY = NVL(EARNEDMONEY,0) - :onEarnedMoney
        // player.EarnedMoney を 0 にする前に呼ぶ必要がある
        long earnedMoney = player.EarnedMoney;
        player.GamMoney += earnedMoney;
        if (player.GamMoney < 0) player.GamMoney = 0;   // 原典: safeguard

        _ratingService.UpdatePlayerLevel(player);

        // DB 更新: EARNEDMONEY -= earned, GAMMONEY_U = 0 (原典: UpdateRefillData)
        // player.EarnedMoney はまだ earned のまま = 差引き量として渡る
        await _playerRepo.UpdateCommonRatAsync(player);

        // DB 成功後にメモリを初期化 (原典: pPlayer->m_llEarnedMoney = 0)
        player.EarnedMoney = 0;

        return (true, player.GamMoney);
    }

    /// <summary>
    /// 役満ボーナス付与 — ProcessCommand_YakumanBonus / UpdateEarnedMoneyByYakumanBonus
    /// </summary>
    public async Task GiveYakumanBonusAsync(MajakPlayer player)
    {
        await AddMoneyAsync(player, GameConst.YakumanBonusMoney,
            GameConst.EvtCodeYakumanBonus, player.IpAddress);
    }

    /// <summary>
    /// コイン残高をDBに保存 (トーナメント入退費など、AddMoneyを使わない増減後に呼ぶ)
    /// </summary>
    public async Task SaveMoneyAsync(MajakPlayer player, string eventCode = "", long eventMoney = 0, long? preMoney = null)
    {
        long beforeMoney = preMoney ?? player.GamMoney - eventMoney;
        _ratingService.UpdatePlayerLevel(player);
        await _playerRepo.UpdateCommonRatAsync(player);

        if (_historyRepo != null && !string.IsNullOrEmpty(eventCode) && eventMoney != 0)
        {
            try
            {
                await _historyRepo.InsertGameMoneyHistAsync(
                    player.MemberNo,
                    eventCode,
                    eventMoney,
                    beforeMoney,
                    player.GamMoney,
                    player.IpAddress);
            }
            catch
            {
            }
        }
    }
}
