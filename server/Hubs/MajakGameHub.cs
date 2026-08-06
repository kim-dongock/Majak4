using Microsoft.AspNetCore.SignalR;
using MajakServer.Commands;
using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Hubs;

/// <summary>
/// SignalR Hub: single entry point for client messages.
/// Legacy equivalent: HMajChnlServer + HMajRoomServer.
/// Endpoint: ws://{host}/hubs/majak
/// </summary>
public class MajakGameHub : Hub
{
    private readonly PlayerSessionService               _session;
    private readonly RoomRegistryService                _roomRegistry;
    private readonly ChannelMemberService               _channelMemberSvc;
    private readonly LobbySessionLeaseService           _lobbySessions;
    private readonly GameAuthTokenService               _gameAuth;
    private readonly IServiceProvider                   _sp;

    public MajakGameHub(
        PlayerSessionService               session,
        RoomRegistryService                roomRegistry,
        ChannelMemberService               channelMemberSvc,
        LobbySessionLeaseService           lobbySessions,
        GameAuthTokenService               gameAuth,
        IServiceProvider                   sp)
    {
        _session          = session;
        _roomRegistry     = roomRegistry;
        _channelMemberSvc = channelMemberSvc;
        _lobbySessions    = lobbySessions;
        _gameAuth         = gameAuth;
        _sp               = sp;
    }

    // Connection lifecycle

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var token = http?.Request.Query["access_token"].FirstOrDefault()
            ?? http?.Request.Headers.Authorization.FirstOrDefault();
        var auth = _gameAuth.Validate(token);
        if (auth is null)
        {
            _sp.GetService<ILogger<MajakGameHub>>()?.LogWarning(
                "SignalR connection rejected: missing or invalid access token. connectionId={ConnectionId} remoteIp={RemoteIpAddress}",
                Context.ConnectionId,
                http?.Connection.RemoteIpAddress?.ToString() ?? "");
            Context.Abort();
            return;
        }
        _session.SetAuthenticatedMember(Context.ConnectionId, auth.MemberNo, auth.Pix);

        // Room/channel membership is resolved later by EnterChannel using pix.
        _sp.GetService<ILogger<MajakGameHub>>()?.LogInformation(
            "SignalR connected. connectionId={ConnectionId} memberNo={MemberNo} remoteIp={RemoteIpAddress}",
            Context.ConnectionId,
            auth.MemberNo,
            Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var player = _session.GetByConn(Context.ConnectionId);
            var disconnectReason = _session.PeekDisconnectReason(Context.ConnectionId);
            _sp.GetService<ILogger<MajakGameHub>>()?.LogWarning(exception,
            "SignalR disconnected. connectionId={ConnectionId} memberNo={MemberNo} channelId={ChannelId} roomId={RoomId} seatPos={SeatPos} isViewer={IsViewer} isOutPlayer={IsOutPlayer} serverInitiated={ServerInitiated} reasonSource={ReasonSource} reason={Reason} reasonAt={ReasonAt:o} exceptionType={ExceptionType} exceptionMessage={ExceptionMessage} remoteIp={RemoteIpAddress}",
            Context.ConnectionId,
            player?.MemberNo ?? "",
            player?.ChannelId ?? "",
            player?.RoomId,
            player?.SeatPos,
            player?.IsViewer ?? false,
            player?.IsOutPlayer ?? false,
            disconnectReason != null,
            disconnectReason?.Source ?? "client-or-network",
            disconnectReason?.Reason ?? "",
            disconnectReason?.At,
            exception?.GetType().Name ?? "",
            exception?.Message ?? "",
            Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "");
            if (player != null)
            {
                if (player.RoomId != null)
                {
                    await HandleRoomDisconnectAsync(player);
                }
                if (!string.IsNullOrEmpty(player.ChannelId))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chanel_{player.ChannelId}");
                    await Clients.Group($"chanel_{player.ChannelId}")
                        .SendAsync(Cmd.DeleteMember, new
                        {
                            memberNo = player.Pix,
                            pix      = player.Pix,
                            k3e      = player.Pix,
                        });
                    await _channelMemberSvc.LeaveAsync(player.ChannelId, player.MemberNo);
                }
                _session.Remove(Context.ConnectionId);
            }
        }
        finally
        {
            await _lobbySessions.ReleaseAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }

    /// <summary>
    /// Handles room disconnects.
    /// Legacy equivalent: HMajRoomServer::DispatchRoomSocketClose.
    ///   - Waiting: broadcast DeleteMember and update OK button state.
    ///   - Playing: set IsOutPlayer, trigger ProxyPlay, and update LimitCnt.
    ///   - Common: broadcast DeleteMember and remove Redis room state when empty.
    /// </summary>
    private async Task HandleRoomDisconnectAsync(Models.Player.MajakPlayer player, bool removeMemberMapping = true)
    {
        int roomId = player.RoomId!.Value;
        var room   = _session.GetRoom(roomId);
        if (room == null) return;

        int seatPos = (int)player.SeatPos;
        bool isViewer = player.IsViewer || room.Viewers.Any(v => v.MemberNo == player.MemberNo);

        if (room.State == Models.Game.GameRoomState.Playing && !isViewer)
        {
            // Disconnect during play (legacy: PS_PLAY / PS_CONTINUE).
            // Set the out-player flag (legacy: m_bIsOutPlayer = TRUE).
            player.IsOutPlayer = true;
            await _roomRegistry.SetContinueRoomAsync(player.MemberNo, room);

            // LimitCnt is the number of active connected players left in the room.
            room.LimitCnt = room.Seats.Count(s => s != null && !s.IsOutPlayer);

            // Run ProxyPlay while holding the engine lock.
            if (await room.EngineLock.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    var gameLogic = _sp.GetRequiredService<Services.GameLogicService>();
                    var ctx = new CommandContext
                    {
                        ConnectionId = Context.ConnectionId,
                        Player       = player,
                        Caller       = Clients.Caller,
                        Clients      = Clients,
                        Groups       = Groups,
                        AuthMemberNo = _session.GetAuthenticatedMember(Context.ConnectionId) ?? "",
                        AuthPix      = _session.GetAuthenticatedPix(Context.ConnectionId) ?? "",
                        Payload      = new Dictionary<string, object?>(),
                    };
                    if (seatPos >= 0 && seatPos < GameConst.PlayerMaxCount)
                    {
                        int engineOrder = room.SeatToEngineOrder[seatPos];
                        if (engineOrder >= 0 && engineOrder < GameConst.PlayerMaxCount)
                            await gameLogic.ProxyPlayAsync(room, ctx, engineOrder);
                    }
                }
                finally
                {
                    room.EngineLock.Release();
                }
            }

            // Broadcast DeleteMember (legacy: commandDeleteMember).
            string newHost = room.Seats
                .Where(s => s != null && !s.IsOutPlayer)
                .Select(s => s!.MemberNo)
                .FirstOrDefault() ?? "";

            await Clients.Group($"room_{roomId}")
                .SendAsync(Cmd.DeleteMember, Commands.Room.RoomGetMembersCommand.BuildDeleteMemberPayload(
                    newHost, player, GKey.ValuePlayer, seatPos));

            // Leave the SignalR group. During play, the seat is preserved and only the connection is detached.
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
            if (removeMemberMapping)
            {
                // Keep the seat reserved and disconnect only the connection so ReconnectToRoom can restore it.
                _session.DisconnectFromRoom(player);
            }
            else
            {
                player.RoomId = null;
            }
            // Redis: update active player count, and keep the room while the game can continue.
            var updatedRoom = _session.GetRoom(roomId);
            if (updatedRoom != null && updatedRoom.HasNoActiveMembers)
            {
                await _roomRegistry.UpdateMemberCountAsync(roomId, player.ChannelId, updatedRoom.ActivePlayerCount);
                await Clients.Group($"chanel_{player.ChannelId}")
                    .SendAsync(Cmd.RoomState, RoomStatePayload.Build(updatedRoom, "left"));
            }
            else if (updatedRoom != null)
            {
                await _roomRegistry.UpdateMemberCountAsync(roomId, player.ChannelId, updatedRoom.ActivePlayerCount);
                await Clients.Group($"chanel_{player.ChannelId}")
                    .SendAsync(Cmd.RoomState, RoomStatePayload.Build(updatedRoom, "left"));
            }
            return;
        }

        // Normal disconnect handling (Waiting / Finished).
        // Legacy DispatchRoomSocketClose broadcasts commandDeleteMember after removing the member.
        string roomHost = room.Seats
            .Where(s => s != null && s.MemberNo != player.MemberNo)
            .Select(s => s!.MemberNo)
            .FirstOrDefault() ?? "";
        string playerType = player.IsViewer ? GKey.ValueViewer : GKey.ValuePlayer;

        // Remove from PendingMatch (legacy: RemoveReservePlayer).
        _session.RemovePendingMatchMember(roomId, player.MemberNo);

        _session.LeaveRoom(player);

        await Clients.Group($"room_{roomId}")
            .SendAsync(Cmd.DeleteMember, Commands.Room.RoomGetMembersCommand.BuildDeleteMemberPayload(
                roomHost, player, playerType, seatPos));

        if (room.State == Models.Game.GameRoomState.Waiting && seatPos >= 0 && seatPos < GameConst.PlayerMaxCount)
        {
            // Reset OK button state and broadcast it again.
            room.OkButtonStates[seatPos] = false;
            var okPayload = new Dictionary<string, object>();
            for (int i = 0; i < GameConst.PlayerMaxCount; i++)
                okPayload[$"{Key.OkButton}{i}"] = room.OkButtonStates[i] ? 1 : 0;
            await Clients.Group($"room_{roomId}").SendAsync(Cmd.SendOkButton, okPayload);
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");

        var afterRoom = _session.GetRoom(roomId);
        if (afterRoom == null)
        {
            _session.ExpirePendingMatch(roomId);
            await _roomRegistry.RemoveRoomAsync(roomId, player.ChannelId);
            await Clients.Group($"chanel_{player.ChannelId}")
                .SendAsync(Cmd.RoomState, RoomStatePayload.BuildEmpty(roomId, "left"));
        }
        else
        {
            await _roomRegistry.UpdateMemberCountAsync(roomId, player.ChannelId, afterRoom.ActivePlayerCount);
            await Clients.Group($"chanel_{player.ChannelId}")
                .SendAsync(Cmd.RoomState, RoomStatePayload.Build(afterRoom, "left"));
        }
    }

    public async Task NotifyGameClientReady(int roomId)
    {
        var player = _session.GetByConn(Context.ConnectionId);
        if (player == null || player.RoomId != roomId) return;
        var gameLogic = _sp.GetRequiredService<GameLogicService>();
        bool allReady = await gameLogic.MarkGameClientReadyAsync(roomId, Context.ConnectionId);

        var room = _session.GetRoom(roomId);
        if (room?.State != GameRoomState.Playing || room.PlayHistory.Count == 0) return;
        if (!await room.EngineLock.WaitAsync(TimeSpan.FromSeconds(5))) return;
        try
        {
            var ctx = new CommandContext
            {
                ConnectionId = Context.ConnectionId,
                Player = player,
                Caller = Clients.Caller,
                Clients = Clients,
                Groups = Groups,
                Payload = new Dictionary<string, object?>(),
            };
            await gameLogic.SendGameResyncAsync(room, ctx, player, includePrompt: false);
            if (allReady)
                await gameLogic.StartGameActionsAsync(room, ctx);
        }
        finally
        {
            room.EngineLock.Release();
        }
    }

    public async Task<object> GetGameStateFingerprint(int roomId)
    {
        var player = _session.GetByConn(Context.ConnectionId);
        var room = player?.RoomId == roomId ? _session.GetRoom(roomId) : null;
        if (room == null)
        {
            return new { valid = false, playing = false, roomId, kyokuCnt = -1, leftCount = -1 };
        }

        if (!await room.EngineLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            return new { valid = false, playing = false, roomId, kyokuCnt = -1, leftCount = -1 };
        }
        try
        {
            return new
            {
                valid = true,
                playing = room.State == GameRoomState.Playing,
                roomId,
                kyokuCnt = room.Engine.HanchanInfo.CurKyoku,
                leftCount = room.Engine.GetBipaiCount(),
                handCounts = room.Engine.Player.Select(enginePlayer => enginePlayer.Tehai.Count).ToArray(),
                discardCounts = room.Engine.Player.Select(enginePlayer => enginePlayer.Sutehai.Count).ToArray(),
                meldCounts = room.Engine.Player.Select(enginePlayer => enginePlayer.Furo.Count).ToArray(),
                flowerCounts = room.Engine.Player.Select(enginePlayer => enginePlayer.NukiDora.Count).ToArray(),
                reachStates = room.Engine.Player.Select(enginePlayer => enginePlayer.RichiType != Engine.RichiType.None).ToArray(),
            };
        }
        finally
        {
            room.EngineLock.Release();
        }
    }

    public async Task RequestGameResync(int roomId)
    {
        var player = _session.GetByConn(Context.ConnectionId);
        var log = _sp.GetService<ILogger<MajakGameHub>>();
        if (player == null || player.RoomId != roomId)
        {
            log?.LogWarning("RequestGameResync skipped: invalid session. connectionId={ConnectionId} requestedRoomId={RequestedRoomId} hasPlayer={HasPlayer} memberNo={MemberNo} playerRoomId={PlayerRoomId}",
                Context.ConnectionId,
                roomId,
                player != null,
                player?.MemberNo ?? "",
                player?.RoomId);
            return;
        }
        var room = _session.GetRoom(roomId);
        if (room == null)
        {
            log?.LogWarning("RequestGameResync skipped: room not found. connectionId={ConnectionId} memberNo={MemberNo} requestedRoomId={RequestedRoomId}",
                Context.ConnectionId,
                player.MemberNo,
                roomId);
            return;
        }

        log?.LogDebug("RequestGameResync begin. connectionId={ConnectionId} memberNo={MemberNo} roomId={RoomId} isViewer={IsViewer} seatPos={SeatPos} engineOrder={EngineOrder} state={RoomState} playHistoryCount={PlayHistoryCount} leftCount={LeftCount} handCounts={HandCounts} discardCounts={DiscardCounts}",
            Context.ConnectionId,
            player.MemberNo,
            roomId,
            player.IsViewer,
            player.SeatPos,
            player.EngineOrder,
            room.State,
            room.PlayHistory.Count,
            room.Engine.GetBipaiCount(),
            string.Join(',', room.Engine.Player.Select(p => p.Tehai.Count)),
            string.Join(',', room.Engine.Player.Select(p => p.Sutehai.Count)));

        var gameLogic = _sp.GetRequiredService<GameLogicService>();
        var ctx = new CommandContext
        {
            ConnectionId = Context.ConnectionId,
            Player       = player,
            Caller       = Clients.Caller,
            Clients      = Clients,
            Groups       = Groups,
            AuthMemberNo = _session.GetAuthenticatedMember(Context.ConnectionId) ?? "",
            AuthPix      = _session.GetAuthenticatedPix(Context.ConnectionId) ?? "",
            Payload      = new Dictionary<string, object?>(),
        };

        if (!await room.EngineLock.WaitAsync(TimeSpan.FromSeconds(5))) return;
        try
        {
            await gameLogic.SendGameResyncAsync(room, ctx, player);
            log?.LogDebug("RequestGameResync complete. connectionId={ConnectionId} memberNo={MemberNo} roomId={RoomId}",
                Context.ConnectionId,
                player.MemberNo,
                roomId);
        }
        finally
        {
            room.EngineLock.Release();
        }
    }

    // Command dispatch

    /// <summary>
    /// Entry point for all legacy game commands sent by the client.
    /// payload: Dictionary&lt;string, object&gt;
    /// </summary>
    public async Task SendCommand(string code, Dictionary<string, object?> payload)
    {
        var player = _session.GetByConn(Context.ConnectionId);
        if (code == Cmd.GetMajItemList)
        {
            _sp.GetService<ILogger<MajakGameHub>>()?.LogInformation(
                "[GetMajItemList] hub received. connectionId={ConnectionId} memberNo={MemberNo} payloadKeys={PayloadKeys}",
                Context.ConnectionId,
                player?.MemberNo ?? "",
                string.Join(",", payload.Keys));
        }

        var cmdCtx = new CommandContext
        {
            ConnectionId = Context.ConnectionId,
            Player       = player,
            Caller       = Clients.Caller,
            Clients      = Clients,
            Groups       = Groups,
            AbortConnection = () => AbortConnectionWithReason(code, player, "Legacy command requested connection abort without a detailed reason."),
            AbortConnectionWithReason = reason => AbortConnectionWithReason(code, player, reason),
            RemoteIpAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "",
            AuthMemberNo = _session.GetAuthenticatedMember(Context.ConnectionId) ?? "",
            Payload      = payload!,
        };

        var handler = ResolveCommand(code);
        if (handler == null)
        {
            AbortConnectionWithReason(code, player, $"Unknown legacy command. code={code}");
            _sp.GetService<ILogger<MajakGameHub>>()?.LogWarning(
                "Unknown legacy command. Code={Code}, ConnectionId={ConnectionId}, MemberNo={MemberNo}",
                code, Context.ConnectionId, player?.MemberNo ?? "");
            return;
        }

        try
        {
            await handler.ExecuteAsync(cmdCtx);
        }
        catch (Exception ex)
        {
            _sp.GetService<ILogger<MajakGameHub>>()?.LogError(ex,
                "Legacy command handler failed. Code={Code}, ConnectionId={ConnectionId}, MemberNo={MemberNo}",
                code, Context.ConnectionId, player?.MemberNo ?? "");
            AbortConnectionWithReason(code, player, $"Legacy command handler failed. exceptionType={ex.GetType().Name} message={ex.Message}");
        }
    }

    private void AbortConnectionWithReason(string code, Models.Player.MajakPlayer? player, string reason)
    {
        _session.RecordDisconnectReason(Context.ConnectionId, $"SendCommand:{code}", reason);
        _sp.GetService<ILogger<MajakGameHub>>()?.LogWarning(
            "SignalR abort requested. connectionId={ConnectionId} memberNo={MemberNo} channelId={ChannelId} roomId={RoomId} seatPos={SeatPos} command={Command} reason={Reason} remoteIp={RemoteIpAddress}",
            Context.ConnectionId,
            player?.MemberNo ?? "",
            player?.ChannelId ?? "",
            player?.RoomId,
            player?.SeatPos,
            code,
            reason,
            Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "");
        Context.Abort();
    }

    private ICommand? ResolveCommand(string code) => code switch
    {
        // Channel commands
        Cmd.GetDetailRec         => _sp.GetRequiredService<Commands.Channel.GetDetailRecCommand>(),
        Cmd.AutoMatching         => _sp.GetRequiredService<Commands.Channel.AutoMatchingCommand>(),
        Cmd.CancelAutoMatching   => _sp.GetRequiredService<Commands.Channel.CancelAutoMatchingCommand>(),
        Cmd.AutoEnterRoom        => _sp.GetRequiredService<Commands.Channel.AutoEnterRoomCommand>(),
        Cmd.GetServerTime        => _sp.GetRequiredService<Commands.Channel.GetServerTimeCommand>(),
        Cmd.MoneyReplenishment   => _sp.GetRequiredService<Commands.Channel.MoneyReplenishmentCommand>(),
        Cmd.ApplyEarnedMoney     => _sp.GetRequiredService<Commands.Channel.ApplyEarnedMoneyCommand>(),
        Cmd.YakumanBonus         => _sp.GetRequiredService<Commands.Channel.YakumanBonusCommand>(),
        Cmd.GetTitle             => _sp.GetRequiredService<Commands.Channel.GetTitleCommand>(),
        Cmd.UseEmoticon          => _sp.GetRequiredService<Commands.Room.RoomEmoticonCommand>(),
        Cmd.GetMissionList       => _sp.GetRequiredService<Commands.Channel.GetMissionListCommand>(),
        Cmd.RcvWeeklyReward      => _sp.GetRequiredService<Commands.Channel.RcvWeeklyRewardCommand>(),
        Cmd.RcvSerialBonus       => _sp.GetRequiredService<Commands.Channel.RcvSerialBonusCommand>(),
        Cmd.ShopItemRequest      => _sp.GetRequiredService<Commands.Channel.ShopItemRequestCommand>(),
        Cmd.CustomItem           => _sp.GetRequiredService<Commands.Channel.CustomItemCommand>(),
        Cmd.BuyCustomItem        => _sp.GetRequiredService<Commands.Channel.BuyCustomItemCommand>(),
        Cmd.EquipCustomItem      => _sp.GetRequiredService<Commands.Channel.EquipCustomItemCommand>(),
        Cmd.AvatarGear           => _sp.GetRequiredService<Commands.Channel.AvatarGearCommand>(),
        Cmd.BuyMajItem           => _sp.GetRequiredService<Commands.Channel.BuyMajItemCommand>(),
        Cmd.SelectMajItem        => _sp.GetRequiredService<Commands.Channel.SelectMajItemCommand>(),
        Cmd.GetMajItemList       => _sp.GetRequiredService<Commands.Channel.GetMajItemListCommand>(),
        Cmd.GetGem               => _sp.GetRequiredService<Commands.Channel.GetGemCommand>(),
        Cmd.RatingRankInfo       => _sp.GetRequiredService<Commands.Channel.RatingRankInfoCommand>(),
        Cmd.Invite               => _sp.GetRequiredService<Commands.Channel.InviteCommand>(),
        Cmd.InviteResponse       => _sp.GetRequiredService<Commands.Channel.InviteResponseCommand>(),
        Cmd.TournamentList       => _sp.GetRequiredService<Commands.Channel.TournamentListCommand>(),
        Cmd.TournamentRegist     => _sp.GetRequiredService<Commands.Channel.TournamentRegistCommand>(),
        Cmd.TournamentJoin       => _sp.GetRequiredService<Commands.Channel.TournamentJoinCommand>(),
        Cmd.TournamentJoinCancel => _sp.GetRequiredService<Commands.Channel.TournamentJoinCancelCommand>(),
        Cmd.TournamentDetail     => _sp.GetRequiredService<Commands.Channel.TournamentDetailCommand>(),
        Cmd.SetCustomItem        => _sp.GetRequiredService<Commands.Channel.SetCustomItemCommand>(),
        // Channel lifecycle commands.
        Cmd.EnterChannel         => _sp.GetRequiredService<Commands.Channel.EnterChannelCommand>(),
        Cmd.RoomCreated          => _sp.GetRequiredService<Commands.Channel.CreateRoomCommand>(),
        Cmd.GetRoomList          => _sp.GetRequiredService<Commands.Channel.GetRoomListCommand>(),
        Cmd.GetMemberList        => _sp.GetRequiredService<Commands.Channel.GetMemberListCommand>(),
        Cmd.GetRoomMembers       => _sp.GetRequiredService<Commands.Room.RoomGetMembersCommand>(),
        Cmd.ExitChannel          => _sp.GetRequiredService<Commands.Channel.ExitChannelCommand>(),
        Cmd.HanChatRelay         => _sp.GetRequiredService<Commands.Channel.HanChatAllRelayCommand>(),
        Cmd.HanChatOneToOne      => _sp.GetRequiredService<Commands.Channel.HanChatOneToOneCommand>(),
        Cmd.HanChatOneToOneString => _sp.GetRequiredService<Commands.Channel.HanChatOneToOneStringCommand>(),
        Cmd.HanChatOneToOneEnd   => _sp.GetRequiredService<Commands.Channel.HanChatOneToOneEndCommand>(),
        Cmd.ViewRoom             => _sp.GetRequiredService<Commands.Channel.ViewRoomCommand>(),
        Cmd.Complaint            => _sp.GetRequiredService<Commands.Channel.ComplaintCommand>(),
        // Room commands
        Cmd.ExitRoom             => _sp.GetRequiredService<Commands.Room.RoomExitRoomCommand>(),
        Cmd.SendOkButton         => _sp.GetRequiredService<Commands.Room.SendOkButtonCommand>(),
        Cmd.PushOkButton         => _sp.GetRequiredService<Commands.Room.PushOkButtonCommand>(),
        Cmd.EnterRoomCmd         => _sp.GetRequiredService<Commands.Room.RoomEnterRoomCommand>(),
        Cmd.AlterRoom            => _sp.GetRequiredService<Commands.Room.RoomAlterRoomCommand>(),
        Cmd.EventInfo            => _sp.GetRequiredService<Commands.Room.EventInfoCommand>(),
        Cmd.PaiInfoList          => _sp.GetRequiredService<Commands.Room.PaiInfoListCommand>(),
        Cmd.IpAdapterInfo        => _sp.GetRequiredService<Commands.Room.IpAdapterInfoCommand>(),
        Cmd.RoomState            => _sp.GetRequiredService<Commands.Room.RoomStateCommand>(),
        Cmd.Tsumikomi            => _sp.GetRequiredService<Commands.Room.TsumikomiCommand>(),
        // Game commands
        Cmd.GamePlay             => _sp.GetRequiredService<Commands.Game.GamePlayCommand>(),
        Cmd.AgariRec             => _sp.GetRequiredService<Commands.Game.AgariRecCommand>(),
        Cmd.History              => _sp.GetRequiredService<Commands.Game.HistoryCommand>(),
        Cmd.GameReport           => _sp.GetRequiredService<Commands.Game.GameReportCommand>(),
        Cmd.ReplayNavi           => _sp.GetRequiredService<Commands.Game.ReplayNaviCommand>(),
        Cmd.ReserveChance        => _sp.GetRequiredService<Commands.Game.ReserveChanceCommand>(),
        _                        => null,
    };
}

