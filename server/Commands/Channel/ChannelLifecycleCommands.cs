using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Protocol;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using Microsoft.Extensions.Options;

namespace MajakServer.Commands.Channel;

/// <summary>
/// room:get_list ルームリスト取征E


/// </summary>
public class GetRoomListCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly MasterCacheService? _masterCache;
    private readonly ChannelServerSettings? _channelSettings;
    private readonly RoomRegistryService? _roomRegistry;

    public GetRoomListCommand(PlayerSessionService session, ChannelRepository? channelRepository = null, MasterCacheService? masterCache = null, IOptions<ChannelServerSettings>? channelSettings = null, RoomRegistryService? roomRegistry = null)
    {
        _session = session;
        _masterCache = masterCache;
        _channelSettings = channelSettings?.Value;
        _roomRegistry = roomRegistry;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        var localRooms = _session.GetChannelRooms(player.ChannelId)
            .Where(r => !_session.HasPendingMatch(r.RoomId))
            .Where(r => !r.HasNoActiveMembers || r.State == GameRoomState.Playing)
            .OrderBy(r => r.RoomId)
            .ToList();

        var localByRoomId = localRooms.ToDictionary(room => room.RoomId);
        var rooms = new List<Dictionary<string, object?>>();
        if (_roomRegistry is not null)
        {
            var registryRooms = await _roomRegistry.GetChannelRoomsAsync(player.ChannelId);
            var seenRoomIds = new HashSet<int>();
            foreach (var registryRoom in registryRooms.OrderBy(room => room.RoomId))
            {
                seenRoomIds.Add(registryRoom.RoomId);
                if (localByRoomId.TryGetValue(registryRoom.RoomId, out var localRoom))
                {
                    rooms.Add(BuildRoomListEntry(localRoom, registryRoom.ServerUrl));
                    continue;
                }
                rooms.Add(BuildRegistryRoomListEntry(registryRoom));
            }

            foreach (var localRoom in localRooms.Where(room => !seenRoomIds.Contains(room.RoomId)))
                rooms.Add(BuildRoomListEntry(localRoom, _channelSettings?.ResolveUrl(player.ChannelId) ?? _channelSettings?.ServerUrl ?? ""));
        }
        else
        {
            rooms = localRooms
                .Select(room => BuildRoomListEntry(room, _channelSettings?.ResolveUrl(player.ChannelId) ?? _channelSettings?.ServerUrl ?? ""))
                .ToList();
        }

        int roomSlotCount = _session.GetKnownChannelRoomSlotCount(player.ChannelId);
        if (_masterCache is not null)
        {
            var channels = await _masterCache.GetChannelListAsync();
            var channel = channels.FirstOrDefault(c => c.ChanelId == player.ChannelId || c.SubId == player.ChannelId);
            if (channel is not null)
                roomSlotCount = Math.Max(channel.MaxRoom, roomSlotCount);
        }

        var packet = new Dictionary<string, object?>
        {
            ["result"] = 1,
            [GKey.Result] = GKey.ValueSuccess,
            ["count"] = rooms.Count,
            [GKey.RoomCount] = Math.Max(roomSlotCount, rooms.Count),
            ["rooms"] = rooms,
        };

        foreach (var room in localRooms)
            packet[$"{GKey.RoomId}{room.RoomId}"] = BuildLegacyRoomInfo(room);

        await ctx.Caller.SendAsync(Cmd.GetRoomList, packet);
    }

    private static Dictionary<string, object?> BuildRegistryRoomListEntry(RoomRedisEntry room)
    {
        var entry = new Dictionary<string, object?>
        {
            ["roomId"] = room.RoomId,
            ["title"] = room.Title,
            ["isPrivate"] = room.IsPrivate,
            ["memberCnt"] = room.MemberCnt,
            [GKey.MemberCnt] = room.MemberCnt,
            ["opMemberCnt"] = 0,
            [GKey.OpMemberCnt] = 0,
            ["memberMax"] = room.MemberMax,
            ["playerCnt"] = room.MemberCnt,
            ["viewerCnt"] = 0,
            [GKey.ViewerCnt] = 0,
            ["roomOption"] = room.RoomOption,
            ["serverUrl"] = room.ServerUrl,
            ["maxViewer"] = room.MaxViewer,
        };
        if (room.State > 0) entry["state"] = room.State;
        if (room.RoomPlaying > 0)
        {
            entry["roomPlaying"] = room.RoomPlaying;
            entry[GKey.RoomPlaying] = room.RoomPlaying;
        }
        return entry;
    }

    public static string BuildLegacyRoomInfo(GameRoom room)
    {
        var activeSeats = room.Seats
            .Select((member, seat) => new { member, seat })
            .Where(x => x.member != null && !x.member.IsOutPlayer)
            .ToList();
        var continueSeats = room.Seats
            .Select((member, seat) => new { member, seat })
            .Where(x => x.member?.IsOutPlayer == true)
            .ToList();
        string roomCreator = activeSeats.FirstOrDefault(x => x.member?.MemberNo == room.CreatorNo)?.member?.Pix
            ?? continueSeats.FirstOrDefault(x => x.member?.MemberNo == room.CreatorNo)?.member?.Pix
            ?? "";

        var values = new Dictionary<string, object?>
        {
            [GKey.RoomId] = room.RoomId,
            [GKey.RoomTitle] = room.RoomTitle,
            [GKey.PrivateYn] = room.IsPrivate ? "Y" : "N",
            [GKey.RoomOption] = room.RoomOption,
            [GKey.RoomStateKey] = RoomStatePayload.GetLegacyRoomState(room),
            [GKey.RoomCreator] = roomCreator,
            [GKey.RoomHost] = activeSeats.FirstOrDefault()?.member?.Pix ?? "",
            [GKey.RoomPlaying] = GetLegacyPlayState(room),
            [GKey.MemberCnt] = activeSeats.Count,
            [GKey.ViewerCnt] = room.ViewerCount,
            [GKey.OpMemberCnt] = continueSeats.Count,
            [GKey.RoomLimitCnt] = room.LimitCnt,
            [GKey.MaxViewer] = room.MaxViewer,
        };

        for (int i = 0; i < activeSeats.Count; i++)
        {
            values[$"{GKey.Pix}{i}"] = activeSeats[i].member!.Pix;
            values[$"{GKey.MemberPos}{i}"] = activeSeats[i].seat;
        }

        for (int i = 0; i < continueSeats.Count; i++)
        {
            values[$"{GKey.OpPix}{i}"] = continueSeats[i].member!.Pix;
            values[$"{GKey.OpMemberPos}{i}"] = continueSeats[i].seat;
        }

        for (int i = 0; i < room.Viewers.Count; i++)
        {
            values[$"{GKey.ViewerId}{i}"] = room.Viewers[i].Pix;
            values[$"{GKey.ViewerPos}{i}"] = i;
        }

        return string.Join('&', values.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}"));
    }

    public static Dictionary<string, object?> BuildRoomListEntry(GameRoom room, string serverUrl = "")
    {
        int activeMemberCnt = room.Seats.Count(s => s != null && !s.IsOutPlayer);
        int continueMemberCnt = room.Seats.Count(s => s?.IsOutPlayer == true);
        string roomCreator = room.Seats.FirstOrDefault(seat => seat?.MemberNo == room.CreatorNo)?.Pix ?? "";
        int roomPlaying = room.State == GameRoomState.Playing && continueMemberCnt > 0
            ? 3
            : GetLegacyPlayState(room);

        return new()
        {
            ["roomId"] = room.RoomId,
            ["title"] = room.RoomTitle,
            ["isPrivate"] = room.IsPrivate,
            ["roomOption"] = room.RoomOption,
            ["serverUrl"] = string.IsNullOrWhiteSpace(room.ServerUrl) ? serverUrl : room.ServerUrl,
            ["maxViewer"] = room.MaxViewer,
            ["memberCnt"] = activeMemberCnt,
            [GKey.MemberCnt] = activeMemberCnt,
            ["opMemberCnt"] = continueMemberCnt,
            [GKey.OpMemberCnt] = continueMemberCnt,
            ["memberMax"] = room.LimitCnt,
            ["playerCnt"] = room.PlayerCount,
            ["state"] = RoomStatePayload.GetLegacyRoomState(room),
            ["roomCreator"] = roomCreator,
            [GKey.RoomCreator] = roomCreator,
            ["roomPlaying"] = roomPlaying,
            [GKey.RoomPlaying] = roomPlaying,
            ["moneyRate"] = room.MoneyRate,
            ["minMoney"] = room.MinMoney,
            ["maxMoney"] = room.MaxMoney,
            ["seats"] = room.Seats
                .Select((member, seat) => new { member, seat })
                .Where(x => x.member != null)
                .Select(x => new
                {
                    memberNo = x.member!.Pix,
                    pix = x.member!.Pix,
                    pos = x.seat,
                    avatarId = x.member.AvatarId,
                    sex = x.member.Sex,
                    disconnected = x.member.IsOutPlayer,
                })
                .ToList(),
        };
    }

    private static int GetLegacyPlayState(GameRoom room)
        => room.State == GameRoomState.Playing ? 1 : 0;
}

/// <summary>



/// AddToParser_GetMemberListResponse 相当を含む
/// </summary>
public class GetMemberListCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public GetMemberListCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        bool isAutoMatchingChannel = IsAutoMatchingChannel(player.ChannelId);
        var members = _session.GetAllChannelPlayers(player.ChannelId)
            .Select((p, index) => new
            {
                memberNo   = p.Pix,
                pix        = p.Pix,
                k3e        = p.Pix,
                nickname   = p.NickName,
                k8e        = p.NickName,
                avatarId   = p.AvatarId,
                k7e        = p.AvatarId,
                sex        = p.Sex,
                k11e       = p.Sex,
                rating     = p.Rating,
                k31e       = p.Rating,
                nlevel     = p.NLevel,
                k33e       = p.NLevel,
                slevel     = p.SLevel,
                k32e       = p.SLevel,
                gammoney   = p.GamMoney,
                k34e       = p.GamMoney,
                matchCnt   = p.ActiveRecord.MatchCnt,
                k26e       = p.ActiveRecord.MatchCnt,
                winCnt     = p.ActiveRecord.WinCnt,
                k27e       = p.ActiveRecord.WinCnt,
                defeatCnt  = p.ActiveRecord.DefeatCnt,
                k28e       = p.ActiveRecord.DefeatCnt,
                drawCnt    = p.ActiveRecord.DrawCnt,
                k29e       = p.ActiveRecord.DrawCnt,
                disconnCnt = p.ActiveRecord.DisconnCnt,
                k30e       = p.ActiveRecord.DisconnCnt,
                trickTitle = p.TrickTitle,
                majakTitle = p.MajakTitle,
                dispRange  = p.DispRange,
                k448e      = p.DispRange,
                roomId     = p.RoomId ?? 0,
                k42e       = p.RoomId ?? 0,
                location   = FormatMemberLocation(p.RoomId),
                k12e       = FormatMemberLocation(p.RoomId),
                legacyInfo = BuildLegacyMemberInfo(p, isAutoMatchingChannel),
                legacyKey  = $"{GKey.Pix}{index}",
            })
            .ToList();

        var response = new Dictionary<string, object>
        {
            ["result"]      = 1,
            [GKey.Result]   = GKey.ValueSuccess,
            ["count"]       = members.Count,
            [GKey.Count]    = members.Count,
            ["members"]     = members,
        };

        foreach (var member in members)
        {
            response[member.legacyKey] = member.legacyInfo;
        }

        await ctx.Caller.SendAsync(Cmd.GetMemberList, response);
    }

    private static bool IsAutoMatchingChannel(string channelId)
    {
        string subId = channelId.Length >= 11 ? channelId.Substring(6, 5) : channelId;
        return subId.Length > 1 && subId[1] == 'Z';
    }

    private static string BuildLegacyMemberInfo(MajakPlayer player, bool isAutoMatchingChannel)
    {
        var exScores = new[] { 0, 0, 0, 0, 0 };
        string location = FormatMemberLocation(player.RoomId);
        string nickname = string.IsNullOrEmpty(player.NickName) ? " " : player.NickName;

        if (isAutoMatchingChannel)
        {
            return string.Join('\t', new object[]
            {
                player.MemberNo,
                player.Sex,
                location,
                player.ActiveRecord.MatchCnt,
                player.ActiveRecord.WinCnt,
                player.ActiveRecord.DefeatCnt,
                player.ActiveRecord.DrawCnt,
                player.ActiveRecord.Rating,
                player.NLevel,
                player.GamMoney,
                player.DispRange,
                exScores.Length,
                exScores[0], exScores[1], exScores[2], exScores[3], exScores[4],
                nickname,
                player.GradeRecord.Grade,
            });
        }

        return string.Join('\t', new object[]
        {
            player.MemberNo,
            player.AvatarId,
            player.NickName,
            player.Sex,
            0,
            location,
            0,
            player.ActiveRecord.MatchCnt,
            player.ActiveRecord.WinCnt,
            player.ActiveRecord.DefeatCnt,
            player.ActiveRecord.DrawCnt,
            player.ActiveRecord.DisconnCnt,
            player.ActiveRecord.Rating,
            string.IsNullOrEmpty(player.SLevel) ? " " : player.SLevel,
            player.NLevel,
            player.Gateway,
            player.GamMoney,
            player.LastGameDate,
            "",
            0,
            player.DispRange,
            exScores.Length,
            exScores[0], exScores[1], exScores[2], exScores[3], exScores[4],
            nickname,
        });
    }

    private static string FormatMemberLocation(int? roomId)
        => roomId is > 0 ? $"{roomId.Value}番部屋" : "ロビー";
}

/// <summary>
/// channel:exit チャンネル退場


/// </summary>
public class ExitChannelCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly RoomRegistryService? _roomRegistry;
    private readonly LobbySessionLeaseService? _lobbySessions;

    public ExitChannelCommand(
        PlayerSessionService session,
        RoomRegistryService? roomRegistry = null,
        LobbySessionLeaseService? lobbySessions = null)
    {
        _session = session;
        _roomRegistry = roomRegistry;
        _lobbySessions = lobbySessions;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        string channelId = player.ChannelId;

        if (IsAutoMatchingChannel(channelId))
        {
            _session.DequeueMatching(channelId, player.MemberNo);
        }


        if (player.RoomId.HasValue)
        {
            int roomId = player.RoomId.Value;
            var room = _session.GetRoom(roomId);
            int playerPos = (int)player.SeatPos;
            string playerType = player.IsViewer ? GKey.ValueViewer : GKey.ValuePlayer;
            await ctx.Groups.RemoveFromGroupAsync(ctx.ConnectionId, $"room_{roomId}");
            _session.LeaveRoom(player);
            string roomHost = room?.Seats
                .Where(s => s != null && !s.IsOutPlayer)
                .Select(s => s!.MemberNo)
                .FirstOrDefault() ?? "";

            await ctx.Clients.Group($"room_{roomId}")
                .SendAsync(Cmd.DeleteMember, MajakServer.Commands.Room.RoomGetMembersCommand.BuildDeleteMemberPayload(
                    roomHost, player, playerType, playerPos));

            var afterRoom = _session.GetRoom(roomId);
            if (_roomRegistry != null)
            {
                if (afterRoom == null)
                {
                    _session.ExpirePendingMatch(roomId);
                    await _roomRegistry.RemoveRoomAsync(roomId, channelId);
                }
                else
                {
                    await _roomRegistry.UpdateMemberCountAsync(roomId, channelId, afterRoom.ActivePlayerCount);
                }
            }
        }

        // チャンネルから削除
        await ctx.Groups.RemoveFromGroupAsync(ctx.ConnectionId, $"chanel_{channelId}");
        _session.Remove(ctx.ConnectionId);
        if (_lobbySessions != null)
            await _lobbySessions.ReleaseAsync(ctx.ConnectionId);


        await ctx.Clients.Group($"chanel_{channelId}")
            .SendAsync(Cmd.DeleteMember, new
            {
                memberNo = player.Pix,
                pix      = player.Pix,
                k3e      = player.Pix,
            });

        await ctx.Caller.SendAsync(Cmd.ExitChannel, new
        {
            result    = 1,
            k1e       = GKey.ValueSuccess,
            gameId    = GameConst.ServiceId,
            k22e      = GameConst.ServiceId,
            subId     = ExtractSubId(channelId),
            k23e      = ExtractSubId(channelId),
            channelId,
            k24e      = channelId,
            memberNo  = player.Pix,
            pix       = player.Pix,
            k3e       = player.Pix,
        });
    }

    private static string ExtractSubId(string channelId)
        => channelId.Length >= 11 ? channelId[6..11] : channelId;

    private static bool IsAutoMatchingChannel(string channelId)
    {
        string subId = ExtractSubId(channelId);
        return subId.Length > 1 && subId[1] == 'Z';
    }
}

/// <summary>


/// </summary>
public class CreateRoomCommand : ICommand
{
    private readonly PlayerSessionService            _session;
    private readonly PlayerRepository                _playerRepo;
    private readonly RoomRegistryService             _roomRegistry;
    private readonly MasterCacheService              _masterCache;
    private readonly IOptions<ChannelServerSettings> _channelSettings;
    private readonly ILogger<CreateRoomCommand>      _log;

    public CreateRoomCommand(
        PlayerSessionService session,
        PlayerRepository playerRepo,
        RoomRegistryService roomRegistry,
        MasterCacheService masterCache,
        IOptions<ChannelServerSettings> channelSettings,
        ILogger<CreateRoomCommand> log)
    {
        _session         = session;
        _playerRepo      = playerRepo;
        _roomRegistry    = roomRegistry;
        _masterCache     = masterCache;
        _channelSettings = channelSettings;
        _log             = log;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        if (player.RoomId != null)
        {
            await SendRoomConnectError(ctx, player.RoomId.Value, "既にルームに入室しています。", LegacyErrorCode.NotEmptyRoom);
            return;
        }

        string channelId = First(ctx.GetString(GKey.ChannelId), ctx.GetString("channelId"), player.ChannelId);
        string subId = First(ctx.GetString(GKey.SubId), ctx.GetString("subId"), ExtractSubId(channelId));
        bool isCircleChannel = subId == "00000";

        var cupConfigs = await _masterCache.GetCupConfigsAsync();
        var thisCupRoom = cupConfigs.FirstOrDefault(c => c.ChannelId == channelId);

        string roomOption = First(ctx.GetString(GKey.RoomOption), ctx.GetString("roomOption"));
        if (string.IsNullOrEmpty(roomOption))
        {
            await SendRoomConnectError(ctx, 0, "ルーム設定が不正です。", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }

        int requestedSpeedNo = roomOption.Length > 2 && char.IsDigit(roomOption[2]) ? roomOption[2] - '0' : -1;
        _log.LogInformation("CreateRoom received. channelId={ChannelId} subId={SubId} memberNo={MemberNo} roomId={RoomId} roomOption={RoomOption} speedNo={SpeedNo}",
            channelId, subId, player.MemberNo, ctx.GetInt(GKey.RoomId, ctx.GetInt("roomId")), roomOption, requestedSpeedNo);

        string roomTitle = First(ctx.GetString(GKey.RoomTitle), ctx.GetString("roomTitle"));
        string roomPassword = First(ctx.GetString(GKey.RoomPwd), ctx.GetString("roomPwd"), ctx.GetString("roomPassword"));
        string roomType = ctx.GetString("roomType");
        long moneyRate = ctx.GetLong("moneyRate");
        if (moneyRate == 0) moneyRate = 500;
        long unitMoney = ctx.GetLong("unitMoney");
        long minMoney = ctx.GetLong("minMoney");
        long maxMoney = ctx.GetLong("maxMoney");
        int minCnt = ctx.GetInt(GKey.RoomMinCnt, ctx.GetInt("roomMinCnt"));
        int maxViewer = ctx.GetInt(GKey.MaxViewer, ctx.GetInt("maxViewer", 12));
        int requestRoomId = ctx.GetInt(GKey.RoomId, ctx.GetInt("roomId"));
        bool isPrivate = ctx.GetBool("isPrivate")
            || IsTruthy(ctx.GetString(GKey.PrivateYn))
            || IsTruthy(ctx.GetString("roomType"));

        if (requestRoomId <= 0)
        {
            await SendRoomConnectError(ctx, 0, "ルーム番号が不正です。", LegacyErrorCode.MajAutoEnterRoomFailed);
            return;
        }

        var continueRoom = await _roomRegistry.GetContinueRoomAsync(player.MemberNo);
        if (continueRoom != null)
        {
            await SendRoomConnectError(ctx, continueRoom.RoomId, "対局中のルームへ復帰してください。", LegacyErrorCode.NotEmptyRoom);
            return;
        }

        var existingRoom = _session.GetRoom(requestRoomId);
        if (existingRoom != null && existingRoom.HasNoActiveMembers && existingRoom.State != GameRoomState.Playing)
        {
            _session.RemoveRoom(requestRoomId);
            await _roomRegistry.RemoveRoomAsync(requestRoomId, existingRoom.ChannelId);
            existingRoom = null;
        }

        if (existingRoom != null)
        {
            await SendRoomConnectError(ctx, requestRoomId, "既に使用中のルームです。", LegacyErrorCode.NotEmptyRoom);
            return;
        }

        var registryRoom = (await _roomRegistry.GetChannelRoomsAsync(channelId))
            .FirstOrDefault(room => room.RoomId == requestRoomId);
        if (registryRoom != null)
        {
            await SendRoomConnectError(ctx, requestRoomId, "既に使用中のルームです。", LegacyErrorCode.NotEmptyRoom);
            return;
        }

        Dictionary<string, string>? requiredCircles = null;
        var circleIds = GetRequestedCircleIds(ctx);
        if (isCircleChannel && circleIds is { Length: > 0 })
        {
            var keepCircleInfo = player.CircleInfo;
            player.CircleInfo = await _playerRepo.GetCircleInfoAsync(player.MemberNo);
            requiredCircles = new Dictionary<string, string>();
            foreach (var cid in circleIds.Where(c => !string.IsNullOrEmpty(c)))
            {
                if (player.CircleInfo.TryGetValue(cid, out var circleName))
                {
                    requiredCircles[cid] = circleName;
                    continue;
                }

                player.CircleInfo = keepCircleInfo;
                await SendRoomConnectError(ctx, 0,
                    keepCircleInfo.TryGetValue(cid, out var knownName)
                        ? $"以下のサークルに参加していないため指定できません。\n・{knownName}"
                        : "参加していないサークルを指定しています。",
                    LegacyErrorCode.MajNotEntryCircle);
                return;
            }
            player.CircleInfo = keepCircleInfo;
        }

        var room = _session.CreateRoom(channelId, player,
            roomOption, moneyRate, minMoney, maxMoney, isPrivate,
            roomTitle: roomTitle,
            roomPassword: roomPassword,
            roomType: roomType,
            maxViewer: maxViewer,
            cupId:            thisCupRoom?.CupId            ?? 0,
            cupSeq:           thisCupRoom?.CupSeq           ?? 0,
            cupJudgementType: thisCupRoom?.JudgementType    ?? -1,
            cupPointSumType:  thisCupRoom?.CupPointSumType  ?? 0,
            cupMaxMatchCntLimit: thisCupRoom?.MaxMatchCntLimit ?? -1,
            cupConditionRegular: thisCupRoom?.ConditionRegular ?? 0,
            cupConditionBilling: thisCupRoom?.ConditionBilling ?? 0,
            cupEntryLimited:     thisCupRoom?.EntryLimited     ?? false,
            cupNormalYakuCondition: thisCupRoom?.NormalYakuCondition ?? "",
            cupYakumanCondition:    thisCupRoom?.YakumanCondition    ?? "",
            subId:            subId,
            unitMoney:        unitMoney > 0 ? unitMoney : moneyRate,
            minCnt:           minCnt,
            roomId:           requestRoomId);
        room.ServerUrl = _channelSettings.Value.ResolveUrl(channelId);

        if (requiredCircles is { Count: > 0 })
        {
            foreach (var (cid, cname) in requiredCircles)
                room.RequiredCircles[cid] = cname;
        }

        await ctx.Groups.AddToGroupAsync(ctx.ConnectionId, $"room_{room.RoomId}");

        await _roomRegistry.RegisterRoomAsync(
            room.RoomId, player.ChannelId, room.RoomTitle,
            isPrivate, room.PlayerCount, room.LimitCnt,
            _channelSettings.Value.ServerUrl, room.RoomOption, room.MaxViewer);

        var createdPacket = BuildRoomCreatedPacket(room, moneyRate, minMoney, maxMoney);

        await ctx.Clients.Group($"chanel_{player.ChannelId}")
            .SendAsync(Cmd.RoomState, createdPacket);

        await ctx.Clients.Group($"chanel_{player.ChannelId}")
            .SendAsync(Cmd.DeleteMember, new
            {
                memberNo = player.Pix,
                pix      = player.Pix,
                k3e      = player.Pix,
            });

        await ctx.Caller.SendAsync(Cmd.RoomCreated, createdPacket);
    }

    private static Dictionary<string, object?> BuildRoomCreatedPacket(GameRoom room, long moneyRate, long minMoney, long maxMoney)
    {
        var packet = RoomStatePayload.Build(room, "created");
        packet["result"] = 1;
        packet[GKey.Result] = GKey.ValueSuccess;
        packet["moneyRate"] = moneyRate;
        packet["minMoney"] = minMoney;
        packet["maxMoney"] = maxMoney;
        return packet;
    }

    private static string[]? GetRequestedCircleIds(CommandContext ctx)
    {
        var circleIds = ctx.Get<string[]?>("circleIds");
        if (circleIds is { Length: > 0 }) return circleIds;

        int count = ctx.GetInt(Key.CircleIdCnt);
        if (count <= 0) return null;

        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            string circleId = ctx.GetString($"{Key.CircleId}{i}");
            if (!string.IsNullOrEmpty(circleId)) result.Add(circleId);
        }

        return result.Count > 0 ? result.ToArray() : null;
    }

    private static Task SendRoomConnectError(CommandContext ctx, int roomId, string message, int failCode)
        => ctx.Caller.SendAsync(Cmd.ConnectTypeError,
            RoomConnectErrorPayload.Build(roomId, message, failCode));

    private static string First(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? "";

    private static string ExtractSubId(string channelId)
        => channelId.Length >= 11 ? channelId[6..11] : channelId;

    private static bool IsTruthy(string value)
        => value is "Y" or "y" or "1" or "true" or "private";
}

/// <summary>




/// </summary>
public class HanChatAllRelayCommand : ICommand
{
    private readonly PlayerSessionService? _session;
    private readonly RatingRankInfoCommand? _ratingRankInfoCommand;

    public HanChatAllRelayCommand()
    {
    }

    public HanChatAllRelayCommand(PlayerSessionService session, RatingRankInfoCommand? ratingRankInfoCommand = null)
    {
        _session = session;
        _ratingRankInfoCommand = ratingRankInfoCommand;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        string message = ctx.GetString(GKey.String);
        if (string.IsNullOrEmpty(message)) message = ctx.GetString("string");
        if (string.IsNullOrEmpty(message)) return;
        string target = NormalizeChatTarget(ctx.GetString(GKey.Target));
        if (target == GKey.ValueAll) target = NormalizeChatTarget(ctx.GetString("target"));

        if (player.IsAdminId && message == "ADMIN?")
        {
            message = "admin user";
        }
        else if (player.IsAdminId && TryGetDebugHaipaiCommand(message, out int debugYaku, out string debugMessage))
        {
            if (_session != null && player.RoomId is int roomId)
            {
                _session.GetRoom(roomId)?.Engine.SetDebugHaipaiYaku(debugYaku);
            }
            message = debugMessage;
        }
        else if (player.IsAdminId && TryGetLegacyRankForceCommand(message, out string rankForceMessage))
        {
            await ctx.Caller.SendAsync(Cmd.HanChatRelay, BuildChatPacket(player, rankForceMessage));
            return;
        }
        else if (player.IsAdminId && message.StartsWith("!RQRRI", StringComparison.Ordinal))
        {
            string errorMessage = "";
            if (_ratingRankInfoCommand != null && TryBuildLegacyRatingRankPayload(player, message, out var rankPayload, out errorMessage))
            {
                await _ratingRankInfoCommand.ExecuteAsync(new CommandContext
                {
                    ConnectionId = ctx.ConnectionId,
                    Player = ctx.Player,
                    Caller = ctx.Caller,
                    Clients = ctx.Clients,
                    Groups = ctx.Groups,
                    RemoteIpAddress = ctx.RemoteIpAddress,
                    AbortConnection = ctx.AbortConnection,
                    AbortConnectionWithReason = ctx.AbortConnectionWithReason,
                    Payload = rankPayload,
                });
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                await ctx.Caller.SendAsync(Cmd.HanChatRelay, BuildChatPacket(player, errorMessage));
            }
            return;
        }


        //   if(timNow - timLastChat < 500ms) ignore packet
        var now = DateTime.UtcNow;
        if ((now - player.LastChatTime).TotalMilliseconds < 500)
        {
            return;
        }
        player.LastChatTime = now;

        var packet = BuildChatPacket(player, message, target);

        if (_session != null && !IsAllChatTarget(target))
        {
            var targetPlayer = _session.GetByMember(target);
            var recipients = new[] { player.ConnectionId, targetPlayer?.ChannelId == player.ChannelId ? targetPlayer.ConnectionId : "" }
                .Where(connectionId => !string.IsNullOrWhiteSpace(connectionId))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (recipients.Count > 0) await ctx.Clients.Clients(recipients).SendAsync(Cmd.HanChatRelay, packet);
            return;
        }

        if (_session != null && player.RoomId is int senderRoomId && senderRoomId > 0)
        {
            int senderGroupNo = GetLegacyRoomGroupNo(senderRoomId);
            var recipients = _session.GetChannelRooms(player.ChannelId)
                .Where(room => GetLegacyRoomGroupNo(room.RoomId) == senderGroupNo)
                .SelectMany(room => room.Seats
                    .Where(seat => seat != null && !seat.IsOutPlayer)
                    .Concat(room.Viewers)
                    .Select(target => target!.ConnectionId))
                .Append(player.ConnectionId)
                .Where(connectionId => !string.IsNullOrWhiteSpace(connectionId))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (recipients.Count > 0) await ctx.Clients.Clients(recipients).SendAsync(Cmd.HanChatRelay, packet);
            return;
        }

        await ctx.Clients.Group($"chanel_{player.ChannelId}")
            .SendAsync(Cmd.HanChatRelay, packet);
    }

    private static int GetLegacyRoomGroupNo(int roomId)
        => ((roomId - 1) / 10) + 1;

    private static bool IsAllChatTarget(string target)
        => string.IsNullOrEmpty(target) || target == GKey.ValueAll || target.Equals("all", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeChatTarget(string target)
        => string.IsNullOrWhiteSpace(target) || target.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? GKey.ValueAll
            : target.Trim();

    private static Dictionary<string, object?> BuildChatPacket(MajakPlayer player, string message, string? target = null)
    {
        string packetTarget = NormalizeChatTarget(target ?? GKey.ValueAll);
        return new()
        {
            [GKey.Pix] = player.Pix,
            [GKey.PlayerType] = GKey.ValuePlayer,
            [GKey.Target] = packetTarget,
            [GKey.Color] = 0,
            [GKey.String] = message,
            ["memberNo"] = player.Pix,
            ["pix"] = player.Pix,
            ["playerType"] = GKey.ValuePlayer,
            ["target"] = packetTarget,
            ["color"] = 0,
            ["string"] = message,
        };
    }

    private static bool TryGetLegacyRankForceCommand(string message, out string response)
    {
        response = message switch
        {
            _ when message.StartsWith("!RANKFORCED", StringComparison.Ordinal) => "grade rank daily force enabled.",
            _ when message.StartsWith("!RANKFORCEM", StringComparison.Ordinal) => "grade rank monthly force enabled.",
            _ when message.StartsWith("!!RANKFORCED", StringComparison.Ordinal) => "grade rank daily force disabled.",
            _ when message.StartsWith("!!RANKFORCEM", StringComparison.Ordinal) => "grade rank monthly force disabled.",
            _ => "",
        };

        return response.Length > 0;
    }

    private static bool TryBuildLegacyRatingRankPayload(
        MajakPlayer player,
        string message,
        out Dictionary<string, object?> payload,
        out string errorMessage)
    {
        payload = new();
        errorMessage = "";
        var values = message.Length > 7
            ? message[7..].Split(',', StringSplitOptions.None)
            : Array.Empty<string>();

        int rankDate = values.Length > 0 && int.TryParse(values[0], out int parsedDate) ? parsedDate : 0;
        int rankId = values.Length > 1 && int.TryParse(values[1], out int parsedId) ? parsedId : 0;
        int rankRefresh = values.Length > 2 && int.TryParse(values[2], out int parsedRefresh) ? parsedRefresh : 0;
        if (rankDate < 2000)
        {
            errorMessage = "grade rank debug parameter error.";
            return false;
        }
        if (rankRefresh == 0)
            rankRefresh = 7;

        payload = new Dictionary<string, object?>
        {
            [Key.GradeRankDate] = rankDate,
            [Key.GradeRankId] = rankId,
            [Key.GradeRankRefresh] = rankRefresh,
            [GKey.Pix] = player.Pix,
        };
        return true;
    }

    private static bool TryGetDebugHaipaiCommand(string message, out int debugYaku, out string response)
    {
        const int DbgYDaisangen = 1001;
        const int DbgYSuuankou = 1002;
        const int DbgYShosuushi = 1003;
        const int DbgYChinroutou = 1004;
        const int DbgYTsuisou = 1005;
        const int DbgYRyuisou = 1006;
        const int DbgYChurenpaotou = 1007;
        const int DbgYKokushi = 1008;
        const int DbgYDaisuushi = 1009;
        const int DbgYSuuankou2 = 1012;
        const int DbgYKokushi2 = 1013;
        const int DbgYChurenpaotou2 = 1014;
        const int DbgYSuukantsu = 1015;
        const int DbgT1 = 1016;

        (debugYaku, response) = message switch
        {
            "!HPCLR" => (-1, "配牌:初期化"),
            "!HPKOKU" => (DbgYKokushi, "配牌:国士無双(聴牌) To 親."),
            "!HPKOKU2" => (DbgYKokushi2, "配牌:国士無双13面待ち(聴牌) To 親."),
            "!HPSUUA" => (DbgYSuuankou, "配牌:四暗刻(天和) To 親."),
            "!HPSUUA2" => (DbgYSuuankou2, "配牌:四暗刻単騎(天和) To 親."),
            "!HPDAIG" => (DbgYDaisangen, "配牌:大三元(聴牌) To 親."),
            "!HPTSUI" => (DbgYTsuisou, "配牌:字一色(聴牌) To 親."),
            "!HPSHOS" => (DbgYShosuushi, "配牌:小四喜(聴牌) To 親."),
            "!HPDAIS" => (DbgYDaisuushi, "配牌:大四喜(聴牌) To 親."),
            "!HPRYUI" => (DbgYRyuisou, "配牌:緑一色(聴牌) To 親."),
            "!HPCHIN" => (DbgYChinroutou, "配牌:清老頭(聴牌) To 親."),
            "!HPCHUR" => (DbgYChurenpaotou, "配牌:九蓮宝燈(聴牌) To 親."),
            "!HPCHUR2" => (DbgYChurenpaotou2, "配牌:純正九蓮宝燈(聴牌) To 親."),
            "!HPSUUK" => (DbgYSuukantsu, "配牌:四槓子(聴牌) To 親."),
            _ when message.StartsWith("!HPTEST", StringComparison.Ordinal)
                && int.TryParse(message[7..], out int testNo)
                && testNo is >= 1 and <= 30 => (DbgT1 + testNo - 1, $"TEST{testNo}."),
            _ => (0, ""),
        };

        return !string.IsNullOrEmpty(response);
    }
}

/// <summary>
/// room:view 観戦入室


/// </summary>
public class ViewRoomCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly GameLogicService? _gameLogic;

    public ViewRoomCommand(PlayerSessionService session, GameLogicService? gameLogic = null)
    {
        _session = session;
        _gameLogic = gameLogic;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        int  roomId  = ctx.GetInt("roomId");
        string pwd   = ctx.GetString("roomPwd");
        string playerType = ctx.GetString("playerType");

        var room = _session.GetRoom(roomId);
        if (room == null)
        {
            await SendRoomConnectError(ctx, roomId, "ルームが見つかりません", LegacyErrorCode.CannotEnterRoom);
            return;
        }

        if (room.IsTournamentChannel && !HasTournamentViewRequiredKeys(ctx))
        {
            ctx.AbortConnectionWithReason($"{nameof(ViewRoomCommand)} missing tournament view key. roomId={roomId}");
            return;
        }

        if (room.IsTournamentChannel && !IsValidTournamentViewRequest(ctx, room))
        {
            await SendRoomConnectError(ctx, roomId, "トーナメント観戦情報が不正です", LegacyErrorCode.TournamentViewRoom);
            return;
        }

        // playerType check: AutoViewRoom rejects values other than G::valueViewer.
        if (playerType != GKey.ValueViewer && playerType != "viewer" && playerType != "2")
        {
            await SendRoomConnectError(ctx, roomId, "観戦者として入室できません", LegacyErrorCode.CannotEnterRoom);
            return;
        }

        // Password check based on AutoViewRoom.
        if (!string.IsNullOrEmpty(room.Password) && room.Password != pwd)
        {
            await SendRoomConnectError(ctx, roomId, "パスワードが違います", LegacyErrorCode.InvalidPassword);
            return;
        }

        if (room.Seats.Any(s => s?.MemberNo == player.MemberNo) || room.Viewers.Any(v => v.MemberNo == player.MemberNo))
        {
            await SendRoomConnectError(ctx, roomId, "既に入室しています。", LegacyErrorCode.SameUserAlreadyIn);
            return;
        }


        if (room.State != Models.Game.GameRoomState.Playing)
        {
            await SendRoomConnectError(ctx, roomId, "観戦できる状態ではありません", LegacyErrorCode.CannotEnterRoom);
            return;
        }


        if (!room.AddViewer(player))
        {
            await SendRoomConnectError(ctx, roomId, "観戦できる状態ではありません", LegacyErrorCode.CannotEnterRoom);
            return;
        }

        await ctx.Groups.AddToGroupAsync(ctx.ConnectionId, $"room_{roomId}");


        var memberListPayload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildMemberListPayload(room, player.MemberNo);
        if (Convert.ToInt32(memberListPayload[GKey.Count]) > 0)
            await ctx.Caller.SendAsync(Cmd.MemberList, memberListPayload);


        await ctx.Clients.Group($"room_{roomId}")
            .SendAsync(Cmd.AddMember, MajakServer.Commands.Room.RoomGetMembersCommand.BuildAddMemberPayload(room, player, GKey.ValueViewer));

        await ctx.Clients.Group($"chanel_{player.ChannelId}")
            .SendAsync(Cmd.DeleteMember, new
            {
                memberNo = player.Pix,
                pix      = player.Pix,
                k3e      = player.Pix,
            });

        await ctx.Caller.SendAsync(Cmd.ViewRoom, new
        {
            result     = 1,
            k1e        = GKey.ValueSuccess,
            roomId     = room.RoomId,
            k42e       = room.RoomId,
            memberNo   = player.Pix,
            pix        = player.Pix,
            k3e        = player.Pix,
            playerType = GKey.ValueViewer,
            k57e       = GKey.ValueViewer,
            roomTitle  = room.RoomTitle,
            k45e       = room.RoomTitle,
            roomOption = room.RoomOption,
            k46e       = room.RoomOption,
            state      = (int)room.State,
        });

        if (_gameLogic != null)
        {
            if (!await room.EngineLock.WaitAsync(TimeSpan.FromSeconds(5))) return;
            try
            {
                await _gameLogic.SendGameResyncAsync(room, ctx, player);
            }
            finally
            {
                room.EngineLock.Release();
            }
        }
    }

    private static Task SendRoomConnectError(CommandContext ctx, int roomId, string message, int failCode)
        => ctx.Caller.SendAsync(Cmd.ConnectTypeError,
            RoomConnectErrorPayload.Build(roomId, message, failCode));

    private static bool HasTournamentViewRequiredKeys(CommandContext ctx)
        => ctx.Payload.ContainsKey(Key.TournamentNo)
            && ctx.Payload.ContainsKey(Key.TournamentSubId)
            && ctx.Payload.ContainsKey(Key.TournamentChkRoomMember)
            && ctx.GetLong(Key.TournamentNo) != 0
            && ctx.GetInt(Key.TournamentSubId) != 0
            && !string.IsNullOrEmpty(ctx.GetString(Key.TournamentChkRoomMember));

    private static bool IsValidTournamentViewRequest(CommandContext ctx, GameRoom room)
    {
        long tournamentNo = ctx.GetLong(Key.TournamentNo);
        int tournamentSubId = ctx.GetInt(Key.TournamentSubId);
        string roomMembers = ctx.GetString(Key.TournamentChkRoomMember);
        if (room.TournamentSeqNo != 0 && room.TournamentSeqNo != tournamentNo)
            return false;
        if (room.TournamentSubId != 0 && room.TournamentSubId != tournamentSubId)
            return false;

        var requestMembers = roomMembers
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(GameConst.PlayerMaxCount)
            .ToHashSet(StringComparer.Ordinal);
        return requestMembers.Count > 0
            && room.Seats.Any(seat => seat != null && requestMembers.Contains(seat.MemberNo));
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
        19 => "十段",
        _ => "",
    };
}
