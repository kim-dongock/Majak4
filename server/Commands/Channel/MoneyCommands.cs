using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Commands.Channel;


public class MoneyReplenishmentCommand : ICommand
{
    private readonly GameMoneyService _moneyService;

    public MoneyReplenishmentCommand(GameMoneyService moneyService)
        => _moneyService = moneyService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;


        // .NET command context has no abort hook, so do not send a response.
        string subId = player.ChannelId.Length >= 11 ? player.ChannelId.Substring(6, 5) : "";
        if (subId.Length >= 3 && subId[2] is 'D' or 'T') return;
        if (player.RoomId.HasValue) return;


        var (ok, newMoney, lentMoney, restAllIn, repType) =
            await _moneyService.ReplenishAsync(player, 0);

        var packet = new Dictionary<string, object>
        {
            ["memberNo"]              = player.Pix,
            ["pix"]                   = player.Pix,
            ["result"]               = ok ? "success" : "failure",
            ["rating"]               = player.Rating,                 // G::keyRating
            ["slevel"]               = player.SLevel,                 // G::keySLevel
            ["nlevel"]               = player.NLevel,                 // G::keyNLevel
            ["gammoney"]             = newMoney,                      // G::keyGamMoney
            [GKey.Pix]           = player.Pix,
            [GKey.Result]             = ok ? "v1e" : "v2e",
            [GKey.Rating]             = player.Rating,
            [GKey.SLevel]             = player.SLevel,
            [GKey.NLevel]             = player.NLevel,
            [GKey.GamMoney]           = newMoney,
            [Key.ReplenishmentType]  = repType,                       // mjkk42e
            [Key.RestAllInCnt]       = restAllIn,                     // mjkk43e
            [Key.AllInCnt]           = player.AllinCnt,               // mjkk44e
            [Key.UseLentMoney]       = lentMoney,
        };



        if (ok)
            await ctx.Clients.Group($"chanel_{player.ChannelId}").SendAsync(Cmd.MoneyReplenishment, packet);
        else
            await ctx.Caller.SendAsync(Cmd.MoneyReplenishment, packet);
    }
}


public class ApplyEarnedMoneyCommand : ICommand
{
    private readonly GameMoneyService _moneyService;

    public ApplyEarnedMoneyCommand(GameMoneyService moneyService)
        => _moneyService = moneyService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        var (ok, newMoney) = await _moneyService.ApplyEarnedMoneyAsync(player);


        var packet = new Dictionary<string, object>
        {
            ["memberNo"] = player.Pix,
            ["pix"]      = player.Pix,
            ["result"]   = ok ? "success" : "failure",
            ["rating"]   = player.ActiveRecord.Rating,   // G::keyRating
            ["slevel"]   = player.SLevel,                 // G::keySLevel
            ["nlevel"]   = player.NLevel,                 // G::keyNLevel
            ["gammoney"] = newMoney,                      // G::keyGamMoney
            [GKey.Pix] = player.Pix,
            [GKey.Result]   = ok ? "v1e" : "v2e",
            [GKey.Rating]   = player.ActiveRecord.Rating,
            [GKey.SLevel]   = player.SLevel,
            [GKey.NLevel]   = player.NLevel,
            [GKey.GamMoney] = newMoney,
        };



        if (ok)
            await ctx.Clients.Group($"chanel_{player.ChannelId}").SendAsync(Cmd.ApplyEarnedMoney, packet);
        else
            await ctx.Caller.SendAsync(Cmd.ApplyEarnedMoney, packet);
    }
}

/// <summary>

///




///


/// </summary>
public class YakumanBonusCommand : ICommand
{
    public Task ExecuteAsync(CommandContext ctx) => Task.CompletedTask;
}

