using MajakServer.Repositories.MySQL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MajakServer.Repositories.MySQL;

public sealed class LogDataContext : DbContext
{
    public LogDataContext(DbContextOptions<LogDataContext> options)
        : base(options)
    {
    }

    public DbSet<GameSessionLogEntity> GameSessions => Set<GameSessionLogEntity>();
    public DbSet<GamePlayerResultLogEntity> GamePlayerResults => Set<GamePlayerResultLogEntity>();
    public DbSet<TrainingSessionLogEntity> TrainingSessions => Set<TrainingSessionLogEntity>();
    public DbSet<TrainingPlayerResultLogEntity> TrainingPlayerResults => Set<TrainingPlayerResultLogEntity>();
    public DbSet<WeeklyRewardClaimLogEntity> WeeklyRewardClaims => Set<WeeklyRewardClaimLogEntity>();
    public DbSet<MoneyTransactionLogEntity> MoneyTransactions => Set<MoneyTransactionLogEntity>();
    public DbSet<WinningYakuLogEntity> WinningYakuLogs => Set<WinningYakuLogEntity>();
    public DbSet<ItemPurchaseLogEntity> ItemPurchases => Set<ItemPurchaseLogEntity>();
    public DbSet<PlayerLoginLogEntity> PlayerLoginLogs => Set<PlayerLoginLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSessionLogEntity>(entity =>
        {
            entity.ToTable("game_session_log");
            entity.HasKey(x => x.GameSessionId);
            entity.Property(x => x.GameSessionId).HasColumnName("game_session_id").ValueGeneratedOnAdd();
            entity.Property(x => x.PlayedAt).HasColumnName("played_at");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(30);
            entity.Property(x => x.RoomId).HasColumnName("room_id");
            entity.Property(x => x.IsPrivate).HasColumnName("is_private");
            entity.Property(x => x.RoomOption).HasColumnName("room_option").HasMaxLength(200);
            entity.Property(x => x.MoneyRate).HasColumnName("money_rate");
            entity.Property(x => x.MinimumMoney).HasColumnName("minimum_money");
            entity.Property(x => x.MaximumMoney).HasColumnName("maximum_money");
            entity.Property(x => x.MinimumClass).HasColumnName("minimum_class");
            entity.Property(x => x.MaximumClass).HasColumnName("maximum_class");
            entity.Property(x => x.CupId).HasColumnName("cup_id");
            entity.Property(x => x.RuleId).HasColumnName("rule_id");
            entity.Property(x => x.CupSequence).HasColumnName("cup_sequence");
            entity.Property(x => x.UsedTicket).HasColumnName("used_ticket");
            entity.Property(x => x.CupRule).HasColumnName("cup_rule");
        });

        modelBuilder.Entity<GamePlayerResultLogEntity>(entity =>
        {
            entity.ToTable("game_player_result_log");
            entity.HasKey(x => x.GamePlayerResultId);
            entity.Property(x => x.GamePlayerResultId).HasColumnName("game_player_result_id").ValueGeneratedOnAdd();
            entity.Property(x => x.GameSessionId).HasColumnName("game_session_id");
            entity.Property(x => x.PlayedAt).HasColumnName("played_at");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.WasConnected).HasColumnName("was_connected");
            entity.Property(x => x.Ranking).HasColumnName("ranking");
            entity.Property(x => x.Score).HasColumnName("score");
            entity.Property(x => x.Point).HasColumnName("point");
            entity.Property(x => x.HadYakitori).HasColumnName("had_yakitori");
            entity.Property(x => x.Chip).HasColumnName("chip");
            entity.Property(x => x.MoneyBefore).HasColumnName("money_before");
            entity.Property(x => x.LentMoneyBefore).HasColumnName("lent_money_before");
            entity.Property(x => x.DealerFee).HasColumnName("dealer_fee");
            entity.Property(x => x.MoneyChange).HasColumnName("money_change");
            entity.Property(x => x.MoneyAfter).HasColumnName("money_after");
            entity.Property(x => x.LentMoneyAfter).HasColumnName("lent_money_after");
            entity.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(x => x.Gateway).HasColumnName("gateway").HasMaxLength(45);
            entity.Property(x => x.MacAddress).HasColumnName("mac_address").HasMaxLength(17);
            entity.Property(x => x.PreviousTicket).HasColumnName("previous_ticket");
            entity.Property(x => x.ReturnedTicket).HasColumnName("returned_ticket");
            entity.Property(x => x.PreviousClass).HasColumnName("previous_class");
            entity.Property(x => x.CurrentClass).HasColumnName("current_class");
            entity.Property(x => x.CurrentTicket).HasColumnName("current_ticket");
        });

        modelBuilder.Entity<TrainingSessionLogEntity>(entity =>
        {
            entity.ToTable("training_session_log");
            entity.HasKey(x => x.TrainingSessionId);
            entity.Property(x => x.TrainingSessionId).HasColumnName("training_session_id").ValueGeneratedOnAdd();
            entity.Property(x => x.PlayedAt).HasColumnName("played_at");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(30);
            entity.Property(x => x.RoomId).HasColumnName("room_id");
            entity.Property(x => x.RoomOption).HasColumnName("room_option").HasMaxLength(200);
            entity.Property(x => x.PlayerCount).HasColumnName("player_count");
        });

        modelBuilder.Entity<TrainingPlayerResultLogEntity>(entity =>
        {
            entity.ToTable("training_player_result_log");
            entity.HasKey(x => x.TrainingPlayerResultId);
            entity.Property(x => x.TrainingPlayerResultId).HasColumnName("training_player_result_id").ValueGeneratedOnAdd();
            entity.Property(x => x.TrainingSessionId).HasColumnName("training_session_id");
            entity.Property(x => x.SeatOrder).HasColumnName("seat_order");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.Point).HasColumnName("point");
        });

        modelBuilder.Entity<WeeklyRewardClaimLogEntity>(entity =>
        {
            entity.ToTable("weekly_reward_claim_log");
            entity.HasKey(x => x.WeeklyRewardClaimId);
            entity.Property(x => x.WeeklyRewardClaimId).HasColumnName("weekly_reward_claim_id").ValueGeneratedOnAdd();
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.RewardWeek).HasColumnName("reward_week");
            entity.Property(x => x.RewardId).HasColumnName("reward_id");
            entity.Property(x => x.ReceiveStatus).HasColumnName("receive_status");
            entity.Property(x => x.ClaimedAt).HasColumnName("claimed_at");
        });

        modelBuilder.Entity<MoneyTransactionLogEntity>(entity =>
        {
            entity.ToTable("money_transaction_log");
            entity.HasKey(x => x.MoneyTransactionId);
            entity.Property(x => x.MoneyTransactionId).HasColumnName("money_transaction_id").ValueGeneratedOnAdd();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(32);
            entity.Property(x => x.EventTitle).HasColumnName("event_title").HasMaxLength(100);
            entity.Property(x => x.GameId).HasColumnName("game_id").HasMaxLength(20);
            entity.Property(x => x.Amount).HasColumnName("amount");
            entity.Property(x => x.BalanceBefore).HasColumnName("balance_before");
            entity.Property(x => x.BalanceAfter).HasColumnName("balance_after");
            entity.Property(x => x.IsValid).HasColumnName("is_valid");
            entity.Property(x => x.OrderNumber).HasColumnName("order_number").HasMaxLength(64);
            entity.Property(x => x.AdditionalInfo).HasColumnName("additional_info").HasMaxLength(100);
            entity.Property(x => x.BillingOrderNumber).HasColumnName("billing_order_number").HasMaxLength(20);
            entity.Property(x => x.UnitCount).HasColumnName("unit_count");
            entity.Property(x => x.RemoteAddress).HasColumnName("remote_address").HasMaxLength(45);
        });

        modelBuilder.Entity<WinningYakuLogEntity>(entity =>
        {
            entity.ToTable("winning_yaku_log");
            entity.HasKey(x => x.WinningYakuLogId);
            entity.Property(x => x.WinningYakuLogId).HasColumnName("winning_yaku_log_id").ValueGeneratedOnAdd();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.GameId).HasColumnName("game_id").HasMaxLength(20);
            entity.Property(x => x.YakuCode).HasColumnName("yaku_code");
        });

        modelBuilder.Entity<ItemPurchaseLogEntity>(entity =>
        {
            entity.ToTable("item_purchase_log");
            entity.HasKey(x => x.ItemPurchaseId);
            entity.Property(x => x.ItemPurchaseId).HasColumnName("item_purchase_id").ValueGeneratedOnAdd();
            entity.Property(x => x.PurchasedAt).HasColumnName("purchased_at");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.ItemCode).HasColumnName("item_code").HasMaxLength(64);
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price");
            entity.Property(x => x.ExternalUserNo).HasColumnName("external_user_no").HasMaxLength(64);
            entity.Property(x => x.PurchaseChannel).HasColumnName("purchase_channel");
            entity.Property(x => x.OrderNumber).HasColumnName("order_number").HasMaxLength(64);
        });

        modelBuilder.Entity<PlayerLoginLogEntity>(entity =>
        {
            entity.ToTable("player_login_log");
            entity.HasKey(x => x.LoginLogId);
            entity.Property(x => x.LoginLogId).HasColumnName("login_log_id").ValueGeneratedOnAdd();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.EventType).HasColumnName("event_type");
            entity.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(200);
        });
    }
}

public sealed class LogDataContextFactory
{
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 0));
    private readonly LogDbContext _connections;

    public LogDataContextFactory(LogDbContext connections)
    {
        _connections = connections;
    }

    public async Task<LogDataContext> CreateAsync()
    {
        var connectionString = await _connections.GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder<LogDataContext>()
            .UseMySql(connectionString, ServerVersion, options => options.EnableRetryOnFailure())
            .Options;
        return new LogDataContext(options);
    }
}