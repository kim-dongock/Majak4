using MajakServer.Hubs;
using MajakServer.Models.Protocol;
using MajakServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace MajakServer.Infrastructure;

/// <summary>
/// グレードランキング定期処理タイマー — 原典: HMajRootServer の 3 つのタイマー
///
///   TIMERID_MAJANG_GAMECLEARCOUNTER      (60 秒間隔)  → FlushGameClearCntAsync
///   TIMERID_MAJANG_GRADERANKING_DAYLYTIMER (5 分間隔) → グレード別件数は Redis TTL で管理するためタイマー不要
///   TIMERID_MAJANG_GRADERANKING_MONTHLYTIMER          → PastFixGradeRankingAsync (毎月1日)
///
/// SendNoticeToAll — 全接続クライアントへの公知メッセージ配信も提供する。
/// プライマリリーダーのみ月次バッチを実行する (マルチサーバー安全)。
/// </summary>
public class GradeRankBackgroundService : BackgroundService
{
    private readonly GradeRankService                       _gradeRank;
    private readonly PrimaryLeaderService                   _leader;
    private readonly PlayerSessionService                   _session;
    private readonly IHubContext<MajakGameHub>              _hub;
    private readonly ILogger<GradeRankBackgroundService>    _log;

    private static readonly TimeSpan TickInterval      = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ProReloadInterval = TimeSpan.FromHours(1);

    public GradeRankBackgroundService(
        GradeRankService                    gradeRank,
        PrimaryLeaderService                leader,
        PlayerSessionService                session,
        IHubContext<MajakGameHub>           hub,
        ILogger<GradeRankBackgroundService> log)
    {
        _gradeRank = gradeRank;
        _leader    = leader;
        _session   = session;
        _hub       = hub;
        _log       = log;
    }

    /// <summary>
    /// 全接続クライアントへ公知メッセージを送信する — 原典: HMajRootServer::SendNoticeToAll
    /// </summary>
    public async Task SendNoticeToAllAsync(string message, int color = 0)
    {
        foreach (string channelId in _session.GetActiveChannelIds())
        {
            await _hub.Clients.Group($"chanel_{channelId}")
                .SendAsync(Cmd.Notice, NoticePayload.Channel(message, channelId, color));
        }
        _log.LogInformation("[Notice] Broadcast: {Msg}", message);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("GradeRankBackgroundService started.");


        DateTime lastProReload      = DateTime.MinValue;
        int      lastMonthProcessed = 0;   // 今月分を1回だけ実行するための記録

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // ─── 毎分: ゲームクリアカウンターを DB に書き込む ──────────────
                // (原典: TIMERID_MAJANG_GAMECLEARCOUNTER — 60秒間隔)
                await _gradeRank.FlushGameClearCntAsync();


                // ─── 1時間: プロプレイヤーリストを再ロード ───────────────────
                if ((DateTime.Now - lastProReload) >= ProReloadInterval)
                {
                    await _gradeRank.ReloadProPlayersAsync();
                    lastProReload = DateTime.Now;
                }

                // ─── 毎月1日: ランキング確定バッチ ───────────────────────────
                // (原典: TIMERID_MAJANG_GRADERANKING_MONTHLYTIMER — 月1日のみ実行)
                // プライマリリーダーのみ実行 (多重実行防止は UpdateGradeManageStatus の楽観ロックで担保)
                if (_leader.IsLeader)
                {
                    var now = DateTime.Now;
                    int thisMonth = int.Parse(now.ToString("yyyyMM"));
                    if (now.Day == 1 && lastMonthProcessed != thisMonth)
                    {
                        _log.LogInformation("[GradeRank] Monthly batch triggered. month={Month}", thisMonth);
                        await _gradeRank.PastFixGradeRankingAsync();
                        lastMonthProcessed = thisMonth;
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "GradeRankBackgroundService: error in tick.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }
}
