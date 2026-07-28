using Microsoft.AspNetCore.SignalR;
using MajakServer.Hubs;
using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using MajakServer.Models.Protocol;
using MajakServer.Services;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Commands.Channel;


public class AutoMatchingCommand : ICommand
{
    private const int EInsufficientMoney = 4;
    private const int EMajLicenseGradeMode = 30009;

    private readonly PlayerSessionService _session;
    private readonly RatingService        _ratingService;
    private readonly MasterCacheService?  _masterCache;
    private readonly PlayerRepository?    _playerRepo;
    private readonly ILogger<AutoMatchingCommand>? _logger;

    public AutoMatchingCommand(PlayerSessionService session, RatingService ratingService,
        MasterCacheService? masterCache = null, PlayerRepository? playerRepo = null,
        ILogger<AutoMatchingCommand>? logger = null)
    {
        _session       = session;
        _ratingService = ratingService;
        _masterCache   = masterCache;
        _playerRepo    = playerRepo;
        _logger        = logger;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        if (!IsAutoMatchingChannel(player.ChannelId))
        {
            _logger?.LogError(
                "AutoMatching ignored: non automatching channel. memberNo={MemberNo} channelId={ChannelId}",
                player.MemberNo, player.ChannelId);
            return;
        }

        // Legacy reference: CheckEnterChannelLimit and billing checks.
        // Reject matching when UNITMONEY cannot be covered.
        if (player.GamMoney <= 0)
        {
            await SendAutoMatchingFailure(ctx, EInsufficientMoney, "所持金額不足で対戦できません。");
            return;
        }

        // Legacy reference: CheckEnterGradeMode for grade and billing checks.
        // IsGradeChannel = subId[2] == 'G'
        var subId = player.ChannelId.Length >= 11 ? player.ChannelId.Substring(6, 5) : "";
        if (subId.Length > 0 && subId[0] == '1' && player.RegularRecord.MatchCnt > 10)
        {
            await SendAutoMatchingFailure(ctx, EInsufficientMoney, "所持金額不足で対戦できません。");
            return;
        }

        bool isGradeChannel = subId.Length >= 3 && subId[2] == 'G';
        if (isGradeChannel)
        {
            if (!_ratingService.CheckEnterGradeMode(player.GradeRecord.Grade, player.GamMoney, subId))
            {
                await SendAutoMatchingFailure(ctx, EMajLicenseGradeMode);
                return;
            }
        }

        if (_masterCache is not null && subId.Length > 2 && subId[2] == 'C')
        {
            if (_playerRepo is not null && await _playerRepo.GetCupStatusAsync(player.ChannelId) == 2)
            {
                await SendAutoMatchingPauseFailure(ctx, "開催時間が終了したため、現在は対戦を行うことができません。");
                return;
            }

            var cup = (await _masterCache.GetCupConfigsAsync()).FirstOrDefault(c => c.ChannelId == player.ChannelId);
            int cupLicense = CupPlayLicense.Check(cup, player);
            if (cupLicense != CupPlayLicense.Success)
            {
                await SendAutoMatchingFailure(ctx, cupLicense);
                return;
            }
        }

        _session.EnqueueMatching(player.ChannelId, player.MemberNo);
        _logger?.LogInformation(
            "AutoMatching queued. memberNo={MemberNo} channelId={ChannelId}",
            player.MemberNo, player.ChannelId);

    }

    private static Task SendAutoMatchingFailure(CommandContext ctx, int failCode, string? message = null)
    {
        var packet = new Dictionary<string, object>
        {
            ["result"] = 0,
            [GKey.Result] = GKey.ValueFailure,
            ["failCode"] = failCode,
            [GKey.FailCode] = failCode,
        };
        if (!string.IsNullOrEmpty(message))
        {
            packet["message"] = message;
            packet[GKey.Message] = message;
        }
        return ctx.Caller.SendAsync(Cmd.AutoMatching, packet);
    }

    private static Task SendAutoMatchingPauseFailure(CommandContext ctx, string message)
    {
        var packet = new Dictionary<string, object>
        {
            ["result"] = 0,
            [GKey.Result] = GKey.ValueFailure,
            ["message"] = message,
            [GKey.Message] = message,
        };
        return ctx.Caller.SendAsync(Cmd.AutoMatching, packet);
    }

    private static bool IsAutoMatchingChannel(string channelId)
    {
        string subId = channelId.Length >= 11 ? channelId.Substring(6, 5) : channelId;
        return subId.Length > 1 && subId[1] == 'Z';
    }
}


public class CancelAutoMatchingCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public CancelAutoMatchingCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;
        if (!IsAutoMatchingChannel(player.ChannelId)) return;

        _session.DequeueMatching(player.ChannelId, player.MemberNo);
        await ctx.Caller.SendAsync(Cmd.CancelAutoMatching, ctx.Payload);
    }

    private static bool IsAutoMatchingChannel(string channelId)
    {
        string subId = channelId.Length >= 11 ? channelId.Substring(6, 5) : channelId;
        return subId.Length > 1 && subId[1] == 'Z';
    }
}

/// <summary>



///










/// </summary>
public class AutoEnterRoomCommand : ICommand
{
    private readonly PlayerSessionService      _session;
    private readonly IHubContext<MajakGameHub> _hub;
    private readonly GameLogicService          _gameLogic;
    private readonly RoomRegistryService?      _roomRegistry;
    private readonly ILogger<AutoEnterRoomCommand>? _logger;

    public AutoEnterRoomCommand(PlayerSessionService session, IHubContext<MajakGameHub> hub,
        GameLogicService gameLogic, RoomRegistryService? roomRegistry = null,
        ILogger<AutoEnterRoomCommand>? logger = null)
    {
        _session      = session;
        _hub          = hub;
        _gameLogic    = gameLogic;
        _roomRegistry = roomRegistry;
        _logger       = logger;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;



        if (!ctx.Payload.TryGetValue(GKey.RoomId, out var roomIdObj)
            || !int.TryParse(roomIdObj?.ToString(), out int roomId))
        {
            _logger?.LogError(
                "AutoEnterRoom failed: roomId required. memberNo={MemberNo} connectionId={ConnectionId}",
                player.MemberNo, ctx.ConnectionId);
            await SendConnectError(ctx, roomId: 0, "roomId required", LegacyErrorCode.InvalidPacket);
            return;
        }

        string connectFor = FirstString(ctx, GKey.ConnectFor, "connectFor");
        string playerType = FirstString(ctx, GKey.PlayerType, "playerType");
        string payloadPlayerId = FirstString(ctx, GKey.Pix, "pix", "memberNo");
        string payloadMemberNo = _session.ResolveMemberNo(payloadPlayerId);



        if (!string.IsNullOrEmpty(payloadMemberNo) && payloadMemberNo != player.MemberNo)
        {
            _logger?.LogError(
                "AutoEnterRoom failed: member no mismatch. roomId={RoomId} sessionMemberNo={SessionMemberNo} payloadPlayerId={PayloadPlayerId} resolvedMemberNo={ResolvedMemberNo}",
                roomId, player.MemberNo, payloadPlayerId, payloadMemberNo);
            _session.RemovePendingMatchMember(roomId, player.MemberNo);
            await SendConnectError(ctx, roomId, "Member No mismatch", LegacyErrorCode.NotMatchSocketId);
            return;
        }

        var room = _session.GetRoom(roomId);
        if (room == null)
        {
            _logger?.LogError(
                "AutoEnterRoom failed: room not found. roomId={RoomId} memberNo={MemberNo}",
                roomId, player.MemberNo);
            await SendConnectError(ctx, roomId, "room not found", LegacyErrorCode.CannotEnterRoom);
            return;
        }

        if (_roomRegistry != null)
        {
            var continueRoom = await _roomRegistry.GetContinueRoomAsync(player.MemberNo);
            if (continueRoom != null && continueRoom.RoomId != roomId)
            {
                await SendConnectError(ctx, continueRoom.RoomId, "対局中のルームへ復帰してください。", LegacyErrorCode.NotEmptyRoom);
                return;
            }
        }

        // Check whether the player is included in PendingAutoMatch.
        // Legacy reference: FindReservePlayer(pPlayer->m_szMemberNo).
        var pending = _session.GetPendingMatch(roomId);
        bool isViewRequest = IsConnectForView(connectFor);
        bool isViewer = isViewRequest;
        if (!isViewer && pending != null && !_session.IsPendingMatchMember(pending, player.MemberNo))
        {
            _logger?.LogError(
                "AutoEnterRoom failed: not a reserved player. roomId={RoomId} memberNo={MemberNo} expected=[{ExpectedMembers}]",
                roomId, player.MemberNo, string.Join(",", pending.ExpectedMembers));
            await SendConnectError(ctx, roomId, "Not a reserved player", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }

        bool hasValidConnectFor = IsConnectForCreate(connectFor)
            || IsConnectForGameJoin(connectFor)
            || IsConnectForView(connectFor);
        if (!hasValidConnectFor)
        {
            _logger?.LogError(
                "AutoEnterRoom failed: invalid connectFor. roomId={RoomId} memberNo={MemberNo} connectFor={ConnectFor}",
                roomId, player.MemberNo, connectFor);
            _session.RemovePendingMatchMember(roomId, player.MemberNo);
            await SendConnectError(ctx, roomId, "Invalid enter room type", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }


        bool alreadyIn = player.RoomId == roomId;
        if (!alreadyIn)
        {




            bool isCreate = string.IsNullOrEmpty(room.CreatorNo) || IsConnectForCreate(connectFor);

            if (isViewer)
            {







                if (playerType != GKey.ValueViewer)
                {
                    await SendConnectError(ctx, roomId, "Cannot enter room", LegacyErrorCode.CannotEnterRoom);
                    return;
                }

                string roomPwd = FirstString(ctx, GKey.RoomPwd, "roomPwd");
                if (!string.IsNullOrEmpty(room.Password) && room.Password != roomPwd)
                {
                    await SendConnectError(ctx, roomId, "Invalid password", LegacyErrorCode.InvalidPassword);
                    return;
                }
                if (room.Viewers.Any(v => v.MemberNo == player.MemberNo)
                    || room.Seats.Any(s => s?.MemberNo == player.MemberNo))
                {
                    await SendConnectError(ctx, roomId, "Same member already in room", LegacyErrorCode.SameUserAlreadyIn);
                    return;
                }

                if (room.State != GameRoomState.Playing)
                {
                    await SendConnectError(ctx, roomId, "Room not in viewable state", LegacyErrorCode.CannotEnterRoom);
                    return;
                }
                if (!room.AddViewer(player))
                {
                    await SendConnectError(ctx, roomId, "Cannot enter room", LegacyErrorCode.CannotEnterRoom);
                    return;
                }
            }
            else
            {
                if (playerType != GKey.ValuePlayer)
                {
                    _session.RemovePendingMatchMember(roomId, player.MemberNo);
                    await SendConnectError(ctx, roomId, "Auto enter room failed", LegacyErrorCode.MajAutoEnterRoomFailed);
                    return;
                }

                bool hasActiveSeat = room.Seats.Any(s => s?.MemberNo == player.MemberNo && !s.IsOutPlayer);
                bool isPlayingSeatReconnect = room.State == GameRoomState.Playing && hasActiveSeat;


                if (hasActiveSeat && !isPlayingSeatReconnect)
                {
                    _session.RemovePendingMatchMember(roomId, player.MemberNo);
                    await SendConnectError(ctx, roomId, "Same member already in room", LegacyErrorCode.SameUserAlreadyIn);
                    return;
                }




                bool isContinue = room.State == GameRoomState.Playing
                    && room.Seats.Any(s => s?.MemberNo == player.MemberNo && s.IsOutPlayer);

                if (isContinue)
                {

                    int seatIdx = _session.ReconnectToRoom(roomId, player);
                    if (seatIdx < 0)
                    {
                        await SendConnectError(ctx, roomId, "Seat not found for continue player", LegacyErrorCode.MajAutoEnterRoomFailed);
                        return;
                    }


                    room.LimitCnt = room.Seats.Count(s => s != null && !s.IsOutPlayer);
                    if (!room.Seats.Any(s => s != null && s.IsOutPlayer))
                        room.LimitCnt = 0;
                    if (_roomRegistry != null)
                        await _roomRegistry.ClearContinueRoomAsync(player.MemberNo);
                }
                else if (isPlayingSeatReconnect)
                {
                    if (!room.RefreshPlayerConnection(player))
                    {
                        await SendConnectError(ctx, roomId, "Seat not found for reconnect player", LegacyErrorCode.MajAutoEnterRoomFailed);
                        return;
                    }
                }
                else
                {
                    if (!isCreate && !string.IsNullOrEmpty(room.Password))
                    {
                        string roomPwd = FirstString(ctx, GKey.RoomPwd, "roomPwd");
                        if (room.Password != roomPwd)
                        {
                            _session.RemovePendingMatchMember(roomId, player.MemberNo);
                            await SendConnectError(ctx, roomId, "Invalid password", LegacyErrorCode.InvalidPassword);
                            return;
                        }
                    }

                    if (isCreate && string.IsNullOrEmpty(room.CreatorNo))
                    {
                        ApplyAutoCreateRoomPayload(room, ctx);
                        room.CreatorNo = player.MemberNo;
                    }

                    bool joined = _session.JoinRoom(roomId, player);
                    if (!joined)
                    {
                        _session.RemovePendingMatchMember(roomId, player.MemberNo);
                        await SendConnectError(ctx, roomId, "Room full", LegacyErrorCode.MajAutoEnterRoomFailed);
                        return;
                    }


                    player.PreMatchMemberNos = BuildPreMatchMemberNos(pending, room, player.MemberNo);
                }
            }


            await ctx.Groups.AddToGroupAsync(ctx.ConnectionId, $"room_{roomId}");



            var memberListPayload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildMemberListPayload(room, player.MemberNo);
            if (Convert.ToInt32(memberListPayload[GKey.Count]) > 0)
                await ctx.Caller.SendAsync(Cmd.MemberList, memberListPayload);



            await _hub.Clients.GroupExcept($"room_{roomId}", ctx.ConnectionId)
                .SendAsync(Cmd.AddMember, MajakServer.Commands.Room.RoomGetMembersCommand.BuildAddMemberPayload(
                    room, player, isViewer ? GKey.ValueViewer : GKey.ValuePlayer));

            await _hub.Clients.Group($"chanel_{player.ChannelId}")
                .SendAsync(Cmd.DeleteMember, new
                {
                    memberNo = player.Pix,
                    pix      = player.Pix,
                    k3e      = player.Pix,
                });

            if (_roomRegistry != null)
                await _roomRegistry.UpdateMemberCountAsync(roomId, player.ChannelId, room.ActivePlayerCount);
        }

        await ctx.Caller.SendAsync(Cmd.AutoEnterRoom, new { result = 1, roomId });




        bool isReconnect = room.State == GameRoomState.Playing && !isViewer;
        if (isReconnect)
        {
            await _gameLogic.SendPaiInfoAsync(room, ctx, player, isInit: true, includeAll: true);
            if (room.PlayHistory.Count > 0)
            {
                await ctx.Caller.SendAsync(Cmd.History, new
                {
                    result       = 1,
                    historyCount = room.PlayHistory.Count,
                    history      = room.PlayHistory,
                });
            }
            await _gameLogic.SendCurrentActionPromptAsync(room, ctx, player);
            return;
        }



        if (isViewer) return;

        var (allEntered, match) = _session.ConfirmAutoEntry(roomId, player.MemberNo);
        _logger?.LogInformation(
            "AutoEnterRoom confirmed. roomId={RoomId} memberNo={MemberNo} allEntered={AllEntered} entered={Entered}/{Expected}",
            roomId,
            player.MemberNo,
            allEntered,
            match?.EnteredMembers.Count ?? 0,
            match?.ExpectedMembers.Length ?? 0);
        if (!allEntered || match == null) return;



        if (await room.EngineLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            try
            {
                _logger?.LogInformation(
                    "AutoEnterRoom all players entered: starting game. roomId={RoomId} members=[{Members}]",
                    roomId, string.Join(",", match.ExpectedMembers));
                await _gameLogic.StartGameLogicAsync(room, ctx);
            }
            finally
            {
                room.EngineLock.Release();
            }
        }
    }



    private static Task SendConnectError(CommandContext ctx, int roomId, string msg, int code)
        => ctx.Caller.SendAsync(Cmd.ConnectTypeError,
            RoomConnectErrorPayload.Build(roomId, msg, code));

    private static string FirstString(CommandContext ctx, params string[] keys)
    {
        foreach (string key in keys)
        {
            string value = ctx.GetString(key);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return "";
    }

    private static bool IsConnectForCreate(string value)
        => IsConnectFor(value, GKey.ValueConnectForCreate, "CreateRoom");

    private static bool IsConnectForGameJoin(string value)
        => IsConnectFor(value, GKey.ValueConnectForGameJoin, "GameJoin");

    private static bool IsConnectForView(string value)
        => IsConnectFor(value, GKey.ValueConnectForView, "View");

    private static bool IsConnectFor(string value, string legacyValue, string webAlias)
        => string.Equals(value, legacyValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, webAlias, StringComparison.OrdinalIgnoreCase);

    private static void ApplyAutoCreateRoomPayload(GameRoom room, CommandContext ctx)
    {
        room.RoomTitle = FirstString(ctx, GKey.RoomTitle, "roomTitle");
        room.Password = FirstString(ctx, GKey.RoomPwd, "roomPwd");
        room.RoomOption = FirstString(ctx, GKey.RoomOption, "roomOption");
        room.RoomType = FirstString(ctx, "roomType");
        long unitMoney = ctx.GetLong("unitMoney");
        if (unitMoney <= 0)
            unitMoney = ctx.GetInt("unitMoney");
        if (unitMoney > 0)
            room.UnitMoney = unitMoney;

        int minCnt = ctx.GetInt(GKey.RoomMinCnt, ctx.GetInt("roomMinCnt", room.MinCnt));
        if (minCnt > 0)
            room.MinCnt = Math.Min(minCnt, room.Seats.Length);

        int limitCnt = ctx.GetInt(GKey.RoomLimitCnt, ctx.GetInt("roomLimitCnt", room.LimitCnt));
        if (limitCnt > 0)
        {
            room.LimitCnt = Math.Min(limitCnt, room.Seats.Length);
        }
    }

    private static string[] BuildPreMatchMemberNos(PendingAutoMatch? pending, GameRoom room, string memberNo)
    {
        IEnumerable<string> candidates = pending != null
            ? pending.Players
                .Where(player => player != null)
                .Select(player => player!.MemberNo)
                .Concat(pending.ExpectedMembers)
            : room.Seats.Where(seat => seat != null).Select(seat => seat!.MemberNo);

        return candidates
            .Where(id => !string.IsNullOrEmpty(id) && id != memberNo)
            .Distinct()
            .Take(3)
            .ToArray();
    }

    private static string ToDanName(int gradeLevel) => gradeLevel switch
    {
        0 => "10級",
        1 => "9級",
        2 => "8級",
        3 => "7級",
        4 => "6級",
        5 => "5級",
        6 => "4級",
        7 => "3級",
        8 => "2級",
        9 => "1級",
        10 => "初段",
        11 => "二段",
        12 => "三段",
        13 => "四段",
        14 => "五段",
        15 => "六段",
        16 => "七段",
        17 => "八段",
        18 => "九段",
        19 => "10dan",
        _ => "",
    };
}


/// <remarks>



/// </remarks>
public class GetServerTimeCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {

        //         tm_mon+1, tm_mday, tm_year+1900, tm_hour, tm_min, tm_sec)

        var now = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"));

        string svTime = now.ToString("MM/dd/yyyy HH:mm:ss");

        await ctx.Caller.SendAsync(Cmd.GetServerTime, new Dictionary<string, object>
        {
            [Key.ServerTime] = svTime,
        });
    }
}

