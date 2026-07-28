using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Protocol;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Services;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Engine;

namespace MajakServer.Commands.Game;

/// <summary>



/// </summary>
public class GamePlayCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly GameLogicService     _gameLogic;

    public GamePlayCommand(PlayerSessionService session, GameLogicService gameLogic)
    {
        _session   = session;
        _gameLogic = gameLogic;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null)
        {
            ctx.AbortConnectionWithReason("ProcessCommand_GamePlay player is null.");
            return;
        }
        if (player.RoomId == null)
        {
            ctx.AbortConnectionWithReason($"ProcessCommand_GamePlay player is not in room. memberNo={player.MemberNo}");
            return;
        }

        var room = _session.GetRoom(player.RoomId.Value);
        if (room == null)
        {
            ctx.AbortConnectionWithReason($"ProcessCommand_GamePlay room is null. memberNo={player.MemberNo} roomId={player.RoomId}");
            return;
        }


        if (room.Engine.GameStatus == Engine.GameStatus.NotPlaying ||
            player.IsViewer)
        {
            ctx.AbortConnectionWithReason($"ProcessCommand_GamePlay invalid status. memberNo={player.MemberNo} gameStatus={room.Engine.GameStatus} isViewer={player.IsViewer}");
            return;
        }

        if (!ctx.Payload.ContainsKey("playType")
            || !ctx.Payload.ContainsKey("seatOrder")
            || !ctx.Payload.ContainsKey("action")
            || ctx.GetString("playType") != "MJPID_ACTION")
        {
            ctx.AbortConnectionWithReason($"ProcessCommand_GamePlay invalid action packet. memberNo={player.MemberNo} playType={ctx.GetString("playType")}");
            return;
        }

        int order = ctx.GetInt("seatOrder");
        if (order < 0 || order >= GameConst.PlayerMaxCount)
        {
            ctx.AbortConnectionWithReason($"ProcessCommand_GamePlay invalid seatOrder. memberNo={player.MemberNo} seatOrder={order}");
            return;
        }



        if (order != player.EngineOrder)
        {
            bool isHost = room.Seats[0]?.MemberNo == player.MemberNo || room.CreatorNo == player.MemberNo;
            int playerPos = room.Engine.HanchanInfo.Player[order];
            bool canHostProxy = (room.IsTrainingChannel || room.IsTournamentChannel) &&
                                isHost && playerPos >= 0 && playerPos < room.Seats.Length && room.Seats[playerPos] == null;
            if (!canHostProxy)
            {
                ctx.AbortConnectionWithReason($"ProcessCommand_GamePlay nOrder error. memberNo={player.MemberNo} seatOrder={order} engineOrder={player.EngineOrder}");
                return;
            }
        }

        await _gameLogic.GamePlayProcessAsync(room, ctx);
    }
}

/// <summary>


/// </summary>
public class AgariRecCommand : ICommand
{
    private readonly HistoryRepository _histRepo;

    public AgariRecCommand(HistoryRepository histRepo) => _histRepo = histRepo;

    public async Task ExecuteAsync(CommandContext ctx)
    {


        await Task.CompletedTask;
    }
}

/// <summary>


/// </summary>
public class HistoryCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly HistoryRepository    _historyRepo;
    private readonly LogRepository        _mysqlLog;
    private readonly RatingService        _ratingService;
    private readonly PlayerRepository     _playerRepo;

    public HistoryCommand(
        PlayerSessionService session,
        HistoryRepository    historyRepo,
        LogRepository        mysqlLog,
        RatingService        ratingService,
        PlayerRepository     playerRepo)
    {
        _session       = session;
        _historyRepo    = historyRepo;
        _mysqlLog      = mysqlLog;
        _ratingService = ratingService;
        _playerRepo    = playerRepo;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {


        await Task.CompletedTask;
    }

    private static GameReport BuildReport(
        Models.Game.GameRoom room, CommandContext ctx)
    {
        var report = new GameReport
        {
            RoomId     = room.RoomId,
            ChannelId  = room.ChannelId,
            PrivateYn  = room.IsPrivate,
            RoomOption = room.RoomOption,
            MoneyRate  = room.MoneyRate,
            MinMoney   = room.MinMoney,
            MaxMoney   = room.MaxMoney,
        };

        for (int i = 0; i < 4; i++)
        {
            var seat = room.Seats[i];
            if (seat == null) continue;

            report.Users[i] = new GameReport.UserResult
            {
                MemberNo    = seat.MemberNo,
                Connected   = true,
                IpAddress   = seat.IpAddress,
                Gateway     = seat.Gateway,
                MacAddr     = seat.MacAddr,
                PrevMoney   = seat.GamMoney,
                CurrMoney   = seat.GamMoney,
            };
        }
        return report;
    }
}

/// <summary>

/// </summary>
public class ReplayNaviCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public ReplayNaviCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.RoomId == null) return;

        var room = _session.GetRoom(player.RoomId.Value);
        if (room == null) return;

        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.ReplayNavi, ctx.Payload);
    }
}

/// <summary>


/// </summary>
public class ReserveChanceCommand : ICommand
{
    private const string ChanceItemCode = "C01";

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.IsViewer) return;

        bool reserve = ctx.GetBool(GKey.ReserveChance) || ctx.GetBool("reserveChance");
        bool canUse = HasUsableChanceItem(player);
        if (reserve && !canUse)
        {
            player.ReserveChanceItem = false;
            await ctx.Caller.SendAsync(Cmd.ReserveChance, new
            {
                result = 0,
                k1e = GKey.ValueFailure,
                memberNo = player.Pix,
                pix = player.Pix,
                k3e = player.Pix,
                reserveChance = false,
                k118e = false,
                canUseChanceItem = false,
            });
            return;
        }

        player.ReserveChanceItem = reserve;
        await ctx.Caller.SendAsync(Cmd.ReserveChance, new
        {
            result = 1,
            k1e = GKey.ValueSuccess,
            memberNo = player.Pix,
            pix = player.Pix,
            k3e = player.Pix,
            reserveChance = player.ReserveChanceItem,
            k118e = player.ReserveChanceItem,
            canUseChanceItem = canUse,
        });
    }

    public static bool HasUsableChanceItem(MajakPlayer player)
        => player.MajItems.Any(item => item.ItemCode == ChanceItemCode && item.IsValid && item.Qty > 0);
}

/// <summary>


/// </summary>
public class GameReportCommand : ICommand
{
    public Task ExecuteAsync(CommandContext ctx) => Task.CompletedTask;
}
/// <summary>

