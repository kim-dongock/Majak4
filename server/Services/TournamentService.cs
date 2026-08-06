using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using MajakServer.Hubs;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// トーナメント管理サービス — 原典: HMajChnlServer のトーナメント部分
///
/// インメモリキャッシュ (plans + details) を管理し、
/// バックグラウンドサービスと各コマンドから参照される。
/// </summary>
public class TournamentService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlayerSessionService _session;
    private readonly IHubContext<MajakGameHub> _hub;
    private readonly ILogger<TournamentService> _logger;

    // インメモリ状態 — 原典: m_mapTournamentPlan / m_mapTournamentDetailAll
    private readonly ConcurrentDictionary<long, TournamentPlan>
        _plans = new();
    private readonly ConcurrentDictionary<long, Dictionary<int, TournamentDetail>>
        _details = new();
    private readonly List<TournamentLimit> _limits = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // 使用中ルーム数 — 原典: m_nTournamentUseRoomNum
    private int _useRoomNum;
    public  int UseRoomNum => _useRoomNum;

    public TournamentService(IServiceScopeFactory scopeFactory,
        PlayerSessionService session,
        IHubContext<MajakGameHub> hub,
        ILogger<TournamentService> logger)
    {
        _scopeFactory = scopeFactory;
        _session      = session;
        _hub          = hub;
        _logger       = logger;
    }

    // ─────────────────────────────── 初期化 ──────────────────────────────

    /// <summary>起動時に DB からロード — 原典: ReloadTournamentInfo</summary>
    public async Task InitAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        try
        {
            var plans   = await repo.SelectActivePlansAsync();
            var limits  = await repo.SelectLimitsAsync();

            _plans.Clear();
            _details.Clear();
            _limits.Clear();
            _limits.AddRange(limits);
            _useRoomNum = 0;

            foreach (var plan in plans)
            {
                _plans[plan.SeqNo] = plan;
                var detailList = await repo.SelectDetailsAsync(plan.SeqNo);
                _details[plan.SeqNo] = detailList.ToDictionary(d => d.SubId);
                plan.PlayEndCount = detailList.Count(d => d.IsFinished);

                if (plan.IsActive)
                    _useRoomNum += plan.MaxRoomNum;
            }
            _logger.LogInformation("TournamentService: loaded {Count} plans.", _plans.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TournamentService.InitAsync failed.");
        }
    }

    // ─────────────────────────────── 照会 ────────────────────────────────

    public IReadOnlyCollection<TournamentPlan> GetAllPlans()
        => _plans.Values.ToList();

    public TournamentPlan? GetPlan(long seqNo)
        => _plans.TryGetValue(seqNo, out var p) ? p : null;

    public Dictionary<int, TournamentDetail>? GetDetails(long seqNo)
        => _details.TryGetValue(seqNo, out var d) ? d : null;

    // ─────────────────────────── 登録バリデーション ──────────────────────

    /// <summary>
    /// 登録要件チェック — 原典: CheckTournamentRequiredValue + CheckTournamentCoordinalValue
    /// </summary>
    public (bool Ok, List<int> FailCodes) ValidateRegist(
        string playName, string baseRule, string moneyRule,
        string playDate, string password, int maxViewer, string roomOption,
        string planMemberNo, bool isAdmin, out TournamentPlan? plan)
    {
        plan = null;
        var fails = new List<int>();

        // baseRule: "maxPlayers|playMode|playNum|playTime"
        var bv = baseRule.Split('|');
        if (bv.Length != 4 ||
            !int.TryParse(bv[0], out int maxPlayers) ||
            !int.TryParse(bv[1], out int playMode)   ||
            !int.TryParse(bv[2], out int playNum)     ||
            !int.TryParse(bv[3], out int playTime))
        {
            fails.Add(1001); return (false, fails);
        }

        // 有効な playerNum/playMode 組み合わせか確認
        var (maxPhase, maxRoom) = TournamentTables.GetPlayInfo(maxPlayers, playMode);
        if (maxPhase == 0) { fails.Add(1001); return (false, fails); }

        // moneyRule: "joinMoney|grade1|grade2|grade3|grade4"
        var mv = moneyRule.Split('|');
        if (mv.Length != 5 ||
            !long.TryParse(mv[0], out long joinMoney)  ||
            !long.TryParse(mv[1], out long grade1)     ||
            !long.TryParse(mv[2], out long grade2)     ||
            !long.TryParse(mv[3], out long grade3)     ||
            !long.TryParse(mv[4], out long grade4)     ||
            joinMoney < TournamentConst.JoinMoneyMin || joinMoney > TournamentConst.JoinMoneyMax)
        {
            fails.Add(1002); return (false, fails);
        }

        // 名前チェック
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        int playNameByteLength = Encoding.GetEncoding(932).GetByteCount(playName);
        if (string.IsNullOrEmpty(playName) ||
            playNameByteLength < TournamentConst.NameLenMin ||
            playNameByteLength > TournamentConst.NameLenMax)
        {
            fails.Add(1003);
        }

        // 開催日チェック
        if (!DateTime.TryParseExact(
                playDate,
                "yyyy/MM/dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime playStartDt))
        {
            fails.Add(1004); return (false, fails);
        }
        var now = DateTime.Now;
        var minDt = now.AddHours(TournamentConst.ReserveMinHours).AddMinutes(TournamentConst.JoinOpenMinutes);
        var maxDt = now.AddDays(TournamentConst.ReserveMaxDays);
        if (!isAdmin && (playStartDt < minDt || playStartDt > maxDt))
            fails.Add(1004);

        // パスワード長チェック
        if (password.Length > TournamentConst.PwLenMax)
            fails.Add(1005);

        // 名前重複チェック
        if (_plans.Values.Any(p => p.IsActive && p.PlayName == playName))
            fails.Add(1006);

        // 主催者重複チェック
        if (_plans.Values.Any(p => p.IsActive && p.PlanMemberNo == planMemberNo))
            fails.Add(1007);

        // ルーム数制限チェック
        if (_useRoomNum + maxRoom > TournamentConst.MaxRoomNumGlobal * TournamentConst.MaxRoomDivision)
            fails.Add(1008);

        // 時間制限チェック (MJK_TOURNAMENTLIMIT)
        var playEndDt = CalculatePlayEndDt(playStartDt, maxPlayers, playMode, playNum, playTime);
        var joinStartDt = playStartDt.AddHours(-TournamentConst.JoinOpenHours)
                                     .AddMinutes(-TournamentConst.JoinOpenMinutes);
        if (IsInLimitPeriod(joinStartDt, playEndDt))
            fails.Add(1009);

        if (fails.Count > 0) return (false, fails);

        // TournamentPlan 組み立て
        var pt = TournamentTables.GetPlayTime(playTime) ??
                 new TournamentPlayTimeInfo(playTime, 30, 15, 25);

        plan = new TournamentPlan
        {
            PlayName     = playName,
            PlayStatus   = TournamentPlanStatus.Join,
            PlayPhase    = playNum == TournamentPlayNum.OnePlay
                           ? TournamentConst.PhaseFull : TournamentConst.PhaseHalf,
            MaxPlayerNum = maxPlayers,
            MaxRoomNum   = maxRoom,
            RoomOption   = roomOption,
            Password     = password,
            MaxViewer    = maxViewer,
            PlayNum      = playNum,
            PlayTime     = playTime,
            PlayMode     = playMode,
            JoinMoney    = joinMoney,
            GradeMoney   = [grade1, grade2, grade3, grade4],
            PlanMemberNo = planMemberNo,
            PlayStartDt  = playStartDt,
            PlayEndDt    = playEndDt,
            JoinStartDt  = joinStartDt,
            MatchStartDt = playStartDt.AddMinutes(-TournamentConst.PlayStartBefore),
            ViewEndDt    = playEndDt.AddMinutes(TournamentConst.PlayEndAfter),
        };

        // スケジュール計算
        BuildSchedule(plan, pt);
        return (true, fails);
    }

    /// <summary>
    /// 参加バリデーション — 原典: CheckTournamentJoin
    /// </summary>
    public (bool Ok, int FailCode) ValidateJoin(
        long seqNo, string memberNo, string password, long playerMoney,
        TournamentJoin? currentJoin)
    {
        if (!_plans.TryGetValue(seqNo, out var plan))
            return (false, 2001); // not found

        var now = DateTime.Now;
        if (!plan.IsJoinable(now))
            return (false, 2002); // 参加受付時間外

        if (plan.PlayerNum >= plan.MaxPlayerNum)
            return (false, 2003); // 満員

        if (currentJoin?.JoinStatus == TournamentJoinStatus.Join)
            return (false, 2004); // 既に参加中

        if (playerMoney < plan.JoinMoney)
            return (false, 2005); // 参加費不足

        // パスワードチェック
        if (!string.IsNullOrEmpty(plan.Password) && plan.Password != password)
            return (false, 2006);

        return (true, 0);
    }

    /// <summary>
    /// キャンセルバリデーション — 原典: CheckTournamentJoinCancel
    /// </summary>
    public (bool Ok, int FailCode) ValidateCancel(
        long seqNo, TournamentJoin? currentJoin)
    {
        if (!_plans.TryGetValue(seqNo, out var plan))
            return (false, 3001);

        if (DateTime.Now >= plan.MatchStartDt)
            return (false, 3002); // マッチング後はキャンセル不可

        if (currentJoin?.JoinSeqNo != seqNo ||
            currentJoin.JoinStatus != TournamentJoinStatus.Join)
            return (false, 3003);

        return (true, 0);
    }

    // ─────────────────────────────── 登録処理 ────────────────────────────

    /// <summary>トーナメント登録 — 原典: RegistTournamentPlan</summary>
    public async Task<bool> RegisterAsync(TournamentPlan plan, MajakPlayer organizer)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        long planMoney = TournamentTables.CalcPlanMoney(plan.GradeMoney);
        if (organizer.GamMoney < planMoney) return false;

        organizer.GamMoney -= planMoney;
        bool ok = await repo.InsertPlanAsync(plan);
        if (!ok)
        {
            organizer.GamMoney += planMoney;
            return false;
        }

        _plans[plan.SeqNo] = plan;
        _details[plan.SeqNo] = new();
        _useRoomNum += plan.MaxRoomNum;
        return true;
    }

    /// <summary>参加処理 — 原典: RegistTournamentJoin</summary>
    public async Task<(bool Ok, int UpdatedCount)> JoinAsync(
        long seqNo, MajakPlayer player, string memberNo)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        if (!_plans.TryGetValue(seqNo, out var plan)) return (false, 0);

        if (plan.JoinMoney > 0)
        {
            if (player.GamMoney < plan.JoinMoney) return (false, 0);
            player.GamMoney -= plan.JoinMoney;
        }

        var normalizedMemberNo = string.IsNullOrWhiteSpace(memberNo) ? "00" : memberNo;
        var (ok, count) = await repo.MergeJoinAsync(
            player.MemberNo, seqNo, TournamentJoinStatus.Join, normalizedMemberNo);

        if (!ok)
        {
            player.GamMoney += plan.JoinMoney;
            return (false, 0);
        }

        plan.PlayerNum++;
        await repo.UpdatePlayerNumAsync(seqNo, 1);
        return (true, count);
    }

    /// <summary>参加キャンセル — 原典: RegistTournamentJoinCancel</summary>
    public async Task<(bool Ok, int UpdatedCount)> CancelJoinAsync(
        long seqNo, MajakPlayer player)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        if (!_plans.TryGetValue(seqNo, out var plan)) return (false, 0);

        var (ok, count) = await repo.MergeJoinAsync(
            player.MemberNo, seqNo, TournamentJoinStatus.Cancel);

        if (!ok) return (false, 0);

        if (plan.JoinMoney > 0)
            player.GamMoney += plan.JoinMoney;

        plan.PlayerNum = Math.Max(0, plan.PlayerNum - 1);
        await repo.UpdatePlayerNumAsync(seqNo, -1);
        return (true, count);
    }

    // ─────────────────────────────── マッチング ──────────────────────────

    /// <summary>
    /// JOIN → WAIT 遷移 (マッチング開始時刻になった計画を処理)
    /// 原典: PreTournamentMatching
    /// </summary>
    public async Task PreMatchingAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        var now  = DateTime.Now;
        var targets = _plans.Values
            .Where(p => p.PlayStatus == TournamentPlanStatus.Join
                     && now >= p.MatchStartDt)
            .ToList();

        foreach (var plan in targets)
        {
            // 参加者が半数以下なら破棄
            var joiners = await repo.SelectJoinListAsync(plan.SeqNo);
            if (joiners.Count <= plan.MaxPlayerNum / 2)
            {
                plan.PlayStatus = TournamentPlanStatus.Reject;
                await repo.BulkUpdateJoinStatusAsync(
                    joiners.Select(j => j.MemberNo), TournamentJoinStatus.Exit);
                await repo.InsertUserPresentsAsync(BuildTournamentReturnMoneyPresents(
                    plan, joiners, TournamentPlanStatus.Reject));
                await repo.UpdatePlanStatusAsync(plan);
                _useRoomNum -= plan.MaxRoomNum;
                _logger.LogInformation("Tournament {SeqNo} rejected (not enough players).", plan.SeqNo);
                continue;
            }

            // マッチング実行: WAIT に遷移し対局割り当て作成
            plan.PlayStatus = TournamentPlanStatus.Wait;
            var newDetails = BuildMatchDetails(plan, joiners);
            _details[plan.SeqNo] = newDetails.ToDictionary(d => d.SubId);

            await repo.UpdatePlanStatusAsync(plan);
            await repo.MergeDetailsAsync(newDetails);
            _logger.LogInformation(
                "Tournament {SeqNo} matched: {Count} rooms.", plan.SeqNo, newDetails.Count);
        }
    }

    /// <summary>
    /// WAIT → PLAY 遷移 (NextStartDt を過ぎた計画のルームを起動)
    /// 原典: GoTournamentMatching
    /// 戻り値: (SeqNo, [MemberNo, RoomOption, SubId]) のリスト
    ///         → Hub 側でオートマッチングパケットを送る
    /// </summary>
    public async Task<List<TournamentMatchStartInfo>> GoMatchingAsync()
    {
        var now     = DateTime.Now;
        var result  = new List<TournamentMatchStartInfo>();

        // §４: GoMatching は _lock ではなく小さなスナップショットで安全に参照する。
        //          PostMatching との競合回避は _lock で行う。
        var targets = _plans.Values
            .Where(p => p.PlayStatus == TournamentPlanStatus.Wait
                     && now >= p.NextStartDt)
            .ToList();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        foreach (var plan in targets)
        {
            plan.PlayStatus = TournamentPlanStatus.Play;
            await repo.UpdatePlanStatusAsync(plan);

            if (!_details.TryGetValue(plan.SeqNo, out var detailMap)) continue;
            var startingDetails = detailMap.Values.Where(detail => !detail.IsFinished).ToList();
            foreach (var detail in startingDetails)
                detail.StartDt = now;
            if (startingDetails.Count > 0)
                await repo.MergeDetailsAsync(startingDetails);

            foreach (var detail in startingDetails)
            {
                // §３: NPC (“*AI*”) は除外して実プレイヤーのみに通知
                var realMembers = detail.PlayerMemberNo
                    .Where(m => !string.IsNullOrEmpty(m) && m != TournamentConst.NpcMemberNo)
                    .ToList();

                var players = realMembers
                    .Select(memberNo => _session.GetByMember(memberNo))
                    .Where(player => player is not null)
                    .Cast<MajakPlayer>()
                    .ToList();

                var presentMembers = players.Select(player => player.MemberNo).ToHashSet(StringComparer.Ordinal);
                var missingMembers = realMembers.Where(memberNo => !presentMembers.Contains(memberNo)).ToList();
                if (missingMembers.Count > 0)
                {
                    await repo.BulkUpdateJoinStatusAsync(missingMembers, TournamentJoinStatus.Exit);
                    for (int i = 0; i < detail.PlayerMemberNo.Length; i++)
                    {
                        if (missingMembers.Contains(detail.PlayerMemberNo[i], StringComparer.Ordinal))
                            detail.PlayerMemberNo[i] = "";
                    }
                }

                int roomId = 0;
                string channelId = players.FirstOrDefault()?.ChannelId ?? "";
                if (players.Count == 0)
                {
                    detail.StartDt = now;
                    detail.EndDt = now;
                    for (int i = 0; i < detail.GradeMemberNo.Length; i++)
                        detail.GradeMemberNo[i] = detail.JoinMemberNo[i];
                    plan.PlayEndCount++;
                    continue;
                }

                if (players.Count > 0 && !string.IsNullOrEmpty(channelId))
                {
                    var room = _session.CreateReservedRoom(
                        channelId,
                        roomOption: plan.RoomOption,
                        moneyRate: 1,
                        minMoney: 0,
                        maxMoney: long.MaxValue,
                        isPrivate: false,
                        subId: ExtractSubId(channelId));
                    room.TournamentSeqNo = plan.SeqNo;
                    room.TournamentSubId = detail.SubId;
                    room.LimitCnt = players.Count;
                    detail.RoomId = room.RoomId;
                    roomId = room.RoomId;

                    _session.RegisterPendingMatch(new PendingAutoMatch
                    {
                        RoomId = room.RoomId,
                        ChannelId = channelId,
                        ExpectedMembers = players.Select(player => player.MemberNo).ToArray(),
                        RoomOption = plan.RoomOption,
                        Players = players,
                    });
                }

                var info = new TournamentMatchStartInfo
                {
                    SeqNo      = plan.SeqNo,
                    SubId      = detail.SubId,
                    RoomId     = roomId,
                    ChannelId  = channelId,
                    RoomOption = plan.RoomOption,
                    MemberNos  = players.Select(player => player.MemberNo).ToList(),
                };
                result.Add(info);
            }
        }
        return result;
    }

    /// <summary>
    /// 対局終了後処理 — 原典: PostTournamentMatching
    /// </summary>
    public async Task PostMatchingAsync()
    {
        // §４: PostMatching 全体を _lock で保護
        await _lock.WaitAsync();
        try
        {
        await PostMatchingCoreAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task PostMatchingCoreAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        var now = DateTime.Now;

        var playing = _plans.Values
            .Where(p => p.PlayStatus == TournamentPlanStatus.Play)
            .ToList();

        foreach (var plan in playing)
        {
            if (!_details.TryGetValue(plan.SeqNo, out var detailMap)) continue;

            bool allDone = detailMap.Values.All(d => d.IsFinished);
            bool timeout = now >= plan.NextEndDt;
            if (!allDone && !timeout) continue;

            // タイムアウトした未終了対局を強制終了
            var timedOutDetails = detailMap.Values.Where(detail => !detail.IsFinished).ToList();
            foreach (var d in timedOutDetails)
            {
                d.EndDt = now;
                for (int i = 0; i < 4; i++)
                    d.GradeMemberNo[i] = d.JoinMemberNo[i];
                plan.PlayEndCount++;
            }
            if (timedOutDetails.Count > 0)
                await repo.UpdateDetailResultsAsync(timedOutDetails);

            var (maxPhase, _) = TournamentTables.GetPlayInfo(plan.MaxPlayerNum, plan.PlayMode);

            if (plan.PlayPhase < maxPhase)
            {
                // 次ラウンドへ
                int phaseUnit = plan.PlayNum == TournamentPlayNum.OnePlay
                    ? TournamentConst.PhaseFull : TournamentConst.PhaseHalf;
                plan.PlayPhase += phaseUnit;
                plan.PlayStatus = TournamentPlanStatus.Wait;

                // 次ラウンドの詳細作成
                var winners = CollectWinners(plan, detailMap);
                var pt      = TournamentTables.GetPlayTime(plan.PlayTime)
                              ?? new TournamentPlayTimeInfo(plan.PlayTime, 30, 15, 25);
                TournamentTables.SetNextStartAndCut(plan);

                var nextDetails = BuildNextRoundDetails(plan, winners, plan.NextStartDt, pt);
                foreach (var nd in nextDetails)
                    detailMap[nd.SubId] = nd;

                await repo.UpdatePlanStatusAsync(plan);
                await repo.MergeDetailsAsync(nextDetails);
            }
            else
            {
                // 決勝終了
                plan.PlayStatus = TournamentPlanStatus.End;
                SetFinalResult(plan, detailMap);

                var joiners = await repo.SelectJoinListAsync(plan.SeqNo);
                await repo.BulkUpdateJoinStatusAsync(
                    joiners.Select(j => j.MemberNo), TournamentJoinStatus.End);
                var presents = BuildTournamentResultPresents(plan);
                await UpdatePlannerManageAndTitlePresentAsync(repo, plan, presents);
                await repo.InsertUserPresentsAsync(presents);
                await repo.UpdatePlanStatusAsync(plan);

                _useRoomNum -= plan.MaxRoomNum;
                _logger.LogInformation("Tournament {SeqNo} ended.", plan.SeqNo);
            }
        }
    }

    /// <summary>対局結果を DetailMap に反映 — GameLogicService から呼ばれる</summary>
    public async Task ReportMatchEndAsync(long seqNo, int subId,
        string[] gradePlayerMemberNos, string[] gradeMemberNos, int[] pointSums)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        if (!_details.TryGetValue(seqNo, out var detailMap)) return;
        if (!detailMap.TryGetValue(subId, out var detail)) return;

        var joinNoByPlayerNo = Enumerable.Range(0, detail.PlayerMemberNo.Length)
            .Where(i => !string.IsNullOrEmpty(detail.PlayerMemberNo[i]))
            .ToDictionary(i => detail.PlayerMemberNo[i], i => detail.JoinMemberNo[i], StringComparer.Ordinal);

        detail.EndDt        = DateTime.Now;
        detail.GradePlayerMemberNo = gradePlayerMemberNos;
        detail.GradeMemberNo = gradePlayerMemberNos
            .Select((memberNo, index) => joinNoByPlayerNo.TryGetValue(memberNo, out var joinMemberNo)
                ? joinMemberNo
                : index < gradeMemberNos.Length ? gradeMemberNos[index] : $"{index + 1:D2}")
            .ToArray();

        var usedDetailIndexes = new HashSet<int>();
        for (int index = 0; index < gradePlayerMemberNos.Length && index < pointSums.Length; index++)
        {
            int detailIndex = FindTournamentDetailIndex(detail, gradePlayerMemberNos[index], usedDetailIndexes);
            if (detailIndex < 0) continue;

            usedDetailIndexes.Add(detailIndex);
            if (_plans.TryGetValue(seqNo, out var pointPlan)
                && pointPlan.PlayPhase % TournamentConst.PhaseFull != 0)
            {
                detail.PointTmp[detailIndex] = pointSums[index];
            }
            detail.Point[detailIndex] += pointSums[index];
        }

        // 2試合制の場合: SetTournamentResultRank で最終順位を決定
        // 原典: HMajRoomServer::SetTournamentResultRank
        if (_plans.TryGetValue(seqNo, out var plan) && plan.PlayNum == TournamentPlayNum.TwoPlay)
            SetTournamentResultRank(plan, detail);

        await repo.UpdateDetailResultAsync(detail);

        if (_plans.TryGetValue(seqNo, out plan))
        {
            if (plan.PlayPhase % TournamentConst.PhaseFull == 0)
            {
                var endMembers = detail.GradePlayerMemberNo
                    .Skip(plan.PlayMode)
                    .Where(memberNo => !string.IsNullOrEmpty(memberNo))
                    .ToList();
                await repo.BulkUpdateJoinStatusAsync(endMembers, TournamentJoinStatus.End);
            }

            plan.PlayEndCount++;
        }
    }

    private static int FindTournamentDetailIndex(TournamentDetail detail, string memberNo, HashSet<int> usedIndexes)
    {
        for (int i = 0; i < detail.PlayerMemberNo.Length; i++)
        {
            if (usedIndexes.Contains(i)) continue;
            if (detail.PlayerMemberNo[i] == memberNo) return i;
        }

        if (memberNo == TournamentConst.NpcMemberNo || string.IsNullOrEmpty(memberNo))
        {
            for (int i = 0; i < detail.PlayerMemberNo.Length; i++)
            {
                if (usedIndexes.Contains(i)) continue;
                if (string.IsNullOrEmpty(detail.PlayerMemberNo[i]) || detail.PlayerMemberNo[i] == TournamentConst.NpcMemberNo)
                    return i;
            }
        }

        return -1;
    }

    // ─────────────────────────────────────────────────────────────────────
    // SetTournamentResultRank / MakeTournamentResultStr
    // 原典: HMajRoomServer::SetTournamentResultRank + MakeTournamentResultStr
    // ───────────────────────────────────────────────────────────────────
    /// <summary>
    /// 2試合制: 1回戦+2回戦の合計で最終順位を決定。
    /// 原典: HMajRoomServer::SetTournamentResultRank
    ///   通算 → max(一二戦) → 1回戦 の優先順でソート
    ///   結果を detail.GradePlayerMemberNo[] / GradeMemberNo[] に記録
    /// </summary>
    private static void SetTournamentResultRank(TournamentPlan plan, TournamentDetail detail)
    {
        // 1試合制: そのまま反映済み
        if (plan.PlayNum != TournamentPlayNum.TwoPlay) return;

        // 2試合終了フェーズ以外は順位記録なし
        if (plan.PlayPhase % TournamentConst.PhaseFull != 0) return;

        // 小さいスコア(マイナス対策)を求める
        int worst1st = detail.PointTmp.Min();
        int worst2nd = detail.Point.Zip(detail.PointTmp).Select(p => p.First - p.Second).Min();

        // ソートキー文字列 → セットに入れて辞書順ソート(降順)
        var entries = new SortedSet<string>(Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.Ordinal)));
        for (int i = 0; i < 4; i++)
        {
            string key = MakeTournamentResultStr(
                detail.JoinMemberNo[i],
                detail.PointTmp[i],
                detail.Point[i] - detail.PointTmp[i],
                worst1st, worst2nd);
            entries.Add(key);
        }

        // 順位(降順) → GradeXxx に記録
        var memberMap = Enumerable.Range(0, 4)
            .ToDictionary(i => detail.JoinMemberNo[i], i => detail.PlayerMemberNo[i]);

        int rank = 0;
        foreach (var e in entries)
        {
            string memberNo = e.Substring(9, 2);  // 10-11桁目が MemberNo
            detail.GradeMemberNo[rank] = memberNo;
            detail.GradePlayerMemberNo[rank] = memberMap.TryGetValue(memberNo, out var mid)
                ? (mid == TournamentConst.NpcMemberNo ? "" : mid)
                : "";
            rank++;
        }
    }

    /// <summary>
    /// 2試合制結果ソート用文字列を作成。
    /// 原典: HMajRoomServer::MakeTournamentResultStr
    ///   書式: "%03d%03d%03d%s" (通算、MAX連屋、1回戦、MemberNo)
    /// </summary>
    private static string MakeTournamentResultStr(
        string memberNo, int first, int second, int worstFirst, int worstSecond)
    {
        int p1   = first  + (-worstFirst);
        int p2   = second + (-worstSecond);
        int tot  = p1 + p2;
        int best = Math.Max(p1, p2);
        return $"{tot:D3}{best:D3}{p1:D3}{memberNo}";
    }

    /// <summary>
    /// 2試合制結果をクライアント向けペイロードに変換。
    /// 原典: HMajRoomServer::AddToParser_UpdateTournamentResult
    ///   MemberNo / Grade(勝ち抜けフラグ) / PointTotal / Point1st / Point2nd
    /// </summary>
    public string GetPixForMemberNo(string memberNo)
        => string.IsNullOrEmpty(memberNo) ? "" : (_session.GetPixByMemberNo(memberNo) ?? "");

    public IReadOnlyDictionary<string, object?>? GetTournamentResultPayload(long seqNo, int subId)
    {
        if (!_plans.TryGetValue(seqNo, out var plan)) return null;
        if (!_details.TryGetValue(seqNo, out var detailMap)) return null;
        if (!detailMap.TryGetValue(subId, out var detail)) return null;

        // 2試合制の終了フェーズのみ対象
        bool isTwoPlayFinal = plan.PlayNum == TournamentPlayNum.TwoPlay
            && plan.PlayPhase % TournamentConst.PhaseFull == 0;
        if (!isTwoPlayFinal)
            return new Dictionary<string, object?>
            {
                [Key.TournamentTotalReportCnt] = 0,
                ["tournamentTotalReportCnt"] = 0,
            };

        int maxPhase = TournamentTables.GetPlayInfo(plan.MaxPlayerNum, plan.PlayMode).MaxPhase;
        bool isFinal = maxPhase == plan.PlayPhase;  // 決勝: 勝ち抜け表示なし

        var scoreMap = Enumerable.Range(0, 4)
            .ToDictionary(i => detail.JoinMemberNo[i], i => (
                Total: detail.Point[i],
                First: detail.PointTmp[i]
            ));

        var payload = new Dictionary<string, object?>
        {
            [Key.TournamentTotalReportCnt] = 4,
            ["tournamentTotalReportCnt"] = 4,
        };

        var items = Enumerable.Range(0, 4).Select(i =>
        {
            string mno = detail.GradeMemberNo[i];
            var sc     = scoreMap.TryGetValue(mno, out var s) ? s : default;
            int grade = (i < plan.PlayMode && !isFinal) ? 1 : 0;
            string pix = GetPixForMemberNo(detail.GradePlayerMemberNo[i]);
            payload[$"{Key.TournamentTotalReport}{i}"] =
                $"{pix}\t{grade}\t{sc.Total}\t{sc.First}\t{sc.Total - sc.First}\t";
            return new
            {
                pix,
                grade,  // 勝ち抜けフラグ
                pointTotal = sc.Total,
                point1st   = sc.First,
                point2nd   = sc.Total - sc.First,
            };
        }).ToList();

        payload["tournamentTotalReport"] = items;
        return payload;
    }

    // ──────────────────────────────── helpers ────────────────────────────

    /// <summary>
    /// ランダム割り当てで第1ラウンドの対局詳細を生成 — 原典: GetTournamentPlayTable (1回目)
    /// </summary>
    private static List<TournamentDetail> BuildMatchDetails(
        TournamentPlan plan, List<TournamentJoin> joiners)
    {
        var (_, subStart, subEnd) = TournamentTables.GetRoomInfo(
            plan.PlayPhase, plan.MaxPlayerNum, plan.PlayMode);
        TournamentTables.SetNextStartAndCut(plan);

        // ランダムシャッフル
        var shuffled = joiners.OrderBy(_ => Random.Shared.Next()).ToList();
        // NPC で不足を補完
        while (shuffled.Count < plan.MaxPlayerNum)
            shuffled.Add(new TournamentJoin { MemberNo = TournamentConst.NpcMemberNo });

        var pt      = TournamentTables.GetPlayTime(plan.PlayTime)
                      ?? new TournamentPlayTimeInfo(plan.PlayTime, 30, 15, 25);
        var endPlan = plan.NextEndDt;

        var details = new List<TournamentDetail>();
        int idx = 0;
        for (int subId = subStart; subId <= subEnd; subId++)
        {
            var d = new TournamentDetail { SeqNo = plan.SeqNo, SubId = subId };
            d.StartPlanDt = plan.NextStartDt;
            d.EndPlanDt   = endPlan;
            for (int seat = 0; seat < 4 && idx < shuffled.Count; seat++, idx++)
            {
                d.PlayerMemberNo[seat] = shuffled[idx].MemberNo == TournamentConst.NpcMemberNo
                    ? "" : shuffled[idx].MemberNo;
                d.JoinMemberNo[seat] = $"{idx + 1:D2}";
            }
            details.Add(d);
        }
        return details;
    }

    /// <summary>前ラウンドの勝者から次ラウンドの詳細を生成 — 原典: GetTournamentPlayTable (2回目以降)</summary>
    private static List<TournamentDetail> BuildNextRoundDetails(
        TournamentPlan plan, List<string> winners, DateTime nextDt,
        TournamentPlayTimeInfo pt)
    {
        var (_, subStart, subEnd) = TournamentTables.GetRoomInfo(
            plan.PlayPhase, plan.MaxPlayerNum, plan.PlayMode);

        var endPlan = nextDt.AddMinutes(pt.PlayTimeMax);
        var details = new List<TournamentDetail>();
        int idx = 0;
        for (int subId = subStart; subId <= subEnd; subId++)
        {
            var d = new TournamentDetail
            {
                SeqNo = plan.SeqNo, SubId = subId,
                StartPlanDt = nextDt, EndPlanDt = endPlan,
            };
            for (int seat = 0; seat < 4 && idx < winners.Count; seat++, idx++)
            {
                d.PlayerMemberNo[seat] = winners[idx];
                d.JoinMemberNo[seat] = $"{idx + 1:D2}";
            }
            details.Add(d);
        }
        return details;
    }

    /// <summary>前ラウンドの勝ち上がり者リストを収集 — 原典: GetTournamentPlayTable winners</summary>
    private static List<string> CollectWinners(
        TournamentPlan plan, Dictionary<int, TournamentDetail> detailMap)
    {
        int winCount = plan.PlayMode == TournamentPlayMode.OneWin ? 1 : 2;
        return detailMap.Values
            .Where(d => d.IsFinished)
            .SelectMany(d => d.GradePlayerMemberNo.Take(winCount).Where(m => !string.IsNullOrEmpty(m)))
            .ToList();
    }

    private static void SetFinalResult(
        TournamentPlan plan, Dictionary<int, TournamentDetail> detailMap)
    {
        int maxSubId = detailMap.Keys.Max();
        if (!detailMap.TryGetValue(maxSubId, out var final)) return;
        for (int i = 0; i < 4; i++)
            plan.ResultMemberNo[i] = final.GradePlayerMemberNo[i];
    }

    private static List<UserPresentRecord> BuildTournamentResultPresents(TournamentPlan plan)
    {
        var presents = new List<UserPresentRecord>();
        for (int i = 0; i < 4; i++)
        {
            if (plan.GradeMoney[i] <= 0 || string.IsNullOrEmpty(plan.ResultMemberNo[i])) continue;
            presents.Add(new UserPresentRecord
            {
                MemberNo    = plan.ResultMemberNo[i],
                PresentNum  = plan.GradeMoney[i],
                PresentKbn  = TournamentPresentKind.ResultGrade,
                PresentKind = TournamentPresentItemKind.Money,
            });
        }

        long planPrize = (long)(TournamentConst.JoinMoneyGetProb * (plan.JoinMoney * plan.PlayerNum));
        if (planPrize > 0 && !string.IsNullOrEmpty(plan.PlanMemberNo))
        {
            presents.Add(new UserPresentRecord
            {
                MemberNo    = plan.PlanMemberNo,
                PresentNum  = planPrize,
                PresentKbn  = TournamentPresentKind.ResultPlan,
                PresentKind = TournamentPresentItemKind.Money,
            });
        }
        return presents;
    }

    private static async Task UpdatePlannerManageAndTitlePresentAsync(
        TournamentRepository repo, TournamentPlan plan, List<UserPresentRecord> presents)
    {
        if (string.IsNullOrEmpty(plan.PlanMemberNo)) return;

        var planner = await repo.SelectJoinAsync(plan.PlanMemberNo)
            ?? new TournamentJoin { MemberNo = plan.PlanMemberNo };
        planner.TotManageNum++;

        if (plan.MaxPlayerNum >= TournamentConst.ExtraManageValue)
        {
            planner.ManageNum++;
            string? titleId = TournamentTables.GetTitleIdForManageCount(planner.ManageNum);
            if (!string.IsNullOrEmpty(titleId))
            {
                presents.Add(new UserPresentRecord
                {
                    MemberNo    = plan.PlanMemberNo,
                    PresentNum  = 1,
                    PresentKbn  = TournamentPresentKind.Title,
                    PresentKind = TournamentPresentItemKind.MajakTitle,
                    PresentId   = titleId,
                });
            }
        }

        await repo.MergePlannerManageAsync(planner);
    }

    private static List<UserPresentRecord> BuildTournamentReturnMoneyPresents(
        TournamentPlan plan, IEnumerable<TournamentJoin> joiners, int planStatus)
    {
        int joinKind = planStatus == TournamentPlanStatus.Reject
            ? TournamentPresentKind.RejectJoin
            : TournamentPresentKind.StopJoin;
        int planKind = planStatus == TournamentPlanStatus.Reject
            ? TournamentPresentKind.RejectPlan
            : TournamentPresentKind.StopPlan;

        var presents = new List<UserPresentRecord>();
        if (plan.JoinMoney > 0)
        {
            foreach (var joiner in joiners)
            {
                if (string.IsNullOrEmpty(joiner.MemberNo)) continue;
                presents.Add(new UserPresentRecord
                {
                    MemberNo    = joiner.MemberNo,
                    PresentNum  = plan.JoinMoney,
                    PresentKbn  = joinKind,
                    PresentKind = TournamentPresentItemKind.Money,
                });
            }
        }

        long planMoney = TournamentTables.CalcPlanMoney(plan.GradeMoney);
        if (planMoney > 0 && !string.IsNullOrEmpty(plan.PlanMemberNo))
        {
            presents.Add(new UserPresentRecord
            {
                MemberNo    = plan.PlanMemberNo,
                PresentNum  = planMoney,
                PresentKbn  = planKind,
                PresentKind = TournamentPresentItemKind.Money,
            });
        }
        return presents;
    }

    /// <summary>試合終了日時を計算 — 原典: SetTournamentPlaySchedule</summary>
    private static DateTime CalculatePlayEndDt(
        DateTime startDt, int players, int mode, int playNum, int playTimeNo)
    {
        var pt = TournamentTables.GetPlayTime(playTimeNo);
        if (pt == null) return startDt;

        int totalRounds = TournamentTables.GetMaxPlayNum(players, mode, playNum);
        return startDt.AddMinutes(pt.PlayTimeMin * totalRounds);
    }

    private static void BuildSchedule(TournamentPlan plan, TournamentPlayTimeInfo pt)
    {
        int totalRounds = TournamentTables.GetMaxPlayNum(
            plan.MaxPlayerNum, plan.PlayMode, plan.PlayNum);
        if (totalRounds == 0) return;

        var dt = plan.PlayStartDt;
        plan.StartPlanDtAll.Clear();
        for (int i = 0; i < totalRounds; i++)
        {
            plan.StartPlanDtAll.Add(dt.ToString("yyyy/MM/dd HH:mm:ss"));
            dt = dt.AddMinutes(pt.PlayTimeMin);
        }
        plan.PlaySchedule = string.Join('|', plan.StartPlanDtAll);

        // NextStart は最初の対局開始
        plan.NextStartDt = plan.PlayStartDt;
        plan.NextEndDt   = plan.PlayStartDt.AddMinutes(pt.PlayTimeMax);
        plan.NextCutDt   = plan.PlayStartDt.AddMinutes(pt.PlayCutTime);

        // MatchStartDt: 5分前
        plan.MatchStartDt = plan.PlayStartDt.AddMinutes(-TournamentConst.PlayStartBefore);
        // ViewEndDt: 全試合終了後60分
        plan.ViewEndDt = dt.AddMinutes(TournamentConst.PlayEndAfter);
    }

    private bool IsInLimitPeriod(DateTime from, DateTime to)
    {
        return _limits.Any(lim =>
            lim.LimitValid == 1 &&
            ((lim.LimitStartDt <= from && from <= lim.LimitEndDt) ||
             (lim.LimitStartDt <= to   && to   <= lim.LimitEndDt) ||
             (from <= lim.LimitStartDt && lim.LimitEndDt <= to)));
    }

    private static string ExtractSubId(string channelId)
        => channelId.Length >= 11 ? channelId.Substring(6, 5) : channelId;

    // ─────────────────────────────────── 制限時間強制停止 ───────────────────

    /// <summary>
    /// 制限期間に重なるトーナメントを強制停止する。
    /// 原典: HMajChnlServer::SetTournamentStopByLimitTime
    ///   制限テーブル (m_mapTournamentLimit) に抵触するプランを STOP 状態に遷移。
    ///   参加者全員の JoinStatus を EXIT に更新する。
    /// TournamentBackgroundService.TickAsync() から毎ループ呼ばれる。
    /// </summary>
    /// <summary>
    /// 主催者によるトーナメントキャンセル — 原典: ProcessCommand_TournamentCancel 相当
    /// MATCHSTARTDT 以前であれば参加費を全員に返金してプランを Reject に設定する。
    /// </summary>
    public async Task<(bool Ok, string FailCode)> CancelPlanAsync(long seqNo, string organizerId)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        if (!_plans.TryGetValue(seqNo, out var plan))
            return (false, "E_TOURNAMENT_NOT_FOUND");

        if (plan.PlanMemberNo != organizerId)
            return (false, "E_TOURNAMENT_NOT_ORGANIZER");

        if (plan.PlayStatus != TournamentPlanStatus.Init
         && plan.PlayStatus != TournamentPlanStatus.Join)
            return (false, "E_TOURNAMENT_ALREADY_STARTED");

        if (DateTime.Now >= plan.MatchStartDt)
            return (false, "E_TOURNAMENT_MATCHSTART_PASSED");

        var joiners = await repo.SelectJoinListAsync(seqNo);
        plan.PlayStatus = TournamentPlanStatus.Reject;
        await repo.BulkUpdateJoinStatusAsync(
            joiners.Select(j => j.MemberNo), TournamentJoinStatus.Exit);
        await repo.InsertUserPresentsAsync(BuildTournamentReturnMoneyPresents(
            plan, joiners, TournamentPlanStatus.Reject));
        await repo.UpdatePlanStatusAsync(plan);

        _logger.LogInformation("Tournament {SeqNo} cancelled by organizer {Id}.", seqNo, organizerId);
        return (true, "");
    }

    public async Task StopTournamentsByLimitAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        var toStop = _plans.Values
            .Where(p => p.PlayStatus != TournamentPlanStatus.End
                     && p.PlayStatus != TournamentPlanStatus.Reject
                     && p.PlayStatus != TournamentPlanStatus.Stop
                     && IsInLimitPeriod(p.JoinStartDt, p.PlayEndDt))
            .ToList();

        foreach (var plan in toStop)
        {
            plan.PlayStatus = TournamentPlanStatus.Stop;
            var joiners = await repo.SelectJoinListAsync(plan.SeqNo);
            foreach (var j in joiners)
            {
                j.JoinStatus = TournamentJoinStatus.Exit;
            }
            await repo.BulkUpdateJoinStatusAsync(
                joiners.Select(j => j.MemberNo), TournamentJoinStatus.Exit);
            await repo.InsertUserPresentsAsync(BuildTournamentReturnMoneyPresents(
                plan, joiners, TournamentPlanStatus.Stop));
            await repo.UpdatePlanStatusAsync(plan);
            _useRoomNum -= plan.MaxRoomNum;
            _logger.LogInformation(
                "Tournament {SeqNo} forced STOP due to limit period.", plan.SeqNo);
        }
    }

    /// <summary>
    /// 制限テーブルを DB から再ロードする。
    /// 原典: HMajChnlServer::ReloadTournamentLimit
    /// TournamentBackgroundService が日次または起動時に呼び出す。
    /// </summary>
    public async Task ReloadLimitsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<TournamentRepository>();
        try
        {
            var limits = await repo.SelectLimitsAsync();
            _limits.Clear();
            _limits.AddRange(limits);
            _logger.LogInformation("TournamentService: limits reloaded ({Count}).", _limits.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TournamentService.ReloadLimitsAsync failed.");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 結果型: GoMatchingAsync の戻り値
// ─────────────────────────────────────────────────────────────────────────────
public class TournamentMatchStartInfo
{
    public long         SeqNo      { get; set; }
    public int          SubId      { get; set; }
    public int          RoomId     { get; set; }
    public string       ChannelId  { get; set; } = "";
    public string       RoomOption { get; set; } = "";
    public List<string> MemberNos  { get; set; } = new();
}
