using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using MajakServer.Engine;
using MajakServer.Models.Protocol;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Services;
using MajakServer.Repositories.MySQL;
using Microsoft.Extensions.Logging;

namespace MajakServer.Commands.Room;

/// <summary>


/// </summary>
public class SendOkButtonCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public SendOkButtonCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {


        await Task.CompletedTask;
    }
}

/// <summary>



/// </summary>
public class PushOkButtonCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly GameLogicService     _gameLogic;
    private readonly RatingService        _ratingService;
    private readonly ILogger<PushOkButtonCommand>? _log;

    public PushOkButtonCommand(PlayerSessionService session, GameLogicService gameLogic, RatingService ratingService, ILogger<PushOkButtonCommand>? log = null)
    {
        _session       = session;
        _gameLogic     = gameLogic;
        _ratingService = ratingService;
        _log           = log;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.RoomId == null)
        {
            _log?.LogError("PushOkButton skipped: missing player or room. connectionId={ConnectionId} hasPlayer={HasPlayer}", ctx.ConnectionId, player != null);
            return;
        }

        var room = _session.GetRoom(player.RoomId.Value);
        if (room == null)
        {
            _log?.LogError("PushOkButton skipped: room not found. roomId={RoomId} memberNo={MemberNo} connectionId={ConnectionId}", player.RoomId.Value, player.MemberNo, ctx.ConnectionId);
            return;
        }

        await room.EngineLock.WaitAsync();
        try
        {


        if (room.State != GameRoomState.Waiting)
        {
            _log?.LogError("PushOkButton ignored: room is not waiting. roomId={RoomId} state={RoomState} memberNo={MemberNo}", room.RoomId, room.State, player.MemberNo);
            return;
        }

        int nPos = (int)player.SeatPos;
        if (nPos < 0 || nPos >= GameConst.PlayerMaxCount)
        {
            _log?.LogError("PushOkButton skipped: invalid seat. roomId={RoomId} memberNo={MemberNo} seatPos={SeatPos}", room.RoomId, player.MemberNo, nPos);
            return;
        }


        if ((room.IsBeginnerChannel && (player.RegularRecord.MatchCnt > 10 || player.GamMoney < 0))
            || (room.IsGradeChannel && !_ratingService.CheckEnterGradeMode(player.GradeRecord.Grade, player.GamMoney, room.SubId)))
        {
            var lackPayload = new Dictionary<string, object>
            {
                [Key.LackMoney] = 1L,
            };
            await ctx.Caller.SendAsync(Cmd.PushOkButton, lackPayload);
            return;
        }

        int cupLicense = CupPlayLicense.Check(room, player);
        if (cupLicense != CupPlayLicense.Success)
        {
            var lackPayload = new Dictionary<string, object>
            {
                [Key.LackMoney] = 1L,
                [GKey.FailCode] = cupLicense,
                ["failCode"] = cupLicense,
            };
            await ctx.Caller.SendAsync(Cmd.PushOkButton, lackPayload);
            return;
        }


        //   bTrain = (ChannelType == CT_TRAINING)

        //   if(!bTrain || !bHost) { m_bReadyToPlay[nPos] ^= 1; }

        bool isTrain = room.IsTrainingChannel;
        string hostId = room.Seats[0]?.MemberNo ?? room.CreatorNo;
        bool isHost  = player.MemberNo == hostId;

        if (!isTrain || !isHost)
        {

            room.OkButtonStates[nPos] = !room.OkButtonStates[nPos];
        }

        _log?.LogInformation(
            "PushOkButton received. roomId={RoomId} memberNo={MemberNo} seatPos={SeatPos} isTraining={IsTraining} isHost={IsHost} okStates={OkStates}",
            room.RoomId,
            player.MemberNo,
            nPos,
            isTrain,
            isHost,
            string.Join(',', room.OkButtonStates.Select(v => v ? 1 : 0)));


        var okPayload = new Dictionary<string, object>();
        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
            okPayload[$"{Key.OkButton}{i}"] = room.OkButtonStates[i] ? 1 : 0;
        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.SendOkButton, okPayload);

        var okResp = new Dictionary<string, object>
        {
            [Key.LackMoney] = 0L,
        };
        await ctx.Caller.SendAsync(Cmd.PushOkButton, okResp);


        //   if(bTrain && !bHost) { return TRUE; }
        if (isTrain && !isHost) return;


        //   for(i=0..PLAYER_MAX_COUNT) {
        //     if(bTrain && (m_vecRoomMember[i]==NULL || i==nPos)) continue;
        //     if(!m_bReadyToPlay[i]) return TRUE;
        //   }
        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
        {
            if (isTrain && (room.Seats[i] == null || i == nPos)) continue;
            if (!room.OkButtonStates[i])
            {
                _log?.LogInformation("PushOkButton waiting for player. roomId={RoomId} missingSeat={SeatPos} okStates={OkStates}", room.RoomId, i, string.Join(',', room.OkButtonStates.Select(v => v ? 1 : 0)));
                return;
            }
        }


        //   m_pRoomInfo->m_nLimitCnt = m_vecRoomMember.m_stPlayerPos.m_nCount;

        if (isTrain)
        {
            room.LimitCnt = room.PlayerCount;
        }


        //   StartGameProcess();
        //   memset(m_bReadyToPlay, 0, sizeof m_bReadyToPlay);

        //   StartGameLogic();
        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
            room.OkButtonStates[i] = false;

        room.State = GameRoomState.Playing;
        _log?.LogInformation("PushOkButton all ready; starting game. roomId={RoomId} starterMemberNo={MemberNo} playerCount={PlayerCount}", room.RoomId, player.MemberNo, room.PlayerCount);
        await _gameLogic.StartGameLogicAsync(room, ctx);
        }
        finally
        {
            room.EngineLock.Release();
        }
    }
}

/// <summary>



/// </summary>
public class RoomGetMembersCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public RoomGetMembersCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player?.RoomId == null) return;

        var room = _session.GetRoom(player.RoomId.Value);
        if (room == null) return;

        await ctx.Caller.SendAsync(Cmd.MemberList, BuildMemberListPayload(room));
    }

    public static Dictionary<string, object?> BuildAddMemberPayload(GameRoom room, MajakPlayer member, string? playerType = null)
    {
        string hostMemberNo = room.Seats.FirstOrDefault(p => p != null && !p.IsOutPlayer)?.MemberNo ?? room.CreatorNo;
        string hostId = room.Seats.FirstOrDefault(p => p != null && !p.IsOutPlayer)?.Pix ?? "";
        string resolvedPlayerType = playerType ?? (member.IsViewer ? GKey.ValueViewer : GKey.ValuePlayer);
        int playerPos = resolvedPlayerType == GKey.ValueViewer
            ? Math.Max(0, room.Viewers.FindIndex(v => v.MemberNo == member.MemberNo))
            : (int)member.SeatPos;
        int costumeId = member.GetCustomEquip(30);
        int costumeType = member.CustomItems.TryGetValue(costumeId, out var customItem) ? customItem.Kind : 0;
        bool ready = resolvedPlayerType == GKey.ValuePlayer
            && playerPos >= 0
            && playerPos < room.OkButtonStates.Length
            && room.OkButtonStates[playerPos];

        var payload = new Dictionary<string, object?>
        {
            [GKey.RoomHost] = hostId,
            ["roomHost"] = hostId,
            [GKey.PlayerType] = resolvedPlayerType,
            ["playerType"] = resolvedPlayerType,
            [GKey.PlayerPos] = playerPos,
            ["playerPos"] = playerPos,
            ["seatPos"] = playerPos,
            ["ready"] = ready,
            ["isReady"] = ready,
            ["okButton"] = ready ? 1 : 0,
            [GKey.Pix] = member.Pix,
            ["memberNo"] = member.Pix,
            ["pix"] = member.Pix,
            [GKey.AvatarId] = member.AvatarId,
            ["avatarId"] = member.AvatarId,
            [GKey.Name] = member.NickName,
            ["name"] = member.NickName,
            ["nickName"] = member.NickName,
            [GKey.Sex] = member.Sex,
            ["sex"] = member.Sex,
            [GKey.Age] = 0,
            [GKey.Location] = "",
            [GKey.TotMoney] = member.GamMoney,
            [GKey.GamMoney] = member.GamMoney,
            [GKey.MatchCnt] = member.ActiveRecord.MatchCnt,
            [GKey.WinCnt] = member.ActiveRecord.WinCnt,
            [GKey.DefeatCnt] = member.ActiveRecord.DefeatCnt,
            [GKey.DrawCnt] = member.ActiveRecord.DrawCnt,
            [GKey.DisconnCnt] = member.ActiveRecord.DisconnCnt,
            [GKey.Rating] = member.Rating,
            ["rating"] = member.Rating,
            [GKey.SLevel] = member.SLevel,
            ["slevel"] = member.SLevel,
            [GKey.NLevel] = member.NLevel,
            ["nlevel"] = member.NLevel,
            ["isProxy"] = member.IsOutPlayer,
            ["skillCnt"] = member.ActiveRecord.MatchCnt,
            ["skillCount"] = member.ActiveRecord.MatchCnt,
            [GKey.ReservedString] = "",
            [GKey.LastGDate] = member.LastGameDate,
            [GKey.LastDisconn] = "",
            [GKey.IPAddress] = member.IpAddress,
            [GKey.Port] = 0,
            [GKey.GamRanking] = 0,
            [GKey.ExScoreCnt] = 0,
            [GKey.DispRange] = member.DispRange,
            ["dispRange"] = member.DispRange,
            [Key.NickName] = member.NickName,
            [Key.TrickTitle] = member.TrickTitle,
            ["trickTitle"] = member.TrickTitle,
            [Key.MajakTitle] = member.MajakTitle,
            ["majakTitle"] = member.MajakTitle,
            [Key.RichiEffect] = member.GetRichiEffect(),
            ["richiEffect"] = member.GetRichiEffect(),
            [Key.CustomCostume] = costumeId,
            ["customCostume"] = costumeId,
            [Key.CustomCostumeType] = costumeType,
            ["customCostumeType"] = costumeType,
            [Key.CircleIdCnt] = room.RequiredCircles.Count,
            ["isHost"] = member.MemberNo == hostMemberNo,
            ["isViewer"] = resolvedPlayerType == GKey.ValueViewer,
        };

        int circleIndex = 0;
        foreach (var (circleId, circleName) in room.RequiredCircles)
        {
            payload[$"{Key.CircleId}{circleIndex}"] = circleId;
            payload[$"{Key.CircleName}{circleIndex}"] = circleName;
            circleIndex++;
        }

        return payload;
    }

    public static Dictionary<string, object?> BuildDeleteMemberPayload(
        string roomHost, MajakPlayer member, string playerType, int playerPos)
        => new()
        {
            [GKey.RoomHost] = roomHost,
            [GKey.PlayerType] = playerType,
            [GKey.PlayerPos] = playerPos,
            [GKey.Pix] = member.Pix,
            [GKey.Name] = member.NickName,
        };

    public static Dictionary<string, object?> BuildMemberListPayload(GameRoom room, string? excludeMemberNo = null)
    {
        string hostId = room.Seats.FirstOrDefault(p => p != null && !p.IsOutPlayer)?.Pix ?? "";
        string roomCreator = room.Seats.FirstOrDefault(p => p?.MemberNo == room.CreatorNo)?.Pix ?? "";
        string reserveMemberNo = room.Seats.FirstOrDefault(p => p?.MemberNo == room.BanishInfo.ReserveMemberNo)?.Pix
            ?? room.Viewers.FirstOrDefault(p => p.MemberNo == room.BanishInfo.ReserveMemberNo)?.Pix
            ?? "";
        var members = new List<object>();
        var payload = new Dictionary<string, object?>
        {
            [GKey.Count] = 0,
            ["count"] = 0,
            [GKey.RoomCreator] = roomCreator,
            ["roomCreator"] = roomCreator,
            [GKey.RoomHost] = hostId,
            ["roomHost"] = hostId,
            [GKey.PreBanishing] = room.BanishInfo.PreBanishing ? 1 : 0,
            [GKey.ReserveBanishing] = room.BanishInfo.ReserveBanishing ? 1 : 0,
            [GKey.Pix] = reserveMemberNo,
            ["members"] = members,
        };

        int seq = 0;
        for (int seat = 0; seat < room.Seats.Length; seat++)
        {
            var member = room.Seats[seat];
            if (member?.MemberNo == excludeMemberNo) continue;
            if (member == null || member.IsOutPlayer) continue;
            AddMember(room, payload, members, member, seq++, GKey.ValuePlayer, seat, hostId);
        }

        for (int viewerPos = 0; viewerPos < room.Viewers.Count; viewerPos++)
        {
            var viewer = room.Viewers[viewerPos];
            if (viewer.MemberNo == excludeMemberNo) continue;
            AddMember(room, payload, members, viewer, seq++, GKey.ValueViewer, viewerPos, hostId);
        }

        payload[GKey.Count] = seq;
        payload["count"] = seq;
        return payload;
    }

    private static void AddMember(
        GameRoom room,
        Dictionary<string, object?> payload,
        List<object> members,
        MajakPlayer member,
        int seq,
        string playerType,
        int playerPos,
        string hostId)
    {
        payload[$"{GKey.PlayerType}{seq}"] = playerType;
        payload[$"{GKey.PlayerPos}{seq}"] = playerPos;
        bool ready = playerType == GKey.ValuePlayer
            && playerPos >= 0
            && playerPos < room.OkButtonStates.Length
            && room.OkButtonStates[playerPos];
        payload[$"ready{seq}"] = ready;
        payload[$"isReady{seq}"] = ready;
        payload[$"okButton{seq}"] = ready ? 1 : 0;
        payload[$"{GKey.Pix}{seq}"] = member.Pix;
        payload[$"{GKey.AvatarId}{seq}"] = member.AvatarId;
        payload[$"{GKey.Name}{seq}"] = member.NickName;
        payload[$"{GKey.Sex}{seq}"] = member.Sex;
        payload[$"{GKey.Age}{seq}"] = 0;
        payload[$"{GKey.Location}{seq}"] = "";
        payload[$"{GKey.TotMoney}{seq}"] = member.GamMoney;
        payload[$"{GKey.MatchCnt}{seq}"] = member.ActiveRecord.MatchCnt;
        payload[$"{GKey.WinCnt}{seq}"] = member.ActiveRecord.WinCnt;
        payload[$"{GKey.DefeatCnt}{seq}"] = member.ActiveRecord.DefeatCnt;
        payload[$"{GKey.DrawCnt}{seq}"] = member.ActiveRecord.DrawCnt;
        payload[$"{GKey.DisconnCnt}{seq}"] = member.ActiveRecord.DisconnCnt;
        payload[$"{GKey.Rating}{seq}"] = member.Rating;
        payload[$"{GKey.SLevel}{seq}"] = member.SLevel;
        payload[$"{GKey.NLevel}{seq}"] = member.NLevel;
        payload[$"isProxy{seq}"] = member.IsOutPlayer;
        payload[$"skillCnt{seq}"] = member.ActiveRecord.MatchCnt;
        payload[$"skillCount{seq}"] = member.ActiveRecord.MatchCnt;
        payload[$"{GKey.ReservedString}{seq}"] = "";
        payload[$"{GKey.GamMoney}{seq}"] = member.GamMoney;
        payload[$"{GKey.LastGDate}{seq}"] = member.LastGameDate;
        payload[$"{GKey.LastDisconn}{seq}"] = "";
        payload[$"{GKey.IPAddress}{seq}"] = member.IpAddress;
        payload[$"{GKey.Port}{seq}"] = 0;
        payload[$"{GKey.GamRanking}{seq}"] = 0;
        payload[$"{GKey.ExScoreCnt}{seq}"] = 0;
        payload[$"{GKey.DispRange}{seq}"] = member.DispRange;
        payload[$"{Key.NickName}{seq}"] = member.NickName;
        payload[$"{Key.TrickTitle}{seq}"] = member.TrickTitle;
        payload[$"{Key.MajakTitle}{seq}"] = member.MajakTitle;
        payload[$"{Key.RichiEffect}{seq}"] = member.GetRichiEffect();

        int costumeId = member.GetCustomEquip(30);
        int costumeType = member.CustomItems.TryGetValue(costumeId, out var customItem) ? customItem.Kind : 0;
        payload[$"{Key.CustomCostume}{seq}"] = costumeId;
        payload[$"{Key.CustomCostumeType}{seq}"] = costumeType;

        members.Add(new
        {
            memberNo = member.Pix,
            pix = member.Pix,
            nickName = member.NickName,
            name = member.NickName,
            avatarId = member.AvatarId,
            sex = member.Sex,
            playerType,
            playerPos,
            seatPos = playerPos,
            ready,
            isReady = ready,
            okButton = ready ? 1 : 0,
            rating = member.Rating,
            nlevel = member.NLevel,
            slevel = member.SLevel,
            isProxy = member.IsOutPlayer,
            skillCnt = member.ActiveRecord.MatchCnt,
            skillCount = member.ActiveRecord.MatchCnt,
            trickTitle = member.TrickTitle,
            majakTitle = member.MajakTitle,
            isHost = member.MemberNo == hostId,
            isViewer = playerType == GKey.ValueViewer,
        });
    }
}

/// <summary>



/// </summary>
public class RoomEnterRoomCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly GameLogicService     _gameLogic;
    private readonly PlayerRepository?    _playerRepo;
    private readonly RoomRegistryService? _roomRegistry;

    public RoomEnterRoomCommand(PlayerSessionService session, GameLogicService gameLogic)
    {
        _session   = session;
        _gameLogic = gameLogic;
    }

    public RoomEnterRoomCommand(
        PlayerSessionService session,
        GameLogicService gameLogic,
        PlayerRepository playerRepo,
        RoomRegistryService roomRegistry)
        : this(session, gameLogic)
    {
        _playerRepo    = playerRepo;
        _roomRegistry  = roomRegistry;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        int requestRoomId = ctx.GetInt(GKey.RoomId);
        if (requestRoomId == 0) requestRoomId = ctx.GetInt("roomId");
        string roomPassword = First(ctx.GetString(GKey.RoomPwd), ctx.GetString("roomPwd"), ctx.GetString("roomPassword"));
        string payloadPlayerId = First(ctx.GetString(GKey.Pix), ctx.GetString("pix"), ctx.GetString("memberNo"));
        string payloadMemberNo = _session.ResolveMemberNo(payloadPlayerId);

        if (!string.IsNullOrEmpty(payloadMemberNo) && payloadMemberNo != player.MemberNo)
        {
            await SendRoomConnectError(ctx, requestRoomId, "", LegacyErrorCode.NotMatchSocketId);
            return;
        }

        if (requestRoomId == 0)
        {
            await SendRoomConnectError(ctx, 0, "", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }

        if (_roomRegistry is not null)
        {
            var continueRoom = await _roomRegistry.GetContinueRoomAsync(player.MemberNo);
            if (continueRoom != null && continueRoom.RoomId != requestRoomId)
            {
                await SendRoomConnectError(ctx, continueRoom.RoomId, "対局中のルームへ復帰してください。", LegacyErrorCode.NotEmptyRoom);
                return;
            }
        }

        if (player.RoomId is int currentRoomId && currentRoomId != requestRoomId)
        {
            await SendRoomConnectError(ctx, requestRoomId, "", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }

        var room = _session.GetRoom(requestRoomId);
        if (room == null)
        {
            await SendRoomConnectError(ctx, requestRoomId, "", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }

        if (string.IsNullOrEmpty(player.ChannelId))
            _session.SetPlayerChannel(player, room.ChannelId);

        if (room.ChannelId != player.ChannelId || room.State is not (GameRoomState.Waiting or GameRoomState.Playing))
        {
            await SendRoomConnectError(ctx, requestRoomId, "", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }

        bool alreadyInRoom = player.RoomId == requestRoomId;
        bool hasExistingSeat = room.Seats.Any(s => s?.MemberNo == player.MemberNo);
        bool hasExistingViewer = room.Viewers.Any(v => v.MemberNo == player.MemberNo);
        bool isPlayingSeatReconnect = room.State == GameRoomState.Playing && hasExistingSeat;
        bool isContinuePlayer = room.State == GameRoomState.Playing
            && room.Seats.Any(s => s?.MemberNo == player.MemberNo && s.IsOutPlayer);
        bool shouldAnnounceRejoin = isContinuePlayer;

        if (!alreadyInRoom && !isPlayingSeatReconnect && !isContinuePlayer
            && !string.IsNullOrEmpty(room.Password) && room.Password != roomPassword)
        {
            await SendRoomConnectError(ctx, requestRoomId, "パスワードが違います", LegacyErrorCode.InvalidPassword);
            return;
        }

        if (!alreadyInRoom && !isPlayingSeatReconnect && (hasExistingSeat || hasExistingViewer))
        {
            await SendRoomConnectError(ctx, requestRoomId, "既に入室しています", LegacyErrorCode.SameUserAlreadyIn);
            return;
        }

        if (!alreadyInRoom && room is { RequiredCircles.Count: > 0 } && _playerRepo is not null)
        {
            var keepCircleInfo = player.CircleInfo;
            player.CircleInfo = await _playerRepo.GetCircleInfoAsync(player.MemberNo);

            bool hasCircle = player.CircleInfo.Keys.Any(cid => room.RequiredCircles.ContainsKey(cid));
            player.CircleInfo = keepCircleInfo;

            if (!hasCircle)
            {
                string circleNames = string.Join("\n・", room.RequiredCircles.Values);
                await SendRoomConnectError(ctx, requestRoomId, $"この部屋に入室できるのは以下のサークル参加者のみです。\n・{circleNames}", LegacyErrorCode.MajNotEntryCircle);
                return;
            }
        }

        bool joined = alreadyInRoom || isPlayingSeatReconnect
            ? room.RefreshPlayerConnection(player)
            : isContinuePlayer
                ? _session.ReconnectToRoom(requestRoomId, player) >= 0
                : _session.JoinRoom(requestRoomId, player);
        if (!joined)
        {
            await SendRoomConnectError(ctx, requestRoomId, "", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }
        if (isContinuePlayer && _roomRegistry is not null)
            await _roomRegistry.ClearContinueRoomAsync(player.MemberNo);

        await ctx.Groups.AddToGroupAsync(ctx.ConnectionId, $"room_{requestRoomId}");

        var updatedRoom = _session.GetRoom(requestRoomId);
        if (updatedRoom == null) return;

        var memberListPayload = RoomGetMembersCommand.BuildMemberListPayload(updatedRoom);
        if (Convert.ToInt32(memberListPayload[GKey.Count]) > 0)
            await ctx.Caller.SendAsync(Cmd.MemberList, memberListPayload);

        if (!alreadyInRoom || shouldAnnounceRejoin)
        {
            await ctx.Clients.Group($"room_{requestRoomId}")
                .SendAsync(Cmd.AddMember, RoomGetMembersCommand.BuildAddMemberPayload(
                    updatedRoom, player, GKey.ValuePlayer));
        }

        if (_roomRegistry is not null)
            await _roomRegistry.UpdateMemberCountAsync(requestRoomId, player.ChannelId, updatedRoom.PlayerCount);

        var roomStatePacket = RoomStatePayload.Build(updatedRoom, "joined");
        roomStatePacket["memberNo"] = player.Pix;
        roomStatePacket["pix"] = player.Pix;
        roomStatePacket["nickname"] = player.NickName;
        roomStatePacket["avatarId"] = player.AvatarId;
        roomStatePacket["seatPos"] = player.SeatPos;
        await ctx.Clients.Group($"chanel_{player.ChannelId}")
            .SendAsync(Cmd.RoomState, roomStatePacket);

        await ctx.Clients.Group($"chanel_{player.ChannelId}")
            .SendAsync(Cmd.DeleteMember, new
            {
                memberNo = player.Pix,
                pix      = player.Pix,
                k3e      = player.Pix,
            });

        room = updatedRoom;



        await ctx.Caller.SendAsync(Cmd.EnterRoomCmd, new
        {
            result     = 1,
            k1e        = GKey.ValueSuccess,
            roomId     = room.RoomId,
            k42e       = room.RoomId,
            memberNo   = player.Pix,
            pix        = player.Pix,
            k3e        = player.Pix,
            name       = player.NickName,
            k8e        = player.NickName,
            avatarId   = player.AvatarId,
            k7e        = player.AvatarId,
            rating     = player.Rating,
            k31e       = player.Rating,
            slevel     = player.SLevel,
            k32e       = player.SLevel,
            nlevel     = player.NLevel,
            k33e       = player.NLevel,
            trickTitle = player.TrickTitle,
            mjkk46e    = player.TrickTitle,
            majakTitle = player.MajakTitle,
            mjkk47e    = player.MajakTitle,
            playerType = GKey.ValuePlayer,
            k57e       = GKey.ValuePlayer,
            playerPos  = player.SeatPos,
            k58e       = player.SeatPos,
            seatPos    = player.SeatPos,
            isHost     = room.Seats[0]?.MemberNo == player.MemberNo,
            roomTitle  = room.RoomTitle,
            k45e       = room.RoomTitle,
            roomOption = room.RoomOption,
            k46e       = room.RoomOption,
            state      = (int)room.State,
            canUseChanceItem = Commands.Game.ReserveChanceCommand.HasUsableChanceItem(player),
            reserveChance = player.ReserveChanceItem,
            k118e       = player.ReserveChanceItem,
        });

        if (room.State == GameRoomState.Waiting)
        {

            var okPayload = new Dictionary<string, object>();
            for (int i = 0; i < GameConst.PlayerMaxCount; i++)
                okPayload[$"{Key.OkButton}{i}"] = room.OkButtonStates[i] ? 1 : 0;
            await ctx.Clients.Group($"room_{room.RoomId}").SendAsync(Cmd.SendOkButton, okPayload);
        }
        else
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
        }
    }

    private static string First(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? "";

    private static Task SendRoomConnectError(CommandContext ctx, int roomId, string message, int failCode)
        => ctx.Caller.SendAsync(Cmd.ConnectTypeError,
            RoomConnectErrorPayload.Build(roomId, message, failCode));
}

/// <summary>


/// </summary>
public class RoomExitRoomCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly RoomRegistryService  _roomRegistry;
    private readonly GameLogicService     _gameLogic;

    public RoomExitRoomCommand(
        PlayerSessionService session,
        RoomRegistryService roomRegistry,
        GameLogicService gameLogic)
    {
        _session      = session;
        _roomRegistry = roomRegistry;
        _gameLogic    = gameLogic;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.RoomId == null) return;

        int roomId = player.RoomId.Value;
        string channelId = player.ChannelId;
        var room = _session.GetRoom(roomId);
        if (room == null)
        {
            player.RoomId = null;
            await ctx.Groups.RemoveFromGroupAsync(ctx.ConnectionId, $"room_{roomId}");
            return;
        }

        await HandleRoomExitAsync(ctx, player, room);

        var roomState = _session.GetRoom(roomId) is { } afterRoom
            ? RoomStatePayload.Build(afterRoom, "left")
            : RoomStatePayload.BuildEmpty(roomId, "left");
        if (_session.GetRoom(roomId) == null)
            _session.ExpirePendingMatch(roomId);
        roomState["memberNo"] = player.Pix;
        roomState["pix"] = player.Pix;

        await ctx.Clients.Group($"chanel_{channelId}")
            .SendAsync(Cmd.RoomState, roomState);
    }

    private async Task HandleRoomExitAsync(CommandContext ctx, MajakPlayer player, GameRoom room)
    {
        int roomId = room.RoomId;
        string channelId = player.ChannelId;
        int seatPos = (int)player.SeatPos;
        bool isViewer = player.IsViewer || room.Viewers.Any(v => v.MemberNo == player.MemberNo);

        if (room.State == GameRoomState.Playing && !isViewer)
        {
            player.IsOutPlayer = true;
            if (_roomRegistry is not null)
                await _roomRegistry.SetContinueRoomAsync(player.MemberNo, room);
            room.LimitCnt = room.Seats.Count(s => s != null && !s.IsOutPlayer);

            if (await room.EngineLock.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    if (seatPos >= 0 && seatPos < GameConst.PlayerMaxCount)
                    {
                        int engineOrder = room.SeatToEngineOrder[seatPos];
                        if (engineOrder >= 0 && engineOrder < GameConst.PlayerMaxCount)
                            await _gameLogic.ProxyPlayAsync(room, ctx, engineOrder);
                    }
                }
                finally
                {
                    room.EngineLock.Release();
                }
            }

            string newHost = room.Seats
                .Where(s => s != null && !s.IsOutPlayer)
                .Select(s => s!.MemberNo)
                .FirstOrDefault() ?? "";

            await ctx.Clients.Group($"room_{roomId}")
                .SendAsync(Cmd.DeleteMember, RoomGetMembersCommand.BuildDeleteMemberPayload(
                    newHost, player, GKey.ValuePlayer, seatPos));

            await ctx.Groups.RemoveFromGroupAsync(ctx.ConnectionId, $"room_{roomId}");
            player.RoomId = null;

            var updatedRoom = _session.GetRoom(roomId);
            if (updatedRoom != null && _roomRegistry != null)
            {
                await _roomRegistry.UpdateMemberCountAsync(roomId, channelId, updatedRoom.ActivePlayerCount);
            }
            return;
        }

        string roomHost = room.Seats
            .Where(s => s != null && s.MemberNo != player.MemberNo)
            .Select(s => s!.MemberNo)
            .FirstOrDefault() ?? "";
        string playerType = player.IsViewer ? GKey.ValueViewer : GKey.ValuePlayer;

        _session.RemovePendingMatchMember(roomId, player.MemberNo);
        _session.LeaveRoom(player);

        await ctx.Clients.Group($"room_{roomId}")
            .SendAsync(Cmd.DeleteMember, RoomGetMembersCommand.BuildDeleteMemberPayload(
                roomHost, player, playerType, seatPos));

        if (room.State == GameRoomState.Waiting && seatPos >= 0 && seatPos < GameConst.PlayerMaxCount)
        {
            room.OkButtonStates[seatPos] = false;
            var okPayload = new Dictionary<string, object>();
            for (int i = 0; i < GameConst.PlayerMaxCount; i++)
                okPayload[$"{Key.OkButton}{i}"] = room.OkButtonStates[i] ? 1 : 0;
            await ctx.Clients.Group($"room_{roomId}").SendAsync(Cmd.SendOkButton, okPayload);
        }

        await ctx.Groups.RemoveFromGroupAsync(ctx.ConnectionId, $"room_{roomId}");

        var afterRoom = _session.GetRoom(roomId);
        if (afterRoom == null)
            await _roomRegistry.RemoveRoomAsync(roomId, player.ChannelId);
        else
            await _roomRegistry.UpdateMemberCountAsync(roomId, player.ChannelId, afterRoom.ActivePlayerCount);
    }
}

/// <summary>



/// </summary>
public class RoomAlterRoomCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public RoomAlterRoomCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.RoomId == null) return;

        var room = _session.GetRoom(player.RoomId.Value);
        if (room == null) return;


        var host = room.Seats.FirstOrDefault(s => s != null);
        if (host?.MemberNo != player.MemberNo) return;


        int limitCnt = ctx.GetInt(GKey.RoomLimitCnt, ctx.GetInt("roomLimitCnt"));
        if (limitCnt > 0 && limitCnt <= GameConst.PlayerMaxCount)
        {
            room.LimitCnt = Math.Max(room.PlayerCount, limitCnt);
        }


        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.AlterRoom, new
            {
                result    = 1,
                limitCnt  = room.LimitCnt,
                roomId    = room.RoomId,
            });
    }
}

/// <summary>



/// </summary>
public class RoomEmoticonCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public RoomEmoticonCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.RoomId == null) return;

        var room = _session.GetRoom(player.RoomId.Value);
        if (room == null) return;

        int emoticonId = ctx.GetInt(Key.EmoticonId);


        var recipients = room.Seats
            .Where(seat => seat != null && !seat.IsOutPlayer)
            .Concat(room.Viewers)
            .Select(target => target!.ConnectionId)
            .Append(player.ConnectionId)
            .Where(connectionId => !string.IsNullOrWhiteSpace(connectionId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (recipients.Count == 0) return;

        await ctx.Clients.Clients(recipients).SendAsync(Cmd.UseEmoticon, new Dictionary<string, object>
        {
            [GKey.Pix]        = player.Pix,
            [Key.EmoticonId]       = emoticonId,
            [Key.EmoticonAvatarId] = player.AvatarId,
        });
    }
}

/// <summary>



/// </summary>
public class EventInfoCommand : ICommand
{
    private readonly PlayerRepository _playerRepo;

    public EventInfoCommand(PlayerRepository playerRepo)
        => _playerRepo = playerRepo;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;



        var eventInfoList = await _playerRepo.GetMemberEventInfoAsync(player.MemberNo);

        await ctx.Caller.SendAsync(Cmd.EventInfo, new
        {
            result   = 1,
            eventCnt = eventInfoList.Count,
            events   = eventInfoList,
        });
    }
}

/// <summary>


/// </summary>
public class PaiInfoListCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public PaiInfoListCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {


        await Task.CompletedTask;
    }
}

/// <summary>



/// </summary>
public class TsumikomiCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly bool                 _testEnv;

    public TsumikomiCommand(PlayerSessionService session, IConfiguration config)
    {
        _session = session;
        _testEnv = config.GetValue<bool>("GameSettings:TestEnvironment", false);
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.RoomId == null) return;


        if (!_testEnv) return;

        var room = _session.GetRoom(player.RoomId.Value);
        if (room == null) return;

        var paiCodes = ctx.GetIntArray("pai")
            ?? ctx.GetIntArray("tiles")
            ?? ctx.GetIntArray("bipai")
            ?? ctx.GetIntArray("kyokuInfo");
        if (paiCodes == null || paiCodes.Length != 136) return;

        room.Engine.SetBipai(paiCodes.Select(code => new PaiCode(code)).ToArray(), 0);

        var hostPix = room.Seats.FirstOrDefault(seat => seat != null && seat.MemberNo == room.CreatorNo)?.Pix
            ?? room.Seats.FirstOrDefault(seat => seat != null)?.Pix
            ?? player.Pix;

        var packet = new Dictionary<string, object?>
        {
            [GKey.PlayerType] = GKey.ValuePlayer,
            [GKey.Pix] = hostPix,
            [GKey.Target] = GKey.ValueAll,
            [GKey.Color] = 0,
            [GKey.String] = "tsumi",
            ["playerType"] = GKey.ValuePlayer,
            ["pix"] = hostPix,
            ["target"] = "all",
            ["color"] = 0,
            ["string"] = "tsumi",
        };

        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.HanChatRelay, packet);
    }
}

/// <summary>


/// </summary>
public class IpAdapterInfoCommand : ICommand
{
    public Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return Task.CompletedTask;

        string gateway = First(ctx.GetString(Key.Gateway), ctx.GetString("gateway"));
        if (gateway.Length is > 0 and <= 15)
            player.Gateway = gateway;

        string macAddr = First(ctx.GetString(Key.MacAddr), ctx.GetString("macAddr"), ctx.GetString("mac"));
        if (macAddr.Length is > 0 and <= 17)
            player.MacAddr = macAddr;

        return Task.CompletedTask;
    }

    private static string First(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
}

/// <summary>


/// </summary>
public class RoomStateCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public RoomStateCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {


        await Task.CompletedTask;
    }
}
