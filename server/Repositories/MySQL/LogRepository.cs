using MajakServer.Models.Game;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// MySQL ログ DB 記録 — HMajLogDBObject 移植
/// </summary>
public class LogRepository
{
    private readonly LogDataContextFactory? _db;

    public LogRepository(LogDbContext db)
    {
    }

    public LogRepository(LogDbContext db, LogDataContextFactory dataContextFactory)
    {
        _db = dataContextFactory;
    }

    /// <summary>
    /// game_session_log + game_player_result_log INSERT。
    /// </summary>
    public virtual async Task InsertGameHistAsync(GameReport report)
        => await InsertGameHistWithIdAsync(report);

    public virtual async Task<ulong> InsertGameHistWithIdAsync(GameReport report)
    {
        await using var strategyDb = await RequireDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.Now;
                var session = new GameSessionLogEntity
                {
                    PlayedAt = now,
                    ChannelId = report.ChannelId,
                    RoomId = checked((uint)report.RoomId),
                    IsPrivate = report.PrivateYn,
                    RoomOption = report.RoomOption,
                    MoneyRate = report.MoneyRate,
                    MinimumMoney = report.MinMoney,
                    MaximumMoney = report.MaxMoney,
                };
                db.GameSessions.Add(session);
                await db.SaveChangesAsync();

                db.GamePlayerResults.AddRange(report.Users
                    .Where(user => user is not null && user.MemberNo != TournamentConst.NpcMemberNo)
                    .Select(user => new GamePlayerResultLogEntity
                    {
                        GameSessionId = session.GameSessionId,
                        PlayedAt = now,
                        MemberNo = MemberNoIds.Parse(user!.MemberNo),
                        WasConnected = user.Connected,
                        Ranking = checked((byte)user.Ranking),
                        Score = user.Score,
                        Point = user.SetPoint,
                        HadYakitori = user.Yakitori,
                        Chip = user.Chip,
                        MoneyBefore = user.PrevMoney,
                        LentMoneyBefore = user.PrevLent,
                        DealerFee = user.DealerFee,
                        MoneyChange = user.MoneyChange,
                        MoneyAfter = user.CurrMoney,
                        LentMoneyAfter = user.CurrLent,
                        IpAddress = user.IpAddress,
                        Gateway = user.Gateway,
                        MacAddress = user.MacAddr,
                    }));
                await db.SaveChangesAsync();

                await tx.CommitAsync();
                return session.GameSessionId;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// training_session_log + training_player_result_log INSERT。
    /// </summary>
    public virtual async Task InsertTrainingHistAsync(string channelId, int roomId,
        string roomOption, int playerCnt,
        (string MemberNo, int Point)[] players)
    {
        await using var strategyDb = await RequireDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var session = new TrainingSessionLogEntity
                {
                    PlayedAt = DateTime.Now,
                    ChannelId = channelId,
                    RoomId = checked((uint)roomId),
                    RoomOption = roomOption,
                    PlayerCount = checked((byte)playerCnt),
                };
                db.TrainingSessions.Add(session);
                await db.SaveChangesAsync();
                db.TrainingPlayerResults.AddRange(players.Select((player, index) => new TrainingPlayerResultLogEntity
                {
                    TrainingSessionId = session.TrainingSessionId,
                    SeatOrder = checked((byte)index),
                    MemberNo = MemberNoIds.TryParse(player.MemberNo, out var memberNoValue) ? memberNoValue : null,
                    Point = player.Point,
                }));
                await db.SaveChangesAsync();

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// weekly_reward_claim_log INSERT。reward_week は対象週の月曜日。
    /// </summary>
    public virtual async Task InsertWeeklyRewardHistAsync(string memberNo, int rewardId, int receiveStatus)
    {
        await using var db = await RequireDb().CreateAsync();
        var now = DateTime.Now;
        int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        db.WeeklyRewardClaims.Add(new WeeklyRewardClaimLogEntity
        {
            MemberNo = MemberNoIds.Parse(memberNo),
            RewardWeek = DateOnly.FromDateTime(now.Date.AddDays(-daysSinceMonday)),
            RewardId = checked((uint)rewardId),
            ReceiveStatus = checked((byte)receiveStatus),
            ClaimedAt = now,
        });
        await db.SaveChangesAsync();
    }

    public virtual async Task InsertGameMoneyHistAsync(
        string memberNo, string eventCode, long eventMoney,
        long preMoney, long afterMoney, string remoteAddr,
        string? eventTitle = null, string? orderNumber = null,
        string? gameId = null, bool isValid = true)
    {
        await using var db = await RequireDb().CreateAsync();
        db.MoneyTransactions.Add(new MoneyTransactionLogEntity
        {
            OccurredAt = DateTime.Now,
            MemberNo = MemberNoIds.Parse(memberNo),
            EventCode = eventCode,
            EventTitle = string.IsNullOrWhiteSpace(eventTitle) ? eventCode : eventTitle,
            GameId = string.IsNullOrWhiteSpace(gameId) ? GameConst.ServiceId : gameId,
            Amount = eventMoney,
            BalanceBefore = preMoney,
            BalanceAfter = afterMoney,
            IsValid = isValid,
            OrderNumber = string.IsNullOrWhiteSpace(orderNumber) ? null : orderNumber,
            RemoteAddress = remoteAddr ?? string.Empty,
        });
        await db.SaveChangesAsync();
    }

    public virtual async Task InsertYakuHistAsync(string memberNo, string gameId, int yaku)
    {
        await using var db = await RequireDb().CreateAsync();
        db.WinningYakuLogs.Add(new WinningYakuLogEntity
        {
            OccurredAt = DateTime.Now,
            MemberNo = MemberNoIds.Parse(memberNo),
            GameId = gameId,
            YakuCode = checked(yaku + 8),
        });
        await db.SaveChangesAsync();
    }

    public virtual async Task InsertItemPurchaseHistAsync(
        string memberNo,
        string itemCode,
        uint quantity,
        long unitPrice,
        byte purchaseChannel,
        string? externalUserNo = null,
        string? orderNumber = null)
    {
        await using var db = await RequireDb().CreateAsync();
        db.ItemPurchases.Add(new ItemPurchaseLogEntity
        {
            PurchasedAt = DateTime.Now,
            MemberNo = MemberNoIds.Parse(memberNo),
            ItemCode = itemCode,
            Quantity = quantity,
            UnitPrice = unitPrice,
            ExternalUserNo = externalUserNo,
            PurchaseChannel = purchaseChannel,
            OrderNumber = orderNumber,
        });
        await db.SaveChangesAsync();
    }

    public virtual async Task InsertPlayerLoginLogOncePerJapanDayAsync(
        string memberNo,
        byte eventType,
        string ipAddress,
        string userAgent)
    {
        await using var db = await RequireDb().CreateAsync();
        if (!MemberNoIds.TryParse(memberNo, out var memberNoValue)) return;

        var nowJst = DateTime.UtcNow.AddHours(9);
        var dayStart = nowJst.Date;
        var dayEnd = dayStart.AddDays(1);
        var exists = await db.PlayerLoginLogs.AnyAsync(log =>
            log.MemberNo == memberNoValue &&
            log.OccurredAt >= dayStart &&
            log.OccurredAt < dayEnd);
        if (exists) return;

        db.PlayerLoginLogs.Add(new PlayerLoginLogEntity
        {
            OccurredAt = nowJst,
            MemberNo = memberNoValue,
            EventType = eventType,
            IpAddress = Truncate(ipAddress ?? string.Empty, 45),
            UserAgent = Truncate(userAgent ?? string.Empty, 200),
        });
        await db.SaveChangesAsync();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private LogDataContextFactory RequireDb()
        => _db ?? throw new InvalidOperationException("MySQL LogDataContextFactory is not configured.");
}
