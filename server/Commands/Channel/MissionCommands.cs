using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Commands.Channel;

/// <summary>mjkc32e ミッションリスト取得</summary>
public class GetMissionListCommand : ICommand
{
    private readonly MissionService _missionService;

    public GetMissionListCommand(MissionService missionService)
        => _missionService = missionService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        MissionListResult result;
        try
        {
            result = await _missionService.GetMissionListAsync(player);
        }
        catch
        {
            await ctx.Caller.SendAsync(Cmd.GetMissionList, new Dictionary<string, object>
            {
                ["result"] = 0,
                [GKey.Result] = "v2e",
                [GKey.Message] = "エラーが発生しました。",
            });
            return;
        }

        var resp = new Dictionary<string, object>
        {
            ["result"]          = 1,
            [GKey.Result]        = "v1e",
            [Key.PointDayOwn]   = result.PointDayOwn,
            [Key.PointDayMax]   = result.PointDayMax,
            [Key.PointWeekOwn]  = result.PointWeekOwn,
            [Key.PointWeekMax]  = result.PointWeekMax,
        };
        // デイリーミッション 11 件
        for (int i = 0; i < 11; i++)
        {
            string key = i switch {
                0 => Key.DailyMission1,  1 => Key.DailyMission2,
                2 => Key.DailyMission3,  3 => Key.DailyMission4,
                4 => Key.DailyMission5,  5 => Key.DailyMission6,
                6 => Key.DailyMission7,  7 => Key.DailyMission8,
                8 => Key.DailyMission9,  9 => Key.DailyMission10,
                _ => Key.DailyMission11,
            };
            resp[key] = i < result.DailyMissions.Length ? result.DailyMissions[i] : 0;
        }
        // 週間報酬 8 件
        for (int i = 0; i < 8; i++)
        {
            string key = i switch {
                0 => Key.WeeklyReward1, 1 => Key.WeeklyReward2,
                2 => Key.WeeklyReward3, 3 => Key.WeeklyReward4,
                4 => Key.WeeklyReward5, 5 => Key.WeeklyReward6,
                6 => Key.WeeklyReward7, _ => Key.WeeklyReward8,
            };
            resp[key] = i < result.WeeklyRewards.Length ? result.WeeklyRewards[i] : 0;
        }
        await ctx.Caller.SendAsync(Cmd.GetMissionList, resp);
    }
}

/// <summary>mjkc33e 週間報酬受取</summary>
public class RcvWeeklyRewardCommand : ICommand
{
    private readonly MissionService   _missionService;
    private readonly GameMoneyService _moneyService;

    public RcvWeeklyRewardCommand(MissionService missionService, GameMoneyService moneyService)
    {
        _missionService = missionService;
        _moneyService   = moneyService;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        int rewardId = ctx.GetInt(Key.WeeklyRewardId);
        var (ok, newMoney, newGemCount, message) = await _missionService.ReceiveWeeklyRewardAsync(
            player, rewardId, _moneyService);

        if (!ok)
        {
            await ctx.Caller.SendAsync(Cmd.RcvWeeklyReward, new Dictionary<string, object>
            {
                ["result"] = 0,
                [GKey.Result] = GKey.ValueFailure,
                ["message"] = message,
                [GKey.Message] = message,
            });
            return;
        }

        await ctx.Caller.SendAsync(Cmd.RcvWeeklyReward, new Dictionary<string, object>
        {
            ["result"] = 1,
            [GKey.Result] = GKey.ValueSuccess,
            ["gammoney"] = newMoney,
            [GKey.GamMoney] = newMoney,
            ["gemcount"] = newGemCount,
            [Key.GemCount] = newGemCount,
            ["slevel"] = player.SLevel,
            [GKey.SLevel] = player.SLevel,
            ["nlevel"] = player.NLevel,
            [GKey.NLevel] = player.NLevel,
            ["message"] = message,
            [GKey.Message] = message,
        });
    }
}

/// <summary>mjkc34e シリアルボーナス受取</summary>
public class RcvSerialBonusCommand : ICommand
{
    private readonly MissionService     _missionService;
    private readonly GameMoneyService   _moneyService;

    public RcvSerialBonusCommand(MissionService missionService, GameMoneyService moneyService)
    {
        _missionService = missionService;
        _moneyService   = moneyService;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        string serialCode = ctx.GetString(Key.SerialCode);
        var (result, newMoney, message) = await _missionService.ReceiveSerialBonusAsync(
            player, serialCode, _moneyService);

        var packet = new Dictionary<string, object>
        {
            [GKey.Result] = result == 1 ? "v1e" : "v2e",
            [GKey.Message] = message,
        };

        if (result == 1)
        {
            packet[GKey.GamMoney] = newMoney;
            packet[Key.GemCount] = player.GemCount;
            packet[GKey.SLevel] = player.SLevel;
            packet[GKey.NLevel] = player.NLevel;
        }

        await ctx.Caller.SendAsync(Cmd.RcvSerialBonus, packet);
    }
}

