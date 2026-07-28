using Microsoft.AspNetCore.SignalR;
using MajakServer.Hubs;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Infrastructure;

/// <summary>
/// Tournament timer processing. Legacy equivalent: the three HMajChnlServer tournament timers.
///   TIMERID_MAJANG_TOURNAMENT_MANAGETIMER  (1-minute interval)  => PreMatchingAsync
///   TIMERID_MAJANG_TOURNAMENT_PLAYTIMER    (10-second interval) => GoMatchingAsync
///   TIMERID_MAJANG_TOURNAMENT_LIMITTIMER   (5-minute interval)  => PostMatchingAsync
///
/// The port consolidates them into a single 30-second timer.
///
/// Multi-server behavior:
///   PrimaryLeaderService elects a Redis leader.
///   Non-leader servers skip timer processing.
///   If the leader drops, another server becomes leader after the 30-second TTL.
///   Legacy equivalent: HMajChnlServer processed only channels assigned by CHANELMAST.MACHINE.
/// </summary>
public class TournamentBackgroundService : BackgroundService
{
    private readonly TournamentService         _tournament;
    private readonly PlayerSessionService      _session;
    private readonly IHubContext<MajakGameHub> _hub;
    private readonly ILogger<TournamentBackgroundService> _logger;
    private readonly PrimaryLeaderService _leader;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public TournamentBackgroundService(
        TournamentService         tournament,
        PlayerSessionService      session,
        IHubContext<MajakGameHub> hub,
        PrimaryLeaderService      leader,
        ILogger<TournamentBackgroundService> logger)
    {
        _tournament = tournament;
        _session    = session;
        _hub        = hub;
        _leader     = leader;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TournamentBackgroundService started.");

        DateTime lastLimitReload = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            if (!_leader.IsLeader)
            {
                _logger.LogDebug("TournamentBackgroundService: not leader, skip.");
                continue;
            }

            try
            {
                // Reload the restriction table daily (legacy: ReloadTournamentLimit).
                if ((DateTime.Now - lastLimitReload).TotalHours >= 24)
                {
                    await _tournament.ReloadLimitsAsync();
                    lastLimitReload = DateTime.Now;
                }

                // Force-stop tournaments that hit a restricted period (legacy: SetTournamentStopByLimitTime).
                await _tournament.StopTournamentsByLimitAsync();

                // Phase 1: JOIN => WAIT (close entries and create matching groups).
                await _tournament.PreMatchingAsync();

                // Phase 2: WAIT => PLAY (notify players to enter rooms).
                var starts = await _tournament.GoMatchingAsync();
                foreach (var info in starts)
                    await NotifyMatchStartAsync(info, stoppingToken);

                // Phase 3: PLAY => next round or END (post-game processing).
                await _tournament.PostMatchingAsync();

                // Phase 4: FORCEEXITROOM timer equivalent.
                // Legacy: TIMERID_MAJANG_FORCEEXITROOM sends force-exit packets when players do not enter after game start.
                await ForceExitTimeoutRoomsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TournamentBackgroundService: error in tick.");
            }
        }
    }

    /// <summary>
    /// Send auto-matching notifications to tournament participants.
    /// Legacy equivalent: GoTournamentMatching (clAutoMatching send path).
    /// Clients receive Cmd.MajAutoMatching and enter the assigned room.
    /// </summary>
    private async Task NotifyMatchStartAsync(
        TournamentMatchStartInfo info, CancellationToken ct)
    {
        foreach (var memberNo in info.MemberNos)
        {
            var player = _session.GetByMember(memberNo);
            if (player == null) continue;

            await _hub.Clients.Client(player.ConnectionId).SendAsync(
                Cmd.MajAutoMatching,
                new Dictionary<string, object>
                {
                    ["result"] = 1,
                    [GKey.Result] = GKey.ValueSuccess,
                    ["memberNo"] = player.Pix,
                    ["pix"] = player.Pix,
                    [GKey.Pix] = player.Pix,
                    [GKey.GameId] = GameConst.ServiceId,
                    ["subId"] = ExtractSubId(player.ChannelId),
                    [GKey.SubId] = ExtractSubId(player.ChannelId),
                    ["channelId"] = player.ChannelId,
                    [GKey.ChannelId] = player.ChannelId,
                    ["roomId"] = info.RoomId,
                    [GKey.RoomId] = info.RoomId,
                    [GKey.RoomTitle] = " ",
                    [GKey.RoomPwd] = "",
                    ["roomMinCnt"] = info.MemberNos.Count,
                    [GKey.RoomMinCnt] = info.MemberNos.Count,
                    ["roomLimitCnt"] = info.MemberNos.Count,
                    [GKey.RoomLimitCnt] = info.MemberNos.Count,
                    ["roomOption"] = info.RoomOption,
                    [GKey.RoomOption] = info.RoomOption,
                    [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                    ["connectFor"] = "GameJoin",
                    // Tournament identifiers used to set TournamentSeqNo / SubId on the room.
                    ["tournamentNo"] = info.SeqNo,
                    ["tournamentSub"] = info.SubId,
                },
                ct);
        }

        // If a room already exists, set TournamentSeqNo / SubId.
        // AutoEnterRoom can create the room, so search the players' current room here.
        if (info.MemberNos.Count > 0)
        {
            var firstPlayer = _session.GetByMember(info.MemberNos[0]);
            if (firstPlayer?.RoomId != null)
            {
                var room = _session.GetRoom(firstPlayer.RoomId.Value);
                if (room != null && room.TournamentSeqNo == 0)
                {
                    room.TournamentSeqNo = info.SeqNo;
                    room.TournamentSubId = info.SubId;
                }
            }
        }

        _logger.LogInformation(
            "Tournament {SeqNo} sub {SubId}: notified {N} players.",
            info.SeqNo, info.SubId, info.MemberNos.Count);
    }

    private static string ExtractSubId(string channelId)
        => channelId.Length >= 11 ? channelId.Substring(6, 5) : channelId;

    /// <summary>
    /// Handles tournament room force-exit timeouts.
    /// Legacy equivalent: HMajRoomServer::OnTimer(TIMERID_MAJANG_FORCEEXITROOM).
    /// If players do not enter within the allowed time after PLAY starts, send AutoExitRoom.
    /// This targets TournamentService matches in Play state whose NextCutDt has passed.
    /// </summary>
    private async Task ForceExitTimeoutRoomsAsync(CancellationToken ct)
    {
        var now = DateTime.Now;
        var plans = _tournament.GetAllPlans()
            .Where(p => p.PlayStatus == Models.Game.TournamentPlanStatus.Play)
            .ToList();

        foreach (var plan in plans)
        {
            var details = _tournament.GetDetails(plan.SeqNo);
            if (details == null) continue;

            foreach (var detail in details.Values.Where(d => !d.IsFinished && now >= d.EndPlanDt))
            {
                // Notify all entered players to force-exit (legacy: ROOM_FORCEEXIT_NOCOMMAND).
                foreach (var memberNo in detail.PlayerMemberNo.Where(m => !string.IsNullOrEmpty(m)))
                {
                    var player = _session.GetByMember(memberNo);
                    if (player == null) continue;

                    var payload = RoomForceExitPayload.Build(
                        GKey.ValuePlayer,
                        (int)player.SeatPos,
                        player.Pix,
                        player.NickName,
                        2); // ROOM_FORCEEXIT_NOCOMMAND

                    await _hub.Clients.Client(player.ConnectionId).SendAsync(Cmd.AutoExitRoom, payload, ct);
                }
                _logger.LogWarning(
                    "Tournament {SeqNo} sub {SubId}: force exit timeout.",
                    plan.SeqNo, detail.SubId);
            }
        }
    }
}
