using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Services;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Commands.Channel;


/// <summary>



/// </summary>
public class AvatarGearCommand : ICommand
{
    private readonly PlayerRepository _playerRepo;

    public AvatarGearCommand(PlayerRepository playerRepo) => _playerRepo = playerRepo;

    public Task ExecuteAsync(CommandContext ctx) => Task.CompletedTask;
}


/// <summary>







/// </summary>
public class BuyMajItemCommand : ICommand
{
    private readonly MajItemService _majItemService;
    private const int EItemDbError = 2;

    public BuyMajItemCommand(MajItemService majItemService)
        => _majItemService = majItemService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player   = ctx.Player;
        if (player == null)
        {
            await SendFailureAsync(ctx, EItemDbError, "チャンネル接続が完了していません。ロビーへ入り直してください。");
            return;
        }

        string sellCode = ctx.GetString(Key.SellCode);   // mjkk57e
        BuyMajItemResult result;
        try
        {
            result = await _majItemService.BuyMajItemAsync(player, sellCode);
        }
        catch
        {
            await SendFailureAsync(ctx, EItemDbError, "接続エラー");
            return;
        }

        if (!result.Ok)
        {
            // Failure: send G::valueFailure shape.
            object failCode = result.ErrorCode >= 0 ? result.ErrorCode : result.Error;
            await SendFailureAsync(ctx, failCode, string.IsNullOrEmpty(result.ErrorMessage) ? result.Error : result.ErrorMessage);
            return;
        }


        var packet = new Dictionary<string, object>
        {
            [Key.GemCount]       = result.GemCount,    // mjkk55e
            [GKey.GamMoney]      = result.GamMoney,    // k34e
            ["cashCount"]       = result.CashCount,
            ["result"]           = 0,
            [GKey.Result]        = "v1e",             // G::valueSuccess
        };
        if (!string.IsNullOrEmpty(result.NewUseItemCode))
        {
            packet[Key.ItemCode] = result.ItemCode;       // mjkk58e
            packet[Key.ItemCode + "0"] = result.ItemCode;
        }
        if (!string.IsNullOrEmpty(result.NewUseItemCode) && result.BuyDt != default)
        {
            packet[Key.BuyDate] = ToEpochSec(result.BuyDt);            // mjkk59e
            packet[Key.BuyDate + "0"] = result.BuyDt.ToString("yyyy/MM/dd");
        }
        if (!string.IsNullOrEmpty(result.NewUseItemCode) && result.EndDt != default)
        {
            packet[Key.EndDate] = ToEpochSec(result.EndDt);            // mjkk60e
            packet[Key.EndDate + "0"] = result.EndDt.ToString("yyyy/MM/dd");
        }
        if (!string.IsNullOrEmpty(result.NewUseItemCode) && result.Qty > 0)
        {
            packet[Key.ItemQuantity] = result.Qty; // mjkk140e
            packet["qty0"] = result.Qty;
        }

        await ctx.Caller.SendAsync(Cmd.BuyMajItem, packet);

        if (!string.IsNullOrEmpty(result.NewUseItemCode))
        {
            var selectPacket = new Dictionary<string, object>
            {
                ["result"] = 0,
                [GKey.Result] = "v1e",
            };
            int count = 0;
            if (!string.IsNullOrEmpty(result.OldUseItemCode))
            {
                selectPacket[Key.ItemCode + count] = result.OldUseItemCode;
                selectPacket[Key.UseFlag + count] = "0";
                count++;
            }
            selectPacket[Key.ItemCode + count] = result.NewUseItemCode;
            selectPacket[Key.UseFlag + count] = "1";
            count++;
            selectPacket[GKey.Count] = count;

            await ctx.Caller.SendAsync(Cmd.SelectMajItem, selectPacket);
        }
    }

    private static Task SendFailureAsync(CommandContext ctx, object failCode, string message)
        => ctx.Caller.SendAsync(Cmd.BuyMajItem, new Dictionary<string, object>
        {
            ["result"] = 0,
            [GKey.Result] = "v2e",
            ["failCode"] = failCode,
            [Key.FailCode] = failCode,
            [GKey.Message] = message,
        });

    private static long ToEpochSec(DateTime dt)
    {
        if (dt == default) return 0;
        var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        return new DateTimeOffset(utc).ToUnixTimeSeconds();
    }
}


/// <summary>





/// </summary>
public class SelectMajItemCommand : ICommand
{
    private readonly MajItemService _majItemService;

    public SelectMajItemCommand(MajItemService majItemService)
        => _majItemService = majItemService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player   = ctx.Player;
        if (player == null) return;

        string itemCode = ctx.GetString(Key.ItemCode);  // mjkk58e
        if (string.IsNullOrEmpty(itemCode)) return;

        var result = await _majItemService.SelectMajItemAsync(player, itemCode);

        if (!result.Ok)
        {
            object failCode = result.ErrorCode >= 0 ? result.ErrorCode : result.Error;
            await ctx.Caller.SendAsync(Cmd.SelectMajItem, new Dictionary<string, object>
            {
                ["result"] = -1,
                [GKey.Result] = "v2e",
                ["failCode"] = failCode,
                [Key.FailCode] = failCode,
                [GKey.Message] = string.IsNullOrEmpty(result.ErrorMessage) ? result.Error : result.ErrorMessage,
            });
            return;
        }



        var packet = new Dictionary<string, object>
        {
            ["result"] = 0,
            [GKey.Result] = "v1e",
            [GKey.Count] = result.ItemCount,  // G::keyCount (k25e)
        };
        int cnt = 0;
        if (!string.IsNullOrEmpty(result.OldItemCode))
        {
            packet[Key.ItemCode + cnt] = result.OldItemCode;  // mjkk58e0
            packet[Key.UseFlag  + cnt] = "0";                 // mjkk61e0
            cnt++;
        }
        packet[Key.ItemCode + cnt] = result.NewItemCode;       // mjkk58e1
        packet[Key.UseFlag  + cnt] = "1";                      // mjkk61e1

        await ctx.Caller.SendAsync(Cmd.SelectMajItem, packet);
    }
}


/// <summary>

///



///

///   k25e=count, mjkk58e{N}=itemCode, mjkk59e{N}=buyDt, mjkk60e{N}=endDt,
///   mjkk140e{N}=qty, mjkk61e{N}=useFlag
/// </summary>
public class GetMajItemListCommand : ICommand
{
    private readonly ItemService _itemService;
    private readonly ILogger<GetMajItemListCommand> _logger;

    public GetMajItemListCommand(ItemService itemService, ILogger<GetMajItemListCommand> logger)
    {
        _itemService = itemService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        try
        {
            await _itemService.LoadMajItemsAsync(player);
            _logger.LogInformation(
                "[GetMajItemList] memberNo={MemberNo} count={Count} items={Items}",
                player.MemberNo,
                player.MajItems.Count,
                string.Join("; ", player.MajItems.Select(item =>
                    $"{item.ItemCode}:use={(item.UseFlag ? 1 : 0)},qty={item.Qty},buy={item.BuyDt:O},end={item.EndDt:O}")));

            var packet = BuildMajItemListPacket(player.MajItems);
            _logger.LogInformation(
                "[GetMajItemList] packet memberNo={MemberNo} count={Count} values={Values}",
                player.MemberNo,
                player.MajItems.Count,
                string.Join("; ", Enumerable.Range(0, player.MajItems.Count).Select(i =>
                    $"{packet[Key.ItemCode + i]}:use={packet[Key.UseFlag + i]},qty={packet[Key.ItemQuantity + i]},end={packet[Key.EndDate + i]}")));

            await ctx.Caller.SendAsync(Cmd.GetMajItemList, packet);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetMajItemList] failed memberNo={MemberNo}", player.MemberNo);

            await ctx.Caller.SendAsync(Cmd.GetMajItemList, new Dictionary<string, object>
            {
                [GKey.Result] = "v2e",
                [GKey.Count] = 0,
            });
        }
    }

    private static Dictionary<string, object> BuildMajItemListPacket(IReadOnlyList<MajItemInfo> items)
    {
        var packet = new Dictionary<string, object>
        {
            [GKey.Result] = "v1e",
            [GKey.Count] = items.Count,
        };

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            packet[Key.ItemCode + i] = item.ItemCode;
            packet[Key.BuyDate + i] = ToEpochSec(item.BuyDt);
            packet[Key.EndDate + i] = ToEpochSec(item.EndDt);
            packet[Key.ItemQuantity + i] = item.Qty;
            packet[Key.UseFlag + i] = item.UseFlag ? 1 : 0;
        }

        return packet;
    }


    private static long ToEpochSec(DateTime dt)
    {
        if (dt == default) return 0;
        var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        return new DateTimeOffset(utc).ToUnixTimeSeconds();
    }
}


/// <summary>

///




/// </summary>
public class GetGemCommand : ICommand
{
    public Task ExecuteAsync(CommandContext ctx) => Task.CompletedTask;
}


/// <summary>



/// </summary>
public class RatingRankInfoCommand : ICommand
{
    private readonly PlayerRepository _playerRepo;
    private readonly GradeRankService _gradeRank;
    private readonly PlayerSessionService _session;

    public RatingRankInfoCommand(PlayerRepository playerRepo, GradeRankService gradeRank, PlayerSessionService session)
    {
        _playerRepo = playerRepo;
        _gradeRank  = gradeRank;
        _session    = session;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        int  rankId      = ctx.GetInt(Key.GradeRankId);
        int  rankDate    = ctx.GetInt(Key.GradeRankDate);
        int  rankRefresh = ctx.GetInt(Key.GradeRankRefresh);
        string requestPix  = ctx.GetString(GKey.Pix);
        string memberNo  = _session.ResolveMemberNo(requestPix);


        if (string.IsNullOrEmpty(memberNo)
            || rankDate == 0
            || rankRefresh == 0
            || !string.Equals(memberNo, player.MemberNo, StringComparison.Ordinal))
        {
            ctx.AbortConnectionWithReason($"RatingRankInfo invalid parameter. memberNo={memberNo}");
            return;
        }



        if (_gradeRank.IsBatchRunning)
        {
            await ctx.Caller.SendAsync(Models.Protocol.Cmd.RatingRankInfo, new
            {
                result          = 0,   // GRADE_RANK_STATUS_DURING
                gradeRankCnt    = 0,
                gradeSelectCnt  = 0,
                serverTime      = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
            return;
        }

        var now = DateTime.Now;
        int nowDate = int.Parse(now.ToString("yyyyMM"));




        const int REFRESH_MANAGE = 0x01;
        const int REFRESH_SELF   = 0x02;


        var rankList = await _playerRepo.GetGradeRankListAsync(rankDate, rankId, 30);


        GradeRankItem? selfItem = null;
        if ((rankRefresh & REFRESH_SELF) != 0)
        {
            selfItem = await _playerRepo.GetGradeRankSelfAsync(
                player.MemberNo, rankDate, player.GradeRecord.Grade);
        }


        string szIndex = "";
        if (selfItem != null && selfItem.Rank > 0)
            szIndex = await _gradeRank.GetGradeIndexStrAsync(rankDate, rankId, selfItem.Rank);

        object? selfInfo = selfItem == null ? null : new
        {
            memberNo   = player.Pix,
            pix        = player.Pix,
            avatarId   = selfItem.AvatarId,
            rating     = selfItem.Rating,
            grade      = selfItem.Grade,
            lastDate   = selfItem.LastDate,
            extraCount = selfItem.ExtraCount,
            rank       = selfItem.Rank,
            szIndex    = szIndex,
        };


        var selectList = (rankRefresh & REFRESH_MANAGE) != 0
            ? (IEnumerable<object>)(await _playerRepo.GetGradeManageListAsync()).Cast<object>()
            : Enumerable.Empty<object>();
        var selectListArr = selectList.ToList();

        var rankItems = rankList.Select((r, i) => new
        {
            memberNo   = r.MemberNo == player.MemberNo ? player.Pix : (_session.GetPixByMemberNo(r.MemberNo) ?? ""),
            pix        = r.MemberNo == player.MemberNo ? player.Pix : (_session.GetPixByMemberNo(r.MemberNo) ?? ""),
            avatarId   = r.AvatarId,
            dispRange  = 0,
            rank       = r.Rank,
            rating     = r.Rating,
            grade      = r.Grade,
            lastDate   = r.LastDate,
            isSelf     = r.MemberNo == player.MemberNo ? 1 : 0,
            extraCount = r.ExtraCount,
            extraDate  = "",
            isPro      = r.MemberNo == player.MemberNo ? (player.IsPro ? 1 : 0) : 0,
        }).ToList();


        int result = 1;

        await ctx.Caller.SendAsync(Cmd.RatingRankInfo, new
        {
            result          = result,
            serverTime      = now.ToString("yyyy/MM/dd HH:mm:ss"),
            gradeRankCnt    = rankItems.Count,
            gradeRankList   = rankItems,
            gradeRankSelf   = selfInfo,
            gradeSelectCnt  = selectListArr.Count,
            gradeSelectList = selectListArr,
            rankDate        = rankDate,
            rankId          = rankId,
        });
    }
}


/// <summary>


/// </summary>
public class TournamentListCommand : ICommand
{
    private readonly TournamentService  _tournament;
    private readonly TournamentRepository _tournRepo;

    public TournamentListCommand(TournamentService t, TournamentRepository r)
    { _tournament = t; _tournRepo = r; }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;
        if (!TournamentCommandGuards.RequireMember(ctx, player, nameof(TournamentListCommand))) return;


        var joinInfo = await _tournRepo.SelectJoinAsync(player.MemberNo);
        long joinSeqNo = joinInfo?.JoinStatus == TournamentJoinStatus.Join
            ? joinInfo.JoinSeqNo
            : 0;

        var now   = DateTime.Now;
        var plans = _tournament.GetAllPlans()
            .Where(p => p.ViewEndDt >= now || p.IsActive)
            .OrderBy(p => p.SeqNo)
            .ToList();

        var list = plans.Select(p => BuildPlanDto(p, joinSeqNo, _tournament)).ToList();


        var registBase = now.AddHours(TournamentConst.JoinOpenHours)
                            .AddMinutes(TournamentConst.JoinOpenMinutes);
        var registDayTime = new DateTime(
            registBase.Year, registBase.Month, registBase.Day,
            registBase.Hour, (registBase.Minute / 10) * 10, 0);

        await ctx.Caller.SendAsync(Cmd.TournamentList, new
        {
            result            = list.Count > 0 ? 1 : 0,
            tournamentCnt     = list.Count,
            tournamentList    = list,
            tournamentJoinChk = joinSeqNo,
            serverTime        = now.ToString("yyyy/MM/dd HH:mm:ss"),
            tournamentRegistDayTime = registDayTime.ToString("yyyy/MM/dd HH:mm:ss"),
        });
    }

    private static object BuildPlanDto(TournamentPlan p, long myJoinSeqNo, TournamentService tournament)
    {
        return new
        {
            seqNo         = p.SeqNo,
            playName      = p.PlayName,
            playStatus    = p.PlayStatus,
            playerNum     = p.PlayerNum,
            maxPlayerNum  = p.MaxPlayerNum,
            hasPassword   = string.IsNullOrEmpty(p.Password) ? 0 : 1,
            playMode      = p.PlayMode,
            playNum       = p.PlayNum,
            playTime      = p.PlayTime,
            joinMoney     = p.JoinMoney,
            gradeMoney1   = p.GradeMoney[0],
            gradeMoney2   = p.GradeMoney[1],
            gradeMoney3   = p.GradeMoney[2],
            gradeMoney4   = p.GradeMoney[3],
            playStartDt   = p.PlayStartDt.ToString("yyyy/MM/dd HH:mm:ss"),
            playEndDt     = p.PlayEndDt.ToString("yyyy/MM/dd HH:mm:ss"),
            playSchedule  = p.PlaySchedule,
            roomOption    = p.RoomOption,
            maxViewer     = p.MaxViewer,
            planMemberNo  = tournament.GetPixForMemberNo(p.PlanMemberNo),
            resultMember1 = tournament.GetPixForMemberNo(p.ResultMemberNo[0]),
            resultMember2 = tournament.GetPixForMemberNo(p.ResultMemberNo[1]),
            resultMember3 = tournament.GetPixForMemberNo(p.ResultMemberNo[2]),
            resultMember4 = tournament.GetPixForMemberNo(p.ResultMemberNo[3]),
        };
    }
}


/// <summary>


/// </summary>
public class TournamentRegistCommand : ICommand
{
    private readonly TournamentService _tournament;
    private readonly GameMoneyService  _money;

    public TournamentRegistCommand(TournamentService t, GameMoneyService m)
    { _tournament = t; _money = m; }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;
        if (!TournamentCommandGuards.RequireMemberAndKeys(ctx, player, nameof(TournamentRegistCommand),
            GKey.RoomOption, "tournamentBaseRule", "tournamentMoneyRule")) return;

        int    registFlag = ctx.GetInt("tournamentRegistFlag");
        string baseRule   = ctx.GetString("tournamentBaseRule");
        string moneyRule  = ctx.GetString("tournamentMoneyRule");
        string playName   = ctx.GetString("tournamentName");
        string playDate   = ctx.GetString("tournamentDate");
        string password   = ctx.GetString(GKey.Password);
        int    maxViewer  = ctx.GetInt(GKey.MaxViewer);
        string roomOption = ctx.GetString(GKey.RoomOption);  // G::keyRoomOption (k46e)

        var (ok, failCodes, plan) = (false, new List<int>(), (TournamentPlan?)null);

        var (valid, codes) = _tournament.ValidateRegist(
            playName, baseRule, moneyRule, playDate, password, maxViewer, roomOption,
            player.MemberNo, player.IsAdminId, out plan);

        failCodes = codes;

        if (valid && registFlag == TournamentConst.CheckFlagReg && plan != null)
        {
            long preMoney = player.GamMoney;
            long planMoney = TournamentTables.CalcPlanMoney(plan.GradeMoney);
            if (player.GamMoney < planMoney)
            {
                failCodes.Add(1010);
            }
            else
            {
                ok = await _tournament.RegisterAsync(plan, player);
                if (!ok) failCodes.Add(9999);
                else     await _money.SaveMoneyAsync(player, GameConst.EvtCodeTournamentPlan, -planMoney, preMoney);
            }
        }

        bool success = valid && (registFlag == TournamentConst.CheckFlagRegTemp || ok);
        await ctx.Caller.SendAsync(Cmd.TournamentRegist, new
        {
            result       = success ? 1 : 0,
            failCodeCnt  = failCodes.Count,
            failCode     = string.Join('\t', failCodes),
            gamMoney     = player.GamMoney,
        });

        if (ok && registFlag == TournamentConst.CheckFlagReg)
        {
            await ctx.Clients.OthersInGroup($"chanel_{player.ChannelId}")
                .SendAsync("tournament:list_changed", new { seqNo = plan!.SeqNo, changeType = "registered" });
        }
    }
}


/// <summary>


/// </summary>
public class TournamentJoinCommand : ICommand
{
    private readonly TournamentService    _tournament;
    private readonly TournamentRepository _tournRepo;
    private readonly GameMoneyService     _money;

    public TournamentJoinCommand(TournamentService t, TournamentRepository r, GameMoneyService m)
    { _tournament = t; _tournRepo = r; _money = m; }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        long   seqNo    = ctx.GetLong(Key.TournamentNo);
        string password = ctx.GetString(GKey.Password);
        if (!TournamentCommandGuards.RequireMember(ctx, player, nameof(TournamentJoinCommand)) || seqNo <= 0)
        {
            ctx.AbortConnectionWithReason($"{nameof(TournamentJoinCommand)} invalid parameter. memberNo={ctx.GetString(GKey.Pix)} seqNo={seqNo}");
            return;
        }

        var currentJoin = await _tournRepo.SelectJoinAsync(player.MemberNo);

        var (valid, failCode) = _tournament.ValidateJoin(
            seqNo, player.MemberNo, password, player.GamMoney, currentJoin);

        if (!valid)
        {
            await ctx.Caller.SendAsync(Cmd.TournamentJoin, new
            {
                result = 0, failCodeCnt = 1, failCode
            });
            return;
        }

        long preMoney = player.GamMoney;
        var joinMemberNo = string.IsNullOrWhiteSpace(currentJoin?.JoinMemberNo)
            ? "00"
            : currentJoin.JoinMemberNo;
        var (ok, count) = await _tournament.JoinAsync(seqNo, player, joinMemberNo);
        if (!ok)
        {
            await ctx.Caller.SendAsync(Cmd.TournamentJoin, new
            {
                result = 0, failCodeCnt = 1, failCode = 9999
            });
            return;
        }
        if (count != 1)
        {
            await ctx.Caller.SendAsync(Cmd.TournamentJoin, new
            {
                result = 0, failCodeCnt = 1, failCode = 2003
            });
            return;
        }

        await _money.SaveMoneyAsync(player, GameConst.EvtCodeTournamentJoin, player.GamMoney - preMoney, preMoney);
        await ctx.Caller.SendAsync(Cmd.TournamentJoin, new
        {
            result      = 1,
            failCodeCnt = 0,
            failCode    = "",
            gamMoney    = player.GamMoney,
        });
        await ctx.Clients.OthersInGroup($"chanel_{player.ChannelId}")
            .SendAsync("tournament:list_changed", new { seqNo, changeType = "joined" });
    }
}


/// <summary>


/// </summary>
public class TournamentJoinCancelCommand : ICommand
{
    private readonly TournamentService    _tournament;
    private readonly TournamentRepository _tournRepo;
    private readonly GameMoneyService     _money;

    public TournamentJoinCancelCommand(TournamentService t, TournamentRepository r, GameMoneyService m)
    { _tournament = t; _tournRepo = r; _money = m; }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        long seqNo = ctx.GetLong(Key.TournamentNo);
        if (!TournamentCommandGuards.RequireMember(ctx, player, nameof(TournamentJoinCancelCommand)) || seqNo == 0)
        {
            ctx.AbortConnectionWithReason($"{nameof(TournamentJoinCancelCommand)} invalid parameter. memberNo={ctx.GetString(GKey.Pix)} seqNo={seqNo}");
            return;
        }

        var currentJoin = await _tournRepo.SelectJoinAsync(player.MemberNo);
        var (valid, failCode) = _tournament.ValidateCancel(seqNo, currentJoin);
        if (!valid)
        {
            await ctx.Caller.SendAsync(Cmd.TournamentJoinCancel, new
            {
                result = 0, failCodeCnt = 1, failCode
            });
            return;
        }

        long preMoney = player.GamMoney;
        var (ok, count) = await _tournament.CancelJoinAsync(seqNo, player);
        if (!ok || count != 1)
        {
            await ctx.Caller.SendAsync(Cmd.TournamentJoinCancel, new
            {
                result = 0, failCodeCnt = 1, failCode = 3003
            });
            return;
        }

        await _money.SaveMoneyAsync(player, GameConst.EvtCodeTournamentJoinCancel, player.GamMoney - preMoney, preMoney);
        await ctx.Caller.SendAsync(Cmd.TournamentJoinCancel, new
        {
            result      = 1,
            failCodeCnt = 0,
            failCode    = "",
            gamMoney    = player.GamMoney,
        });
        await ctx.Clients.OthersInGroup($"chanel_{player.ChannelId}")
            .SendAsync("tournament:list_changed", new { seqNo, changeType = "cancelled" });
    }
}


public class TournamentDetailCommand : ICommand
{
    private readonly TournamentService _tournament;

    public TournamentDetailCommand(TournamentService t) => _tournament = t;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        long seqNo = ctx.GetLong(Key.TournamentNo);
        if (!TournamentCommandGuards.RequireMember(ctx, player, nameof(TournamentDetailCommand)) || seqNo == 0)
        {
            ctx.AbortConnectionWithReason($"{nameof(TournamentDetailCommand)} invalid parameter. memberNo={ctx.GetString(GKey.Pix)} seqNo={seqNo}");
            return;
        }

        var plan = _tournament.GetPlan(seqNo);
        if (plan == null)
        {
            await ctx.Caller.SendAsync(Cmd.TournamentDetail, new
            {
                result = 0, tournamentDetailCnt = 0,
                tournamentList   = (object?)null,
                tournamentDetail = Array.Empty<object>(),
                serverTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
            });
            return;
        }

        var detailMap = _tournament.GetDetails(seqNo);
        var details = detailMap?.Values.Select(d => new
        {
            subId      = d.SubId,
            roomId     = d.RoomId,
            memberNo1  = _tournament.GetPixForMemberNo(d.PlayerMemberNo[0]), memberNo2 = _tournament.GetPixForMemberNo(d.PlayerMemberNo[1]),
            memberNo3  = _tournament.GetPixForMemberNo(d.PlayerMemberNo[2]), memberNo4 = _tournament.GetPixForMemberNo(d.PlayerMemberNo[3]),
            joinMemberNo1 = d.JoinMemberNo[0], joinMemberNo2 = d.JoinMemberNo[1],
            joinMemberNo3 = d.JoinMemberNo[2], joinMemberNo4 = d.JoinMemberNo[3],
            gradeId1   = _tournament.GetPixForMemberNo(d.GradePlayerMemberNo[0]), gradeId2 = _tournament.GetPixForMemberNo(d.GradePlayerMemberNo[1]),
            gradeId3   = _tournament.GetPixForMemberNo(d.GradePlayerMemberNo[2]), gradeId4 = _tournament.GetPixForMemberNo(d.GradePlayerMemberNo[3]),
            startPlanDt = d.StartPlanDt == default ? "" : d.StartPlanDt.ToString("yyyy/MM/dd HH:mm:ss"),
            startDt     = d.StartDt     == default ? "" : d.StartDt.ToString("yyyy/MM/dd HH:mm:ss"),
            endDt       = d.EndDt       == default ? "" : d.EndDt.ToString("yyyy/MM/dd HH:mm:ss"),
        }).ToList() ?? new();


        var planDto = new
        {
            seqNo        = plan.SeqNo,
            playName     = plan.PlayName,
            playStatus   = plan.PlayStatus,
            playerNum    = plan.PlayerNum,
            maxPlayerNum = plan.MaxPlayerNum,
            hasPassword  = string.IsNullOrEmpty(plan.Password) ? 0 : 1,
            playMode     = plan.PlayMode,
            playNum      = plan.PlayNum,
            playTime     = plan.PlayTime,
            joinMoney    = plan.JoinMoney,
            gradeMoney1  = plan.GradeMoney[0],
            gradeMoney2  = plan.GradeMoney[1],
            gradeMoney3  = plan.GradeMoney[2],
            gradeMoney4  = plan.GradeMoney[3],
            playStartDt  = plan.PlayStartDt.ToString("yyyy/MM/dd HH:mm:ss"),
            playEndDt    = plan.PlayEndDt.ToString("yyyy/MM/dd HH:mm:ss"),
            playSchedule = plan.PlaySchedule,
            roomOption   = plan.RoomOption,
            maxViewer    = plan.MaxViewer,
            planMemberNo = _tournament.GetPixForMemberNo(plan.PlanMemberNo),
            resultMember1 = _tournament.GetPixForMemberNo(plan.ResultMemberNo[0]),
            resultMember2 = _tournament.GetPixForMemberNo(plan.ResultMemberNo[1]),
            resultMember3 = _tournament.GetPixForMemberNo(plan.ResultMemberNo[2]),
            resultMember4 = _tournament.GetPixForMemberNo(plan.ResultMemberNo[3]),
        };

        await ctx.Caller.SendAsync(Cmd.TournamentDetail, new
        {
            result               = 1,
            tournamentList       = planDto,
            tournamentDetailCnt  = details.Count,
            tournamentDetail     = details,
            serverTime           = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
        });
    }
}

internal static class TournamentCommandGuards
{
    public static bool RequireMember(CommandContext ctx, MajakPlayer player, string commandName)
    {
        string playerId = ctx.GetString(GKey.Pix);
        if (!ctx.Payload.ContainsKey(GKey.Pix)
            || string.IsNullOrEmpty(playerId)
            || (!string.Equals(playerId, player.MemberNo, StringComparison.Ordinal)
                && !string.Equals(playerId, player.Pix, StringComparison.Ordinal)))
        {
            ctx.AbortConnectionWithReason($"{commandName} invalid player id. playerId={playerId}");
            return false;
        }

        return true;
    }

    public static bool RequireMemberAndKeys(CommandContext ctx, MajakPlayer player, string commandName, params string[] requiredKeys)
    {
        if (!RequireMember(ctx, player, commandName)) return false;

        foreach (var key in requiredKeys)
        {
            if (!ctx.Payload.ContainsKey(key))
            {
                ctx.AbortConnectionWithReason($"{commandName} missing required key. key={key}");
                return false;
            }
        }

        return true;
    }
}


/// <summary>




/// </summary>
public class SetCustomItemCommand : ICommand
{
    private readonly ItemService _itemService;

    public SetCustomItemCommand(ItemService itemService) => _itemService = itemService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        int customId = ctx.GetInt(Key.CustomId);

        await _itemService.SetCustomItemAsync(player, customId);
    }
}
