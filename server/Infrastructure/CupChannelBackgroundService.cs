using Microsoft.AspNetCore.SignalR;
using MajakServer.Hubs;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Infrastructure;

/// <summary>
/// カップチャンネルタイマー管理 — 原典: HMajChnlServer::OnTimer TIMERID_MAJANG_CHNLCTRL
///
/// レガシーではチャンネルサーバーごとに動作していたが、
/// C# 版では単一のホステッドサービスが全カップチャンネルを管理する。
///
/// マルチサーバー構成:
///   PrimaryLeaderService で Redis リーダー選出を行う。
///   リーダーでないサーバーは tick をスキップする。
///   サーバーが落ちると TTL=30 秒後に他サーバーがリーダーに昇格しタイマーを再開する。
///   レガシー相当: HMajChnlServer が CHANELMAST.MACHINE で自担当チャンネルのみ処理。
///
/// 状態遷移 (原典: HMajChnlInfo::m_nStatus):
///   ST_STANBY(0) → now >= DateFrom → ST_RUN(1)  : GoCupStart
///   ST_RUN(1)    → now >= DateTo   → ST_STOP(2) : GoCupStopStart (パケット送信なし)
///   ST_STOP(2)   → now >= DateTo+1h→ ST_STANBY  : GoCupStop (mjkc13e 送信 + DB 更新)
/// </summary>
public class CupChannelBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory      _scopeFactory;
    private readonly IHubContext<MajakGameHub> _hub;
    private readonly ILogger<CupChannelBackgroundService> _logger;
    private readonly PrimaryLeaderService _leader;
    private readonly MasterCacheService   _masterCache;
    private readonly PlayerSessionService _session;

    // 原典: GET_TIMER_PERIOD(TIMERID_MAJANG_CHNLCTRL) * 60s = 1分
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    // カップ状態キャッシュ
    private readonly Dictionary<string, CupChannelState> _states = new();
    private readonly Dictionary<string, DateTime> _nextEventNoticeAt = new(StringComparer.Ordinal);

    public CupChannelBackgroundService(
        IServiceScopeFactory      scopeFactory,
        IHubContext<MajakGameHub> hub,
        PrimaryLeaderService      leader,
        MasterCacheService        masterCache,
        PlayerSessionService      session,
        ILogger<CupChannelBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub          = hub;
        _leader       = leader;
        _masterCache  = masterCache;
        _session      = session;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CupChannelBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            if (!_leader.IsLeader)
            {
                _logger.LogDebug("CupChannelBackgroundService: not leader, skip.");
                continue;
            }

            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CupChannelBackgroundService: tick error.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var cups = await LoadCupConfigsAsync();
        var now  = DateTime.Now;

        foreach (var cup in cups)
        {
            _states.TryGetValue(cup.ChannelId, out var prev);
            var status = prev?.Status ?? CupStatus.Stanby;

            switch (status)
            {
                case CupStatus.Stanby:
                    // ─── GoCupStart: DateFrom 到達 → ST_RUN ───────────────
                    // 原典: GoCupStart() → STATUS=ST_RUN / SERVICE_NOOP
                    if (now >= cup.DateFrom && now <= cup.DateTo)
                    {
                        _states[cup.ChannelId] = new CupChannelState(
                            CupStatus.Running, cup.DateFrom, cup.DateTo, cup.IsFestive,
                            StopStartedAt: null);
                        await UpdateCupStatusAsync(cup, 1, ct); // ST_RUN=1
                        _logger.LogInformation("Cup channel {Id}: GoCupStart.", cup.ChannelId);
                    }
                    break;

                case CupStatus.Running:
                    // ─── GoCupStopStart: DateTo 到達 → ST_STOP ───────────
                    // 原典: GoCupStopStart() → STATUS=ST_STOP / SERVICE_PAUSE
                    // ここではパケット送信なし。GoCupStop は +1時間 後。
                    if (now >= cup.DateTo)
                    {
                        _states[cup.ChannelId] = new CupChannelState(
                            CupStatus.Stopping, cup.DateFrom, cup.DateTo, cup.IsFestive,
                            StopStartedAt: now);
                        await UpdateCupStatusAsync(cup, 2, ct); // ST_STOP=2
                        _logger.LogInformation("Cup channel {Id}: GoCupStopStart (waiting 1h).", cup.ChannelId);
                    }
                    break;

                case CupStatus.Stopping:
                    // ─── GoCupStop: ST_STOP + DateTo+1h 到達 → mjkc13e 送信 ──
                    // 原典:
                    //   GMetpParser clChannelStop(G::serviceChannel, MAJ::commandMajChannelStop)
                    //   clChannelStop.AddValue(G::keyDummy, G::valueDummy)
                    //   SendDataToAll(clChannelStop)
                    //   UpdateCupStatus(ST_STANBY) + CreateChannel (CupMemberCntClear)
                    var stopStarted = prev!.StopStartedAt ?? cup.DateTo;
                    if (now >= stopStarted.AddHours(1))
                    {
                        // mjkc13e 送信
                        await _hub.Clients.Group($"chanel_{cup.ChannelId}")
                            .SendAsync(Cmd.ChannelStop, new { dummy = 1 }, ct);

                        // CupMemberCntClear: CHANELWT の人数をゼロリセット (原典)
                        await CupMemberCntClearAsync(cup.ChannelId, ct);

                        // DB 更新: STATUS=ST_STANBY(0)
                        await UpdateCupStatusAsync(cup, 0, ct);

                        _states[cup.ChannelId] = new CupChannelState(
                            CupStatus.Stanby, cup.DateFrom, cup.DateTo, cup.IsFestive,
                            StopStartedAt: null);
                        _logger.LogInformation("Cup channel {Id}: GoCupStop — mjkc13e sent.", cup.ChannelId);
                    }
                    break;
            }

            // ─── フェスティブカップ: 11:00 / 23:00 (分0-3) にスコア通知 ───
            // 原典: SendCupScoreNotice() — HMajDBObject::GetCupTopScore から最高スコアを取得して通知
            if (status == CupStatus.Running && cup.IsFestive)
            {
                bool isNoticeTime = (now.Hour == 11 || now.Hour == 23)
                                    && now.Minute is >= 0 and <= 3;
                if (isNoticeTime)
                {
                    int topScore = await GetCupTopScoreAsync(cup.ChannelId);
                    // 原典: sprintf(szValue, MAJAK_STR_CUPSCORENOTICE, nMaxCupPoint)
                    string message = $"[{cup.ChannelName}] 最高スコア: {topScore}";
                    await _hub.Clients.Group($"chanel_{cup.ChannelId}")
                        .SendAsync(Cmd.Notice, NoticePayload.Channel(message, cup.ChannelId), ct);
                }
            }

            // ─── ハイイベントカップ告知: 開催中は30分ごとに全チャンネルへ通知 ───
            // 原典: HMajEventMaster::OnTimer → HMajRootServer::SendNoticeToAll
            if (IsHiEventChannel(cup) && now >= cup.DateFrom && now <= cup.DateTo)
            {
                if (!_nextEventNoticeAt.TryGetValue(cup.ChannelId, out var nextNoticeAt)
                    || now >= nextNoticeAt)
                {
                    string message = $"現在、雀龍ロビー「{cup.ChannelName}」を開催しています。";
                    await SendNoticeToAllActiveChannelsAsync(message, ct);
                    _nextEventNoticeAt[cup.ChannelId] = now.AddMinutes(30);
                }
            }
            else
            {
                _nextEventNoticeAt.Remove(cup.ChannelId);
            }
        }
    }

    private async Task UpdateCupStatusAsync(CupConfig cup, int status, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
            await repo.UpdateCupStatusAsync(cup.ChannelId, status, cup.CupId, cup.CupSeq);
            // Redis キャッシュを無効化し次回取得時に新しい値を取得させる
            await _masterCache.InvalidateCupConfigsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CupChannelBackgroundService: UpdateCupStatus failed for {Id}.", cup.ChannelId);
        }
    }

    /// <summary>
    /// カップ終了時のメンバー数クリア — 原典: HMajChnlServer::CupMemberCntClear
    /// CHANELWT のメンバー数をゼロにリセットする。
    /// </summary>
    private async Task CupMemberCntClearAsync(string channelId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
            await repo.ResetCupMemberCountAsync(channelId);
            _logger.LogInformation("Cup channel {Id}: CupMemberCntClear done.", channelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CupChannelBackgroundService: CupMemberCntClear failed for {Id}.", channelId);
        }
    }

    /// <summary>
    /// カップ最高スコア取得 — MasterCacheService (Redis TTL 1分) 経由
    /// </summary>
    private async Task<int> GetCupTopScoreAsync(string channelId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
            return await repo.GetCupTopScoreAsync(channelId);
        }
        catch { return 0; }
    }

    private async Task SendNoticeToAllActiveChannelsAsync(string message, CancellationToken ct)
    {
        foreach (string channelId in _session.GetActiveChannelIds())
        {
            await _hub.Clients.Group($"chanel_{channelId}")
                .SendAsync(Cmd.Notice, NoticePayload.Channel(message, channelId), ct);
        }
    }

    private static bool IsHiEventChannel(CupConfig cup)
    {
        string subId = cup.ChannelId.Length >= 11 ? cup.ChannelId[6..11] : cup.ChannelId;
        return subId.Length > 4 && subId[2] == 'C' && subId[4] >= 'F' && subId[4] != 'Z';
    }

    /// <summary>
    /// タイトルホルダーチャンネル入場通知 — 原典: HMajChnlServer::SendTitleHolderNotice
    /// カップタイトルホルダーがチャンネルに入ったときに全員へ通知する。
    /// 呼び出し元: ChannelLifecycleCommands.EnterChannelCommand
    /// </summary>
    public async Task SendTitleHolderNoticeAsync(
        string channelId, string channelName, string nickname, string memberNo)
    {
        // 原典: sprintf(szValue, MAJAK_STR_TITLEHENTERCHANNEL, nickname, memberNo, channelName)
        string message = $"{nickname} ({memberNo}) が [{channelName}] に入場しました";
        await _hub.Clients.Group($"chanel_{channelId}")
            .SendAsync(Cmd.Notice, NoticePayload.Channel(message, channelId));
    }

    private async Task<List<CupConfig>> LoadCupConfigsAsync()
    {
        try
        {
            // MasterCacheService 経由 — Redis キャッシュを使う (TTL 2分)。キャッシュミス時のみ DB 接続を行う。
            return await _masterCache.GetCupConfigsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CupChannelBackgroundService: failed to load cup configs.");
            return new();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 内部状態
// ─────────────────────────────────────────────────────────────────────────────

// 原典: HMajChnlInfo::m_nStatus 相当 (ST_STANBY=0 / ST_RUN=1 / ST_STOP=2)
public enum CupStatus { Stanby = 0, Running = 1, Stopping = 2 }

public record CupChannelState(
    CupStatus Status,
    DateTime  DateFrom,
    DateTime  DateTo,
    bool      IsFestive,
    DateTime? StopStartedAt);

public record CupConfig(
    string   ChannelId,
    string   ChannelName,
    DateTime DateFrom,
    DateTime DateTo,
    bool     IsFestive,
    int      CupId          = 0,
    int      CupSeq         = 0,
    /// <summary>CUP_JTID_NONE=-1 / CUP_JTID_KILL_DIF=7 / CUP_JTID_GAME_SUM=8</summary>
    int      JudgementType  = -1,
    /// <summary>SUM_MAX=1 / SUM_MIX=2 / SUM_SUC=3 (CUP_JTID_GAME_SUM 時のみ使用)</summary>
    int      CupPointSumType = 0,
    int      MaxMatchCntLimit = -1,
    int      ConditionRegular = 0,
    bool     EntryLimited = false,
    int      ConditionBilling = 0,
    int      MinLevel = 0,
    int      MaxLevel = 0,
    string   NormalYakuCondition = "",
    string   YakumanCondition = "");
