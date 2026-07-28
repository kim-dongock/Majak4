using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MajakServer.Hubs;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Infrastructure;

/// <summary>
/// Auto-matching timer. Legacy equivalent: HMajChnlServer::OnTimer TIMERID_MAJANG_AUTOMATCHING.
///
/// Every 3 seconds, scan matching queues for all channels.
/// Once 4 players are matched, create a reserved room and send mjkc2e (AutoMatching) to each player.
///
/// Processing summary (C# port of GoAutoMatching):
///   1. GetMatchableChannelIds() finds channels with at least 4 queued players.
///   2. TryMatch() takes 4 players from the queue.
///   3. SessionService.CreateRoom() reserves a room.
///   4. Add all players to the room SignalR group.
///   5. Send mjkc2e to all players, including roomId and roomOption.
///   6. Notify the channel with mjkroom (roomState:created).
/// </summary>
public class AutoMatchingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory      _scopeFactory;
    private readonly PlayerSessionService      _session;
    private readonly IHubContext<MajakGameHub> _hub;
    private readonly RoomRegistryService?      _roomRegistry;
    private readonly ILogger<AutoMatchingBackgroundService> _logger;
    private readonly ChannelServerSettings     _serverSettings;

    // Legacy: GET_TIMER_PERIOD(TIMERID_MAJANG_AUTOMATCHING) * 3000ms = 3 seconds.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    // Default room option used by auto-matching.
    // Legacy: GoAutoMatching switch(subId[3]) default = "1200000010000".
    // index 13 is gemGame ('0'=none, '1'=one gem, '2'=big gem), overwritten by onStartNewGame.
    private const string DefaultRoomOption = "1200000010000";

    // GemGame probability table from onStartNewGame.
    // Legacy: static const HITTBL t = {4000, 1000};
    //   0~9999 の乱数:
    //     x < 1000                  => BIG_GEM_GAME (2): 10%, +20 gems for 1st/2nd.
    //     x < 1000+4000 = 5000      => ONE_GEM_GAME (1): 40%, +2 gems for 1st/2nd.
    //     otherwise                 => NOT_GEM_GAME (0): 50%.
    // Reward table: gemtbl[] = {0, 2, 20}; see GetGemCountToGet.
    private static readonly int[] GemTbl = { 0, 2, 20 };

    public AutoMatchingBackgroundService(
        IServiceScopeFactory      scopeFactory,
        PlayerSessionService      session,
        IHubContext<MajakGameHub> hub,
        IOptions<ChannelServerSettings> serverSettings,
        ILogger<AutoMatchingBackgroundService> logger,
        RoomRegistryService? roomRegistry = null)
    {
        _scopeFactory    = scopeFactory;
        _session         = session;
        _hub             = hub;
        _roomRegistry    = roomRegistry;
        _serverSettings  = serverSettings.Value;
        _logger          = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoMatchingBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoMatchingBackgroundService: tick error.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        foreach (var channelId in _session.GetMatchableChannelIds())
        {
            var memberNos = _session.TryMatch(channelId, memberNo =>
            {
                var player = _session.GetByMember(memberNo);
                return player == null ? null : GetMatchingRating(player, channelId);
            });
            if (memberNos == null) continue;

            _logger.LogInformation(
                "AutoMatching matched. channelId={ChannelId} members=[{Members}]",
                channelId, string.Join(",", memberNos));

            await CreateAutoRoomAsync(channelId, memberNos, ct);
        }
    }

    private static int GetMatchingRating(MajakServer.Models.Player.MajakPlayer player, string channelId)
    {
        string subId = channelId.Length >= 11 ? channelId.Substring(6, 5) : "";
        bool isCup = subId.Length > 2 && subId[2] == 'C';
        return isCup && subId.Length > 4 && subId[4] == 'A'
            ? player.RegularRecord.Rating
            : player.ActiveRecord.Rating;
    }

    private static string ResolveAutoRoomOption(string subId)
    {
        bool isGrade = subId.Length > 2 && subId[2] == 'G';
        char gameType = subId.Length > 3 ? subId[3] : '\0';

        if (isGrade)
        {
            return gameType switch
            {
                '6' => "0100020000001",
                '7' => "1100020000001",
                _   => "1100020000001",
            };
        }

        return gameType switch
        {
            '6' => "0200020000000",
            '7' => "1200020000000",
            _   => DefaultRoomOption,
        };
    }

    private async Task CreateAutoRoomAsync(
        string channelId, string[] memberNos, CancellationToken ct)
    {
        // Resolve player sessions from memory.
        var players = memberNos
            .Select(id => _session.GetByMember(id))
            .Where(p => p != null)
            .ToList();

        if (players.Count < 4)
        {
            _logger.LogWarning(
                "AutoMatching [{Channel}]: only {Count}/4 players found in session. Skipping.",
                channelId, players.Count);
            return;
        }

        // GemGame judgment, equivalent to onStartNewGame.
        string subId    = channelId.Length >= 11 ? channelId.Substring(6, 5) : "";
        bool canGemGame = subId.Length == 5 && subId[2] != 'C' && subId[4] > 'A';
        int gemGame     = 0;
        if (canGemGame)
        {
            int x = Random.Shared.Next(10000);
            gemGame = x < 1000 ? 2 : x < 5000 ? 1 : 0;
        }
        string roomOptionBase = ResolveAutoRoomOption(subId);
        var optChars = roomOptionBase.ToCharArray();
        if (optChars.Length > 13) optChars[13] = (char)('0' + gemGame);
        string roomOption = new string(optChars);

        // Create a reserved room with empty seats.
        // Legacy: AddReservePlayer keeps players in reserved state until each mjkc6e (AutoEnterRoom) arrives.
        // Cup channels attach CupConfig (JudgementType / CupId / CupSeq) to the room.
        Infrastructure.CupConfig? cupConfig = null;
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var masterCache = scope.ServiceProvider.GetRequiredService<Infrastructure.MasterCacheService>();
            var cupConfigs  = await masterCache.GetCupConfigsAsync();
            cupConfig       = cupConfigs.FirstOrDefault(c => c.ChannelId == channelId);
        }

        var room = _session.CreateReservedRoom(
            channelId,
            roomOption: roomOption,
            moneyRate:  1,
            minMoney:   0,
            maxMoney:   long.MaxValue,
            isPrivate:  false,
            cupId:            cupConfig?.CupId            ?? 0,
            cupSeq:           cupConfig?.CupSeq           ?? 0,
            cupJudgementType: cupConfig?.JudgementType    ?? -1,
            cupPointSumType:  cupConfig?.CupPointSumType  ?? 0,
            cupMaxMatchCntLimit: cupConfig?.MaxMatchCntLimit ?? -1,
            cupConditionRegular: cupConfig?.ConditionRegular ?? 0,
            cupConditionBilling: cupConfig?.ConditionBilling ?? 0,
            cupEntryLimited:     cupConfig?.EntryLimited     ?? false,
            cupNormalYakuCondition: cupConfig?.NormalYakuCondition ?? "",
            cupYakumanCondition:    cupConfig?.YakumanCondition    ?? "",
            subId: subId);
        room.ServerUrl = _serverSettings.ResolveUrl(channelId);

        // Store SubId on the room for channel type checks.
        room.SubId = subId;

        // The owner is automatically entered when creating a normal room, but auto-matching keeps all 4 players reserved.
        // PendingAutoMatch tracks every matched player.
        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId          = room.RoomId,
            ChannelId       = channelId,
            ExpectedMembers = memberNos,
            RoomOption      = roomOption,
            Players         = players!,
        });

        // Notify each player with mjkc2e and pass room information.
        // Legacy: pJoiner->GetSocket()->SendPacket(clAutoMatching).
        // The client receives this packet and responds with mjkc6e (AutoEnterRoom).
        foreach (var player in players)
        {
            if (player == null) continue;
            string ipAddress = ResolveHost(_serverSettings.ResolveUrl(channelId));
            await _hub.Clients.Client(player.ConnectionId)
                .SendAsync(Cmd.AutoMatching,
                    new Dictionary<string, object>
                    {
                        ["result"] = 1,
                        [GKey.Result] = GKey.ValueSuccess,
                        ["memberNo"] = player.Pix,
                        ["pix"] = player.Pix,
                        [GKey.Pix] = player.Pix,
                        [GKey.GameId] = GameConst.ServiceId,
                        ["subId"] = subId,
                        [GKey.SubId] = subId,
                        ["channelId"] = channelId,
                        [GKey.ChannelId] = channelId,
                        ["roomId"] = room.RoomId,
                        [GKey.RoomId] = room.RoomId,
                        [GKey.RoomTitle] = " ",
                        [GKey.RoomPwd] = "",
                        ["roomMinCnt"] = 4,
                        [GKey.RoomMinCnt] = 4,
                        ["roomLimitCnt"] = 4,
                        [GKey.RoomLimitCnt] = 4,
                        ["roomOption"] = roomOption,
                        [GKey.RoomOption] = roomOption,
                        [GKey.IPAddress] = ipAddress,
                        [GKey.Port] = ResolvePort(_serverSettings.ResolveUrl(channelId)),
                        [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                        ["connectFor"] = GKey.ValueConnectForGameJoin,
                        ["gemGame"] = gemGame,
                    }, ct);

            _logger.LogInformation(
                "AutoMatching result sent. channelId={ChannelId} roomId={RoomId} memberNo={MemberNo} connectionId={ConnectionId}",
                channelId, room.RoomId, player.MemberNo, player.ConnectionId);

            // REST polling fallback for clients that are not connected by WebSocket.
            _session.StoreMatchResult(player.MemberNo, new PlayerSessionService.MatchResult(
                RoomId:     room.RoomId,
                ChannelId:  channelId,
                ServerUrl:  _serverSettings.ServerUrl,
                RoomOption: roomOption,
                GemGame:    gemGame));
        }

        // Notify the whole channel that a room was created.
        var roomStatePacket = RoomStatePayload.Build(room, "created");
        roomStatePacket["moneyRate"] = 1;
        await _hub.Clients.Group($"chanel_{channelId}")
            .SendAsync(Cmd.RoomState, roomStatePacket, ct);

        // FAILEROOM timer (legacy: TIMERID_MAJANG_FAILEROOM = 5 seconds).
        // If not all players send mjkc6e within 5 seconds, send mjkc5e to entered players and close the room.
        int capturedRoomId = room.RoomId;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
            await FireFaileRoomAsync(capturedRoomId, channelId);
        }, CancellationToken.None);

        _logger.LogInformation(
            "AutoMatching [{Channel}]: room {RoomId} reserved for [{Members}]. FAILEROOM timer started.",
            channelId, room.RoomId, string.Join(",", memberNos));
    }

    private static string ResolveHost(string serverUrl)
        => Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ? uri.Host : serverUrl;

    private static int ResolvePort(string serverUrl)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri)) return 0;
        if (!uri.IsDefaultPort) return uri.Port;
        return uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
    }

    // FAILEROOM timeout handling.
    // Legacy: HMajRoomServer::OnTimer TIMERID_MAJANG_FAILEROOM.
    // If not every reserved player enters, send mjkc5e to entered players and destroy the room.
    private async Task FireFaileRoomAsync(int roomId, string channelId)
    {
        var pending = _session.ExpirePendingMatch(roomId);
        if (pending == null) return;   // Everyone entered and the pending record was already removed.

        _logger.LogWarning(
            "FAILEROOM [{Channel}] room {RoomId}: timeout. entered={Entered}/{Expected}",
            channelId, roomId,
            pending.EnteredMembers.Count, pending.ExpectedMembers.Length);

        // 入室済みプレイヤーに mjkc5e (commandMajAutoExitRoom) を送信
        foreach (var memberNo in pending.EnteredMembers)
        {
            var p = _session.GetByMember(memberNo);
            if (p == null) continue;

            var payload = RoomForceExitPayload.Build(
                GKey.ValuePlayer,
                (int)p.SeatPos,
                p.Pix,
                p.NickName,
                1); // ROOM_FORCEEXIT_STARTERROR

            await _hub.Clients.Client(p.ConnectionId).SendAsync(Cmd.AutoExitRoom, payload);

            // Remove from the SignalR group and update the session.
            await _hub.Groups.RemoveFromGroupAsync(p.ConnectionId, $"room_{roomId}");
            _session.LeaveRoom(p);
        }

        var afterRoom = _session.GetRoom(roomId);
        if (afterRoom == null || afterRoom.HasNoActiveMembers)
        {
            _session.RemoveRoom(roomId);
            if (_roomRegistry != null)
                await _roomRegistry.RemoveRoomAsync(roomId, channelId);
        }
        else if (_roomRegistry != null)
        {
            await _roomRegistry.UpdateMemberCountAsync(roomId, channelId, afterRoom.ActivePlayerCount);
        }
    }
}
