using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// グレードランキング補助サービス — 原典: HMajRootServer の _RATING_GRADE_MODE 部分
///
/// 担当機能:
///   1. プロプレイヤー判定 (IsMjkProMemberNo) — EVTUSERMAST EVTCODE='5333'
///   2. ゲームクリアカウンター (AddGameClearCnt / FlushGameClearCntAsync)
///   3. グレード別プレイヤー数指数 (GetGradeIndexStrAsync) — Redis TTL 5分キャッシュ経由
///   4. 月次ランキング確定バッチ (PastFixGradeRankingAsync) — PastFixGradeRanking 相当
///   5. 月次バッチ実行中フラグ (IsBatchRunning) — GetGradeModeManage の DURING 相当
///
/// Singleton。DB アクセスには IServiceScopeFactory 経由でスコープを生成して使用する。
/// グレード別件数はローカルメモリに持たず Redis キャッシュ (PlayerRepository.GetGradeRankCountsAsync)
/// に委譲する。マスターデータはすべて Redis で共有管理する方針に準拠。
/// </summary>
public class GradeRankService
{
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ILogger<GradeRankService>         _log;

    // ─── プロプレイヤーキャッシュ ───────────────────────────────────────────
    private Dictionary<string, string> _proPlayers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _proLock = new();

    // ─── ゲームクリアカウンター ───────────────────────────────────────────
    private long _gameClearCnt = 0;

    // ─── 月次バッチ実行中フラグ ────────────────────────────────────────────
    // 原典: HMajRootServer::m_bGradeRankMonthlyDuring
    // PastFixGradeRankingAsync 実行中は true。
    // クライアントからの RatingRankInfo 要求時にこのフラグを確認し、
    // true の場合は DURING ステータスを返してリトライを促す。
    private volatile bool _isBatchRunning = false;

    /// <summary>
    /// 月次ランキング確定バッチ実行中かどうか — 原典: m_bGradeRankMonthlyDuring
    /// true のとき RatingRankInfoCommand は result=0 (DURING) を返す。
    /// </summary>
    public bool IsBatchRunning => _isBatchRunning;

    // ─── 定数 (HMajDef.h / HMajCommon.h) ─────────────────────────────────
    // s_nMajRankAllKind[]
    private static readonly int[] AllRankKinds =
    {
        0,  1,  2,  3,  4,  5,  6,  7,  8,  9,   // GRADE_10_KYU ... GRADE_1_KYU
        10, 11, 12, 13, 14, 15, 16, 17, 18, 19,   // GRADE_1_DAN  ... GRADE_10_DANI
        98, 99                                      // RATING_RANK_BEGINNER, RATING_RANK_ALL
    };
    private const int RATING_RANK_ALL      = 99;
    private const int RATING_RANK_BEGINNER = 98;
    // GRADE_BATCH_STATUS: INIT=0, PAST=1, NOW=2, DURING=3
    private const int GRADE_BATCH_PAST   = 1;
    private const int GRADE_BATCH_NOW    = 2;
    private const int GRADE_BATCH_DURING = 3;

    public GradeRankService(IServiceScopeFactory scopeFactory, ILogger<GradeRankService> log)
    {
        _scopeFactory = scopeFactory;
        _log          = log;
    }

    /// <summary>起動時に初期データをロードする</summary>
    public async Task InitAsync()
    {
        await ReloadProPlayersAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // プロプレイヤー
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>指定 MemberNo がプロプレイヤーかどうかを判定する — 原典: IsMjkProMemberNo</summary>
    public bool IsPro(string memberNo)
    {
        _proLock.EnterReadLock();
        try   { return _proPlayers.ContainsKey(memberNo); }
        finally { _proLock.ExitReadLock(); }
    }

    public string GetProPictureUrl(string memberNo)
    {
        _proLock.EnterReadLock();
        try { return _proPlayers.TryGetValue(memberNo, out var pictureUrl) ? pictureUrl : ""; }
        finally { _proLock.ExitReadLock(); }
    }

    /// <summary>EVTUSERMAST からプロプレイヤーリストを再ロードする</summary>
    public async Task ReloadProPlayersAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
            var list = await repo.GetProPlayerListAsync();

            var set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in list)
                set[item.MemberNo] = item.PictureUrl;
            _proLock.EnterWriteLock();
            try   { _proPlayers = set; }
            finally { _proLock.ExitWriteLock(); }

            _log.LogInformation("[GradeRank] ProPlayerList reloaded: {Count} entries.", set.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[GradeRank] ReloadProPlayers failed: {Msg}", ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ゲームクリアカウンター
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>ゲーム終了時にカウンターをインクリメントする — 原典: AddGameClearCnt</summary>
    public void AddGameClearCnt() => Interlocked.Increment(ref _gameClearCnt);

    /// <summary>
    /// 蓄積したカウントを KT_GAMECNTMAST に書き込む — 原典: OnTimer(TIMERID_MAJANG_GAMECLEARCOUNTER)
    /// 書き込み失敗時はカウントを元に戻す。
    /// </summary>
    public async Task FlushGameClearCntAsync()
    {
        long cnt = Interlocked.Exchange(ref _gameClearCnt, 0);
        if (cnt <= 0) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
            await repo.UpdateGameClearCntAsync(cnt);
        }
        catch (Exception ex)
        {
            // 失敗したらカウントを戻す
            Interlocked.Add(ref _gameClearCnt, cnt);
            _log.LogWarning("[GradeRank] FlushGameClearCnt failed: {Msg}", ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // グレード別プレイヤー数 / 指数計算
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 上位 XX.XX% の文字列を返す — 原典: GetGradeModeIndex → szIndex
    /// myRank は 1-based 順位。rankKind=99(ALL) なら myRank/全体数、
    /// それ以外はそのグレード以上の人数 / 全体数。
    /// グレード件数は PlayerRepository.GetGradeRankCountsAsync() 経由で Redis TTL 5分キャッシュを使用。
    /// ローカルメモリには保持しない。
    /// </summary>
    public async Task<string> GetGradeIndexStrAsync(int rankDate, int rankKind, int myRank)
    {
        if (rankKind == RATING_RANK_BEGINNER) return "";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
            var counts = await repo.GetGradeRankCountsAsync(rankDate);
            if (!counts.TryGetValue(RATING_RANK_ALL, out int total) || total <= 0) return "";

            float fIndex = rankKind == RATING_RANK_ALL
                ? (float)myRank / total * 100f
                : (float)SumGradesAbove(rankKind, counts) / total * 100f;

            return fIndex.ToString("F2");
        }
        catch (Exception ex)
        {
            _log.LogWarning("[GradeRank] GetGradeIndexStr failed: {Msg}", ex.Message);
            return "";
        }
    }

    /// rankKind 以上のグレードの人数合計 — 原典: GetGradeIndexPlayerSum
    private static int SumGradesAbove(int rankKind, Dictionary<int, int> counts)
    {
        int sum = 0;
        foreach (var kv in counts)
        {
            if (kv.Key != RATING_RANK_BEGINNER && kv.Key != RATING_RANK_ALL
                && kv.Key >= rankKind)
                sum += kv.Value;
        }
        return sum;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 月次ランキング確定バッチ
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 前月のランキングを確定させ、今月の管理レコードを登録する
    /// — 原典: HMajRootServer::PastFixGradeRanking (月1日に実行)
    ///
    /// 処理順:
    ///   1. MJK_GRADEMANAGE の前月レコードを NOW→DURING (楽観ロック)
    ///   2. MJK_GRADERANK 前月分の各 RankKind ごとに RANK 列を更新
    ///   3. 今月分の MJK_GRADEMANAGE レコードを INSERT
    ///   4. 前月レコードを DURING→PAST に更新
    /// </summary>
    public async Task PastFixGradeRankingAsync()
    {
        var now = DateTime.Now;
        int rankDateNow  = int.Parse(now.ToString("yyyyMM"));
        int rankDatePast = now.Month == 1
            ? (now.Year - 1) * 100 + 12
            : now.Year * 100 + (now.Month - 1);

        _log.LogInformation("[GradeRank] PastFixGradeRanking start. past={Past} now={Now}",
            rankDatePast, rankDateNow);
        _isBatchRunning = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();

            // 1. 楽観ロック: BATCHFLAG NOW(2) → DURING(3)
            int updCnt = await repo.UpdateGradeManageStatusAsync(rankDatePast, GRADE_BATCH_NOW, GRADE_BATCH_DURING);
            if (updCnt == 0)
            {
                _log.LogInformation("[GradeRank] PastFix skipped (already processed or no row). past={Past}", rankDatePast);
                return;
            }
            if (updCnt > 1)
            {
                _log.LogError("[GradeRank] PastFix UpdateGradeManageStatus returned too many rows: {Cnt}", updCnt);
                return;
            }

            // 2. 各 RankKind の RANK 列を確定
            foreach (int rankKind in AllRankKinds)
            {
                var rows = await repo.LoadGradeRankForConfirmAsync(rankDatePast, rankKind);
                if (rows.Count == 0) continue;
                await repo.UpdateGradeRankConfirmAsync(rankDatePast, rankKind, rows, now);
            }

            // 3. 今月の管理レコードを INSERT (既に存在する場合はスキップ)
            await repo.InsertGradeManageAsync(rankDateNow, now);

            // 4. 前月を PAST: DURING(3) → PAST(1)
            int releaseCnt = await repo.UpdateGradeManageStatusAsync(rankDatePast, GRADE_BATCH_DURING, GRADE_BATCH_PAST);
            if (releaseCnt != 1)
            {
                _log.LogError("[GradeRank] PastFix release failed. past={Past} count={Count}", rankDatePast, releaseCnt);
                return;
            }

            _log.LogInformation("[GradeRank] PastFixGradeRanking done. past={Past} now={Now}",
                rankDatePast, rankDateNow);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[GradeRank] PastFixGradeRanking error.");
        }
        finally
        {
            _isBatchRunning = false;
        }
    }
}
