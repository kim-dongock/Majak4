using MajakServer.Repositories.MySQL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MajakServer.Repositories.MySQL;

public sealed class GameDataContext : DbContext
{
    public GameDataContext(DbContextOptions<GameDataContext> options)
        : base(options)
    {
    }

    public DbSet<PlayerAccountEntity> PlayerAccounts => Set<PlayerAccountEntity>();
    public DbSet<PlayerWalletEntity> PlayerWallets => Set<PlayerWalletEntity>();
    public DbSet<PlayerAvatarInventoryEntity> PlayerAvatarInventory => Set<PlayerAvatarInventoryEntity>();
    public DbSet<PlayerProfileEntity> PlayerProfiles => Set<PlayerProfileEntity>();
    public DbSet<PlayerModeStatsEntity> PlayerModeStats => Set<PlayerModeStatsEntity>();
    public DbSet<PlayerHighClassSummaryEntity> PlayerHighClassSummaries => Set<PlayerHighClassSummaryEntity>();
    public DbSet<PlayerHighClassYakuEntity> PlayerHighClassYaku => Set<PlayerHighClassYakuEntity>();
    public DbSet<CupPlayerRatingEntity> CupPlayerRatings => Set<CupPlayerRatingEntity>();
    public DbSet<TransactionCodeMasterEntity> TransactionCodeMasters => Set<TransactionCodeMasterEntity>();
    public DbSet<MemorialShopMasterEntity> MemorialShopMasters => Set<MemorialShopMasterEntity>();
    public DbSet<TitleMasterEntity> TitleMasters => Set<TitleMasterEntity>();
    public DbSet<DailyMissionMasterEntity> DailyMissionMasters => Set<DailyMissionMasterEntity>();
    public DbSet<WeeklyRewardMasterEntity> WeeklyRewardMasters => Set<WeeklyRewardMasterEntity>();
    public DbSet<GradeRankScheduleEntity> GradeRankSchedules => Set<GradeRankScheduleEntity>();
    public DbSet<ChannelMasterEntity> ChannelMasters => Set<ChannelMasterEntity>();
    public DbSet<ChannelRuntimeEntity> ChannelRuntimes => Set<ChannelRuntimeEntity>();
    public DbSet<RuleMasterEntity> RuleMasters => Set<RuleMasterEntity>();
    public DbSet<CupMasterEntity> CupMasters => Set<CupMasterEntity>();
    public DbSet<CupChannelEntity> CupChannels => Set<CupChannelEntity>();
    public DbSet<TournamentPlanMasterEntity> TournamentPlanMasters => Set<TournamentPlanMasterEntity>();
    public DbSet<EventMasterEntity> EventMasters => Set<EventMasterEntity>();
    public DbSet<EventUserEntity> EventUsers => Set<EventUserEntity>();
    public DbSet<GameAdminMemberEntity> GameAdminMembers => Set<GameAdminMemberEntity>();
    public DbSet<TournamentSessionEntity> TournamentSessions => Set<TournamentSessionEntity>();
    public DbSet<TournamentRoomEntity> TournamentRooms => Set<TournamentRoomEntity>();
    public DbSet<TournamentLimitEntity> TournamentLimits => Set<TournamentLimitEntity>();
    public DbSet<TournamentParticipantEntity> TournamentParticipants => Set<TournamentParticipantEntity>();
    public DbSet<PlayerPresentEntity> PlayerPresents => Set<PlayerPresentEntity>();
    public DbSet<BillingItemMasterEntity> BillingItemMasters => Set<BillingItemMasterEntity>();
    public DbSet<CustomItemMasterEntity> CustomItemMasters => Set<CustomItemMasterEntity>();
    public DbSet<CustomShopMasterEntity> CustomShopMasters => Set<CustomShopMasterEntity>();
    public DbSet<CustomItemSetEntity> CustomItemSets => Set<CustomItemSetEntity>();
    public DbSet<PlayerCustomItemEntity> PlayerCustomItems => Set<PlayerCustomItemEntity>();
    public DbSet<PlayerFunctionItemEntity> PlayerFunctionItems => Set<PlayerFunctionItemEntity>();
    public DbSet<PlayerTitleEntity> PlayerTitles => Set<PlayerTitleEntity>();
    public DbSet<PlayerDailyMissionEntity> PlayerDailyMissions => Set<PlayerDailyMissionEntity>();
    public DbSet<PlayerDailyMissionHistoryEntity> PlayerDailyMissionHistory => Set<PlayerDailyMissionHistoryEntity>();
    public DbSet<PlayerWeeklyRewardEntity> PlayerWeeklyRewards => Set<PlayerWeeklyRewardEntity>();
    public DbSet<PlayerGradeRankEntity> PlayerGradeRanks => Set<PlayerGradeRankEntity>();
    public DbSet<TournamentPlayerRatingEntity> TournamentPlayerRatings => Set<TournamentPlayerRatingEntity>();
    public DbSet<PlayerSkinEntity> PlayerSkins => Set<PlayerSkinEntity>();
    public DbSet<PlayerShopEntity> PlayerShops => Set<PlayerShopEntity>();
    public DbSet<EventGiftMasterEntity> EventGiftMasters => Set<EventGiftMasterEntity>();
    public DbSet<SerialExchangeItemEntity> SerialExchangeItems => Set<SerialExchangeItemEntity>();
    public DbSet<SerialCouponEntity> SerialCoupons => Set<SerialCouponEntity>();
    public DbSet<GameClearCountEntity> GameClearCounts => Set<GameClearCountEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerAccountEntity>(entity =>
        {
            entity.ToTable("player_account");
            entity.HasKey(x => x.MemberNo);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100);
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
            entity.Property(x => x.GoogleSub).HasColumnName("google_sub").HasMaxLength(64);
            entity.Property(x => x.SexCode).HasColumnName("sex_code").HasMaxLength(1);
            entity.Property(x => x.BirthYear).HasColumnName("birth_year");
            entity.Property(x => x.AvatarId).HasColumnName("avatar_id").HasMaxLength(255);
            entity.Property(x => x.TermsAgreedAt).HasColumnName("terms_agreed_at");
            entity.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            entity.Property(x => x.ApprovedBy).HasColumnName("approved_by");
            entity.Property(x => x.RejectReason).HasColumnName("reject_reason").HasMaxLength(200);
            entity.Property(x => x.AccountStatus).HasColumnName("account_status");
            entity.Property(x => x.SourceEnvironment).HasColumnName("source_environment").HasMaxLength(16);
            entity.Property(x => x.FirstLoginAt).HasColumnName("first_login_at");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.GoogleSub).IsUnique();
        });

        modelBuilder.Entity<PlayerWalletEntity>(entity =>
        {
            entity.ToTable("player_wallet");
            entity.HasKey(x => x.MemberNo);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.GameMoney).HasColumnName("game_money");
            entity.Property(x => x.PendingGameMoney).HasColumnName("pending_game_money");
            entity.Property(x => x.EarnedGameMoney).HasColumnName("earned_game_money");
            entity.Property(x => x.LoanedGameMoney).HasColumnName("loaned_game_money");
            entity.Property(x => x.GemCount).HasColumnName("gem_count");
            entity.Property(x => x.CashCount).HasColumnName("cash_count");
            entity.Property(x => x.PaidCashCount).HasColumnName("paid_cash_count");
            entity.Property(x => x.FreeCashCount).HasColumnName("free_cash_count");
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerAvatarInventoryEntity>(entity =>
        {
            entity.ToTable("player_avatar_inventory");
            entity.HasKey(x => x.InventoryId);
            entity.Property(x => x.InventoryId).HasColumnName("inventory_id").ValueGeneratedOnAdd();
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.AvatarCode).HasColumnName("avatar_code").HasMaxLength(32);
            entity.Property(x => x.CostMoney).HasColumnName("cost_money");
            entity.Property(x => x.CostGem).HasColumnName("cost_gem");
            entity.Property(x => x.AcquiredAt).HasColumnName("acquired_at");
        });

        modelBuilder.Entity<PlayerProfileEntity>(entity =>
        {
            entity.ToTable("player_profile");
            entity.HasKey(x => x.MemberNo);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.CommonRating).HasColumnName("common_rating");
            entity.Property(x => x.Experience).HasColumnName("experience");
            entity.Property(x => x.BestMoneyLevel).HasColumnName("best_money_level");
            entity.Property(x => x.ConsecutiveWinLoss).HasColumnName("consecutive_win_loss");
            entity.Property(x => x.AllInCount).HasColumnName("all_in_count");
            entity.Property(x => x.LastAllInAt).HasColumnName("last_all_in_at");
            entity.Property(x => x.TrickTitleCode).HasColumnName("trick_title_code").HasMaxLength(32);
            entity.Property(x => x.MajakTitleCode).HasColumnName("majak_title_code").HasMaxLength(32);
            entity.Property(x => x.EventOpenFlag).HasColumnName("event_open_flag").HasMaxLength(1);
            entity.Property(x => x.WeeklyPoint).HasColumnName("weekly_point");
            entity.Property(x => x.WeeklyTargetDate).HasColumnName("weekly_target_date");
            entity.Property(x => x.JoinedAt).HasColumnName("joined_at");
            entity.Property(x => x.LastPlayedAt).HasColumnName("last_played_at");
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerModeStatsEntity>(entity =>
        {
            entity.ToTable("player_mode_stats");
            entity.HasKey(x => new { x.MemberNo, x.ModeCode });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.ModeCode).HasColumnName("mode_code").HasMaxLength(20);
            entity.Property(x => x.Rating).HasColumnName("rating");
            entity.Property(x => x.MatchCount).HasColumnName("match_count");
            entity.Property(x => x.WinCount).HasColumnName("win_count");
            entity.Property(x => x.DefeatCount).HasColumnName("defeat_count");
            entity.Property(x => x.DrawCount).HasColumnName("draw_count");
            entity.Property(x => x.FirstCount).HasColumnName("first_count");
            entity.Property(x => x.SecondCount).HasColumnName("second_count");
            entity.Property(x => x.ThirdCount).HasColumnName("third_count");
            entity.Property(x => x.FourthCount).HasColumnName("fourth_count");
            entity.Property(x => x.TurnCount).HasColumnName("turn_count");
            entity.Property(x => x.DealerCount).HasColumnName("dealer_count");
            entity.Property(x => x.PointSum).HasColumnName("point_sum");
            entity.Property(x => x.RoundCount).HasColumnName("round_count");
            entity.Property(x => x.WinHandCount).HasColumnName("win_hand_count");
            entity.Property(x => x.WinHandPoints).HasColumnName("win_hand_points");
            entity.Property(x => x.DealInCount).HasColumnName("deal_in_count");
            entity.Property(x => x.DealInPoints).HasColumnName("deal_in_points");
            entity.Property(x => x.RiichiCount).HasColumnName("riichi_count");
            entity.Property(x => x.MeldCount).HasColumnName("meld_count");
            entity.Property(x => x.TipPoint).HasColumnName("tip_point");
            entity.Property(x => x.TipMatchCount).HasColumnName("tip_match_count");
            entity.Property(x => x.BustCount).HasColumnName("bust_count");
            entity.Property(x => x.BustOtherCount).HasColumnName("bust_other_count");
            entity.Property(x => x.DoraCount).HasColumnName("dora_count");
            entity.Property(x => x.UraDoraCount).HasColumnName("ura_dora_count");
            entity.Property(x => x.RiichiWinCount).HasColumnName("riichi_win_count");
            entity.Property(x => x.DisconnectCount).HasColumnName("disconnect_count");
            entity.Property(x => x.LastDisconnectAt).HasColumnName("last_disconnect_at");
            entity.Property(x => x.LastChannelId).HasColumnName("last_channel_id").HasMaxLength(30);
            entity.Property(x => x.GradeLevel).HasColumnName("grade_level");
            entity.Property(x => x.GradePoint).HasColumnName("grade_point");
            entity.Property(x => x.ExtraCount).HasColumnName("extra_count");
            entity.Property(x => x.LastExtraAt).HasColumnName("last_extra_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerHighClassSummaryEntity>(entity =>
        {
            entity.ToTable("player_high_class_summary");
            entity.HasKey(x => x.MemberNo);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.ScoreMax).HasColumnName("score_max");
            entity.Property(x => x.ScoreMin).HasColumnName("score_min");
            entity.Property(x => x.MoneyMax).HasColumnName("money_max");
            entity.Property(x => x.MoneyMin).HasColumnName("money_min");
            entity.Property(x => x.WinHandDoraMax).HasColumnName("win_hand_dora_max");
            entity.Property(x => x.ConsecutiveTopMax).HasColumnName("consecutive_top_max");
            entity.Property(x => x.ConsecutiveTopCurrent).HasColumnName("consecutive_top_current");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerHighClassYakuEntity>(entity =>
        {
            entity.ToTable("player_high_class_yaku");
            entity.HasKey(x => new { x.MemberNo, x.YakuId });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.YakuId).HasColumnName("yaku_id");
            entity.Property(x => x.Count).HasColumnName("count");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<CupPlayerRatingEntity>(entity =>
        {
            entity.ToTable("cup_player_rating");
            entity.HasKey(x => new { x.CupId, x.MemberNo });
            entity.Property(x => x.CupId).HasColumnName("cup_id");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.CupPoint).HasColumnName("cup_point");
            entity.Property(x => x.MatchCount).HasColumnName("match_count");
            entity.Property(x => x.JoinedAt).HasColumnName("joined_at");
            entity.Property(x => x.LastPlayedAt).HasColumnName("last_played_at");
        });

        modelBuilder.Entity<TitleMasterEntity>(entity =>
        {
            entity.ToTable("title_master");
            entity.HasKey(x => x.TitleId);
            entity.Property(x => x.TitleId).HasColumnName("title_id").HasMaxLength(10);
            entity.Property(x => x.TitleName).HasColumnName("title_name").HasMaxLength(30);
        });

        modelBuilder.Entity<DailyMissionMasterEntity>(entity =>
        {
            entity.ToTable("daily_mission_master");
            entity.HasKey(x => x.MissionId);
            entity.Property(x => x.MissionId).HasColumnName("mission_id");
            entity.Property(x => x.ConditionType).HasColumnName("condition_type");
            entity.Property(x => x.ConditionCount).HasColumnName("condition_count");
            entity.Property(x => x.Point).HasColumnName("point");
        });

        modelBuilder.Entity<WeeklyRewardMasterEntity>(entity =>
        {
            entity.ToTable("weekly_reward_master");
            entity.HasKey(x => x.RewardId);
            entity.Property(x => x.RewardId).HasColumnName("reward_id");
            entity.Property(x => x.RewardType).HasColumnName("reward_type");
            entity.Property(x => x.RewardCount).HasColumnName("reward_count");
            entity.Property(x => x.RequiredPoint).HasColumnName("required_point");
        });

        modelBuilder.Entity<GradeRankScheduleEntity>(entity =>
        {
            entity.ToTable("grade_rank_schedule");
            entity.HasKey(x => x.RankDate);
            entity.Property(x => x.RankDate).HasColumnName("rank_date").HasColumnType("date");
            entity.Property(x => x.BatchFlag).HasColumnName("batch_flag");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ChannelMasterEntity>(entity =>
        {
            entity.ToTable("channel_master");
            entity.HasKey(x => x.ChannelId);
            entity.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(30);
            entity.Property(x => x.GameId).HasColumnName("game_id").HasMaxLength(20);
            entity.Property(x => x.SubId).HasColumnName("sub_id").HasMaxLength(10);
            entity.Property(x => x.ChannelName).HasColumnName("channel_name").HasMaxLength(100);
            entity.Property(x => x.MaxMember).HasColumnName("max_member");
            entity.Property(x => x.MaxRoom).HasColumnName("max_room");
            entity.Property(x => x.UnitMoney).HasColumnName("unit_money");
            entity.Property(x => x.ChannelType).HasColumnName("channel_type");
            entity.Property(x => x.Environment).HasColumnName("env").HasMaxLength(10);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.ServerUrl).HasColumnName("server_url").HasMaxLength(255);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ChannelRuntimeEntity>(entity =>
        {
            entity.ToTable("channel_runtime");
            entity.HasKey(x => x.ChannelId);
            entity.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(30);
            entity.Property(x => x.GameId).HasColumnName("game_id").HasMaxLength(10);
            entity.Property(x => x.SubId).HasColumnName("sub_id").HasMaxLength(5);
            entity.Property(x => x.GoService).HasColumnName("go_service").HasMaxLength(30);
            entity.Property(x => x.ServerIp).HasColumnName("server_ip").HasMaxLength(50);
            entity.Property(x => x.ServerPort).HasColumnName("server_port");
            entity.Property(x => x.GamePort).HasColumnName("game_port");
            entity.Property(x => x.QueryPort).HasColumnName("query_port");
            entity.Property(x => x.ChannelName).HasColumnName("channel_name").HasMaxLength(50);
            entity.Property(x => x.MaxMember).HasColumnName("max_member");
            entity.Property(x => x.MaxRoom).HasColumnName("max_room");
            entity.Property(x => x.UnitMoney).HasColumnName("unit_money");
            entity.Property(x => x.MemberCount).HasColumnName("member_count");
            entity.Property(x => x.UsedRoom).HasColumnName("used_room");
            entity.Property(x => x.ItemYesCount).HasColumnName("item_yes_count");
            entity.Property(x => x.ItemNoCount).HasColumnName("item_no_count");
            entity.Property(x => x.MemberMale).HasColumnName("member_male");
            entity.Property(x => x.MemberFemale).HasColumnName("member_female");
            entity.Property(x => x.MachineName).HasColumnName("machine_name").HasMaxLength(20);
            entity.Property(x => x.ChannelServerVersion).HasColumnName("channel_server_version");
            entity.Property(x => x.RoomServerVersion).HasColumnName("room_server_version");
            entity.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            entity.Property(x => x.ZoneId).HasColumnName("zone_id").HasMaxLength(3);
            entity.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(1);
            entity.Property(x => x.ServiceMask).HasColumnName("service_mask");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(128);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TransactionCodeMasterEntity>(entity =>
        {
            entity.ToTable("transaction_code_master");
            entity.HasKey(x => x.TransactionCode);
            entity.Property(x => x.TransactionCode).HasColumnName("transaction_code").HasMaxLength(20);
            entity.Property(x => x.CodeTitle).HasColumnName("code_title").HasMaxLength(30);
            entity.Property(x => x.IsHistoryEnabled).HasColumnName("is_history_enabled");
            entity.Property(x => x.IsCumulative).HasColumnName("is_cumulative");
            entity.Property(x => x.OpenStatus).HasColumnName("open_status").HasMaxLength(1);
            entity.Property(x => x.StartDate).HasColumnName("start_date");
            entity.Property(x => x.Content).HasColumnName("content").HasMaxLength(80);
            entity.Property(x => x.ServiceCode).HasColumnName("service_code").HasMaxLength(10);
            entity.Property(x => x.ServiceName).HasColumnName("service_name").HasMaxLength(30);
            entity.Property(x => x.IsServiceEnabled).HasColumnName("is_service_enabled");
            entity.Property(x => x.GameId).HasColumnName("game_id").HasMaxLength(10);
            entity.Property(x => x.RegistrantName).HasColumnName("registrant_name").HasMaxLength(20);
            entity.Property(x => x.PlannerName).HasColumnName("planner_name").HasMaxLength(20);
            entity.Property(x => x.DeveloperName).HasColumnName("developer_name").HasMaxLength(20);
            entity.Property(x => x.DirectionCode).HasColumnName("direction_code").HasMaxLength(1);
            entity.Property(x => x.AvatarCode).HasColumnName("avatar_code").HasMaxLength(10);
        });

        modelBuilder.Entity<MemorialShopMasterEntity>(entity =>
        {
            entity.ToTable("memorial_shop_master");
            entity.HasKey(x => x.ShopId);
            entity.Property(x => x.ShopId).HasColumnName("shop_id");
            entity.Property(x => x.ShopName).HasColumnName("shop_name").HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<RuleMasterEntity>(entity =>
        {
            entity.ToTable("rule_master");
            entity.HasKey(x => x.RuleId);
            entity.Property(x => x.RuleId).HasColumnName("rule_id");
            entity.Property(x => x.JudgementType).HasColumnName("judgement_type");
            entity.Property(x => x.RoomOption).HasColumnName("room_option").HasMaxLength(13);
            entity.Property(x => x.NormalYakuCondition).HasColumnName("normal_yaku_condition").HasMaxLength(28);
            entity.Property(x => x.YakumanCondition).HasColumnName("yakuman_condition").HasMaxLength(15);
            entity.Property(x => x.RuleName).HasColumnName("rule_name").HasMaxLength(100);
            entity.Property(x => x.RuleDetail).HasColumnName("rule_detail").HasMaxLength(2000);
            entity.Property(x => x.EventSumType).HasColumnName("evt_sum_type");
        });

        modelBuilder.Entity<CupMasterEntity>(entity =>
        {
            entity.ToTable("cup_master");
            entity.HasKey(x => x.CupId);
            entity.Property(x => x.CupId).HasColumnName("cup_id");
            entity.Property(x => x.CupName).HasColumnName("cup_name").HasMaxLength(40);
            entity.Property(x => x.ShortCupName).HasColumnName("short_cup_name").HasMaxLength(12);
            entity.Property(x => x.RuleId).HasColumnName("rule_id");
            entity.Property(x => x.ConditionMatchCount).HasColumnName("condition_match_count");
            entity.Property(x => x.ConditionRegular).HasColumnName("condition_regular");
            entity.Property(x => x.StartAt).HasColumnName("start_at");
            entity.Property(x => x.EndAt).HasColumnName("end_at");
            entity.Property(x => x.NicknameStartAt).HasColumnName("nickname_start_at");
            entity.Property(x => x.NicknameEndAt).HasColumnName("nickname_end_at");
            entity.Property(x => x.Prize).HasColumnName("prize").HasMaxLength(1000);
            entity.Property(x => x.Detail).HasColumnName("detail").HasMaxLength(1000);
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<CupChannelEntity>(entity =>
        {
            entity.ToTable("cup_channel");
            entity.HasKey(x => new { x.CupId, x.ChannelId });
            entity.Property(x => x.CupId).HasColumnName("cup_id");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(30);
        });

        modelBuilder.Entity<TournamentPlanMasterEntity>(entity =>
        {
            entity.ToTable("tournament_plan");
            entity.HasKey(x => new { x.CupId, x.Sequence });
            entity.Property(x => x.CupId).HasColumnName("cup_id");
            entity.Property(x => x.Sequence).HasColumnName("seq");
            entity.Property(x => x.CupName).HasColumnName("cup_name").HasMaxLength(40);
            entity.Property(x => x.IsFinal).HasColumnName("is_final");
            entity.Property(x => x.StartAt).HasColumnName("start_at");
            entity.Property(x => x.EndAt).HasColumnName("end_at");
            entity.Property(x => x.MinLevel).HasColumnName("min_level");
            entity.Property(x => x.MaxLevel).HasColumnName("max_level");
            entity.Property(x => x.UnitMoney).HasColumnName("unit_money");
            entity.Property(x => x.MaxMatchCount).HasColumnName("max_match_count");
            entity.Property(x => x.MinMatchCount).HasColumnName("min_match_count");
            entity.Property(x => x.Prize).HasColumnName("prize").HasMaxLength(1000);
            entity.Property(x => x.Detail).HasColumnName("detail").HasMaxLength(1000);
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.AdminComment).HasColumnName("admin_comment").HasMaxLength(255);
            entity.Property(x => x.IsValid).HasColumnName("is_valid");
            entity.Property(x => x.RuleId).HasColumnName("rule_id");
            entity.Property(x => x.NoticeUrl).HasColumnName("notice_url").HasMaxLength(255);
            entity.Property(x => x.BannerUrl).HasColumnName("banner_url").HasMaxLength(255);
            entity.Property(x => x.BillingStatus).HasColumnName("billing_status");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<EventMasterEntity>(entity =>
        {
            entity.ToTable("event_master");
            entity.HasKey(x => new { x.EventCode, x.EventNo });
            entity.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(10);
            entity.Property(x => x.EventNo).HasColumnName("event_no");
            entity.Property(x => x.EventName).HasColumnName("event_name").HasMaxLength(120);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(x => x.ServiceId).HasColumnName("service_id").HasMaxLength(20);
            entity.Property(x => x.TableInfo).HasColumnName("table_info").HasMaxLength(100);
            entity.Property(x => x.StartsAt).HasColumnName("starts_at");
            entity.Property(x => x.EndsAt).HasColumnName("ends_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<EventUserEntity>(entity =>
        {
            entity.ToTable("event_user");
            entity.HasKey(x => new { x.EventCode, x.EventNo, x.MemberNo });
            entity.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(10);
            entity.Property(x => x.EventNo).HasColumnName("event_no");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.TotalEarnedPoint).HasColumnName("total_earned_point");
            entity.Property(x => x.DailyEarnedPoint).HasColumnName("daily_earned_point");
            entity.Property(x => x.TotalUsedPoint).HasColumnName("total_used_point");
            entity.Property(x => x.LastActivityAt).HasColumnName("last_activity_at");
            entity.Property(x => x.RegisteredAt).HasColumnName("registered_at");
            entity.Property(x => x.ExtraValue1).HasColumnName("extra_value1");
            entity.Property(x => x.ExtraValue2).HasColumnName("extra_value2");
            entity.Property(x => x.ExtraValue3).HasColumnName("extra_value3");
            entity.Property(x => x.ExtraValue4).HasColumnName("extra_value4");
            entity.Property(x => x.ExtraValue5).HasColumnName("extra_value5");
            entity.Property(x => x.ExtraValue6).HasColumnName("extra_value6");
            entity.Property(x => x.ExtraValue7).HasColumnName("extra_value7");
            entity.Property(x => x.ExtraInfo1).HasColumnName("extra_info1").HasMaxLength(150);
            entity.Property(x => x.ExtraInfo2).HasColumnName("extra_info2").HasMaxLength(150);
            entity.Property(x => x.ExtraInfo3).HasColumnName("extra_info3").HasMaxLength(500);
            entity.Property(x => x.ExtraInfo4).HasColumnName("extra_info4").HasMaxLength(500);
        });

        modelBuilder.Entity<GameAdminMemberEntity>(entity =>
        {
            entity.ToTable("game_admin_member");
            entity.HasKey(x => x.MemberNo);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.AdminStatus).HasColumnName("admin_status");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TournamentSessionEntity>(entity =>
        {
            entity.ToTable("tournament_session");
            entity.HasKey(x => x.SessionId);
            entity.Property(x => x.SessionId).HasColumnName("session_id").ValueGeneratedOnAdd();
            entity.Property(x => x.JoinStartAt).HasColumnName("join_start_at");
            entity.Property(x => x.MatchStartAt).HasColumnName("match_start_at");
            entity.Property(x => x.PlayStartAt).HasColumnName("play_start_at");
            entity.Property(x => x.PlayEndAt).HasColumnName("play_end_at");
            entity.Property(x => x.ViewEndAt).HasColumnName("view_end_at");
            entity.Property(x => x.NextStartAt).HasColumnName("next_start_at");
            entity.Property(x => x.NextCutAt).HasColumnName("next_cut_at");
            entity.Property(x => x.PlaySchedule).HasColumnName("play_schedule").HasMaxLength(200);
            entity.Property(x => x.PlayStatus).HasColumnName("play_status");
            entity.Property(x => x.PlayPhase).HasColumnName("play_phase");
            entity.Property(x => x.PlayerCount).HasColumnName("player_count");
            entity.Property(x => x.MaxPlayerCount).HasColumnName("max_player_count");
            entity.Property(x => x.MaxRoomCount).HasColumnName("max_room_count");
            entity.Property(x => x.SessionName).HasColumnName("session_name").HasMaxLength(100);
            entity.Property(x => x.RoomOption).HasColumnName("room_option").HasMaxLength(20);
            entity.Property(x => x.PrivateInfo).HasColumnName("private_info").HasMaxLength(20);
            entity.Property(x => x.MaxViewerCount).HasColumnName("max_viewer_count");
            entity.Property(x => x.PlayCount).HasColumnName("play_count");
            entity.Property(x => x.PlayTime).HasColumnName("play_time");
            entity.Property(x => x.PlayMode).HasColumnName("play_mode");
            entity.Property(x => x.JoinMoney).HasColumnName("join_money");
            entity.Property(x => x.PrizeMoney1).HasColumnName("prize_money_1");
            entity.Property(x => x.PrizeMoney2).HasColumnName("prize_money_2");
            entity.Property(x => x.PrizeMoney3).HasColumnName("prize_money_3");
            entity.Property(x => x.PrizeMoney4).HasColumnName("prize_money_4");
            entity.Property(x => x.PlanMemberNo).HasColumnName("plan_member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.ResultMemberNo1).HasColumnName("result_member_no_1").HasColumnType("bigint unsigned");
            entity.Property(x => x.ResultMemberNo2).HasColumnName("result_member_no_2").HasColumnType("bigint unsigned");
            entity.Property(x => x.ResultMemberNo3).HasColumnName("result_member_no_3").HasColumnType("bigint unsigned");
            entity.Property(x => x.ResultMemberNo4).HasColumnName("result_member_no_4").HasColumnType("bigint unsigned");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TournamentRoomEntity>(entity =>
        {
            entity.ToTable("tournament_room");
            entity.HasKey(x => new { x.SessionId, x.SubId });
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.SubId).HasColumnName("sub_id");
            entity.Property(x => x.RoomId).HasColumnName("room_id");
            entity.Property(x => x.PlanStartAt).HasColumnName("plan_start_at");
            entity.Property(x => x.PlanEndAt).HasColumnName("plan_end_at");
            entity.Property(x => x.StartedAt).HasColumnName("started_at");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at");
            entity.Property(x => x.MemberNo1).HasColumnName("member_no_1").HasColumnType("bigint unsigned");
            entity.Property(x => x.MemberNo2).HasColumnName("member_no_2").HasColumnType("bigint unsigned");
            entity.Property(x => x.MemberNo3).HasColumnName("member_no_3").HasColumnType("bigint unsigned");
            entity.Property(x => x.MemberNo4).HasColumnName("member_no_4").HasColumnType("bigint unsigned");
            entity.Property(x => x.JoinMemberNo1).HasColumnName("join_member_no_1").HasMaxLength(3);
            entity.Property(x => x.JoinMemberNo2).HasColumnName("join_member_no_2").HasMaxLength(3);
            entity.Property(x => x.JoinMemberNo3).HasColumnName("join_member_no_3").HasMaxLength(3);
            entity.Property(x => x.JoinMemberNo4).HasColumnName("join_member_no_4").HasMaxLength(3);
            entity.Property(x => x.ScoreTmp1).HasColumnName("score_tmp_1");
            entity.Property(x => x.ScoreTmp2).HasColumnName("score_tmp_2");
            entity.Property(x => x.ScoreTmp3).HasColumnName("score_tmp_3");
            entity.Property(x => x.ScoreTmp4).HasColumnName("score_tmp_4");
            entity.Property(x => x.Score1).HasColumnName("score_1");
            entity.Property(x => x.Score2).HasColumnName("score_2");
            entity.Property(x => x.Score3).HasColumnName("score_3");
            entity.Property(x => x.Score4).HasColumnName("score_4");
            entity.Property(x => x.Rank1MemberNo).HasColumnName("rank1_member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.Rank2MemberNo).HasColumnName("rank2_member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.Rank3MemberNo).HasColumnName("rank3_member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.Rank4MemberNo).HasColumnName("rank4_member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.Grade1MemberNo).HasColumnName("grade1_member_no").HasMaxLength(3);
            entity.Property(x => x.Grade2MemberNo).HasColumnName("grade2_member_no").HasMaxLength(3);
            entity.Property(x => x.Grade3MemberNo).HasColumnName("grade3_member_no").HasMaxLength(3);
            entity.Property(x => x.Grade4MemberNo).HasColumnName("grade4_member_no").HasMaxLength(3);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TournamentLimitEntity>(entity =>
        {
            entity.ToTable("tournament_limit");
            entity.HasKey(x => x.LimitNo);
            entity.Property(x => x.LimitNo).HasColumnName("limit_no");
            entity.Property(x => x.IsValid).HasColumnName("is_valid");
            entity.Property(x => x.LimitStartAt).HasColumnName("limit_start_at");
            entity.Property(x => x.LimitEndAt).HasColumnName("limit_end_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TournamentParticipantEntity>(entity =>
        {
            entity.ToTable("tournament_participant");
            entity.HasKey(x => x.MemberNo);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.JoinSequenceNo).HasColumnName("join_seq_no");
            entity.Property(x => x.JoinMemberNo).HasColumnName("join_member_no").HasMaxLength(3);
            entity.Property(x => x.JoinStatus).HasColumnName("join_status");
            entity.Property(x => x.TotalManageCount).HasColumnName("total_manage_count");
            entity.Property(x => x.ManageCount).HasColumnName("manage_count");
            entity.Property(x => x.LastManageAt).HasColumnName("last_manage_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerPresentEntity>(entity =>
        {
            entity.ToTable("player_present");
            entity.HasKey(x => x.PresentId);
            entity.Property(x => x.PresentId).HasColumnName("present_id").ValueGeneratedOnAdd();
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.ReceiveStatus).HasColumnName("receive_status");
            entity.Property(x => x.PresentAmount).HasColumnName("present_amount");
            entity.Property(x => x.PresentType).HasColumnName("present_type");
            entity.Property(x => x.PresentKind).HasColumnName("present_kind");
            entity.Property(x => x.PresentInfo).HasColumnName("present_info").HasMaxLength(200);
            entity.Property(x => x.PresentRefId).HasColumnName("present_ref_id").HasMaxLength(20);
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.SentAt).HasColumnName("sent_at");
            entity.Property(x => x.ReceivedAt).HasColumnName("received_at");
        });

        modelBuilder.Entity<CustomItemMasterEntity>(entity =>
        {
            entity.ToTable("custom_item_master");
            entity.HasKey(x => x.CustomId);
            entity.Property(x => x.CustomId).HasColumnName("custom_id");
            entity.Property(x => x.Kind).HasColumnName("kind");
            entity.Property(x => x.ItemName).HasColumnName("item_name").HasMaxLength(80);
            entity.Property(x => x.IsValid).HasColumnName("is_valid");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<BillingItemMasterEntity>(entity =>
        {
            entity.ToTable("billing_item_master");
            entity.HasKey(x => new { x.ItemCode, x.SubCode });
            entity.Property(x => x.ItemCode).HasColumnName("item_code").HasMaxLength(5);
            entity.Property(x => x.SubCode).HasColumnName("sub_code").HasMaxLength(6);
            entity.Property(x => x.ItemName).HasColumnName("item_name").HasMaxLength(30);
            entity.Property(x => x.ItemType).HasColumnName("item_type").HasMaxLength(1);
            entity.Property(x => x.FullCount).HasColumnName("full_count");
            entity.Property(x => x.UnitMoney).HasColumnName("unit_money");
            entity.Property(x => x.RepayAmount).HasColumnName("repay_amount");
            entity.Property(x => x.InternalComment).HasColumnName("internal_comment").HasMaxLength(150);
            entity.Property(x => x.SecondaryComment).HasColumnName("secondary_comment").HasMaxLength(200);
            entity.Property(x => x.IsOnSale).HasColumnName("is_on_sale");
            entity.Property(x => x.IsUsable).HasColumnName("is_usable");
            entity.Property(x => x.AgeLimit).HasColumnName("age_limit");
            entity.Property(x => x.SexCode).HasColumnName("sex_code").HasMaxLength(1);
            entity.Property(x => x.ItemDescription).HasColumnName("item_description").HasMaxLength(300);
            entity.Property(x => x.GiveResource).HasColumnName("give_resource").HasMaxLength(20);
            entity.Property(x => x.GiveMoneyType).HasColumnName("give_money_type").HasMaxLength(1);
            entity.Property(x => x.FunctionBox).HasColumnName("function_box").HasMaxLength(200);
            entity.Property(x => x.IsClientOnly).HasColumnName("is_client_only");
            entity.Property(x => x.IsUsedOnPurchase).HasColumnName("is_used_on_purchase");
            entity.Property(x => x.MaxPurchaseCount).HasColumnName("max_purchase_count");
            entity.Property(x => x.AvailableDays).HasColumnName("available_days");
            entity.Property(x => x.IsResellable).HasColumnName("is_resellable");
            entity.Property(x => x.IsPresentable).HasColumnName("is_presentable");
            entity.Property(x => x.IsPresentableInBag).HasColumnName("is_presentable_in_bag");
            entity.Property(x => x.IsExposed).HasColumnName("is_exposed");
            entity.Property(x => x.MoneyUnit).HasColumnName("money_unit").HasMaxLength(1);
            entity.Property(x => x.AvCode).HasColumnName("av_code").HasMaxLength(10);
            entity.Property(x => x.ModifiedAt).HasColumnName("modified_at");
        });

        modelBuilder.Entity<CustomShopMasterEntity>(entity =>
        {
            entity.ToTable("custom_shop_master");
            entity.HasKey(x => x.ShopNo);
            entity.Property(x => x.ShopNo).HasColumnName("shop_no");
            entity.Property(x => x.CustomId).HasColumnName("custom_id");
            entity.Property(x => x.ShopName).HasColumnName("shop_name").HasMaxLength(80);
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.HcPrice).HasColumnName("hc_price");
            entity.Property(x => x.GameMoney).HasColumnName("game_money");
            entity.Property(x => x.AvCode).HasColumnName("av_code").HasMaxLength(30);
            entity.Property(x => x.SaleStartAt).HasColumnName("sale_start_at");
            entity.Property(x => x.SaleEndAt).HasColumnName("sale_end_at");
            entity.Property(x => x.IsValid).HasColumnName("is_valid");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<CustomItemSetEntity>(entity =>
        {
            entity.ToTable("custom_item_set");
            entity.HasKey(x => new { x.SetId, x.CustomId });
            entity.Property(x => x.SetId).HasColumnName("set_id");
            entity.Property(x => x.CustomId).HasColumnName("custom_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerCustomItemEntity>(entity =>
        {
            entity.ToTable("player_custom_item");
            entity.HasKey(x => new { x.MemberNo, x.CustomId });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.CustomId).HasColumnName("custom_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.EquipSlot).HasColumnName("equip_slot");
            entity.Property(x => x.AcquiredAt).HasColumnName("acquired_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerFunctionItemEntity>(entity =>
        {
            entity.ToTable("player_function_item");
            entity.HasKey(x => new { x.MemberNo, x.ItemCode });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.ItemCode).HasColumnName("item_code").HasMaxLength(10);
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.BoughtAt).HasColumnName("bought_at");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.IsEquipped).HasColumnName("is_equipped");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerTitleEntity>(entity =>
        {
            entity.ToTable("player_title");
            entity.HasKey(x => new { x.MemberNo, x.TitleId });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.TitleId).HasColumnName("title_id").HasMaxLength(10);
            entity.Property(x => x.ValidFlag).HasColumnName("valid_flag").HasMaxLength(1);
            entity.Property(x => x.AcquiredAt).HasColumnName("acquired_at");
        });

        modelBuilder.Entity<PlayerDailyMissionEntity>(entity =>
        {
            entity.ToTable("player_daily_mission");
            entity.HasKey(x => new { x.MemberNo, x.MissionId });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.MissionId).HasColumnName("mission_id");
            entity.Property(x => x.ProgressCount).HasColumnName("progress_count");
            entity.Property(x => x.MissionState).HasColumnName("mission_state");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerDailyMissionHistoryEntity>(entity =>
        {
            entity.ToTable("player_daily_mission_history");
            entity.HasKey(x => new { x.MemberNo, x.TargetDate, x.MissionId });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.TargetDate).HasColumnName("target_date");
            entity.Property(x => x.MissionId).HasColumnName("mission_id");
            entity.Property(x => x.ProgressCount).HasColumnName("progress_count");
            entity.Property(x => x.MissionState).HasColumnName("mission_state");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerWeeklyRewardEntity>(entity =>
        {
            entity.ToTable("player_weekly_reward");
            entity.HasKey(x => new { x.MemberNo, x.RewardWeek, x.RewardId });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.RewardWeek).HasColumnName("reward_week");
            entity.Property(x => x.RewardId).HasColumnName("reward_id");
            entity.Property(x => x.ReceiveStatus).HasColumnName("receive_status");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerGradeRankEntity>(entity =>
        {
            entity.ToTable("player_grade_rank");
            entity.HasKey(x => new { x.RankDate, x.RankKind, x.MemberNo });
            entity.Property(x => x.RankDate).HasColumnName("rank_date");
            entity.Property(x => x.RankKind).HasColumnName("rank_kind");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.Rating).HasColumnName("rating");
            entity.Property(x => x.GradeLevel).HasColumnName("grade_level");
            entity.Property(x => x.LastPlayedAt).HasColumnName("last_played_at");
            entity.Property(x => x.ExtraCount).HasColumnName("extra_count");
            entity.Property(x => x.LastExtraAt).HasColumnName("last_extra_at");
            entity.Property(x => x.AvatarId).HasColumnName("avatar_id").HasMaxLength(200);
            entity.Property(x => x.DisplayFlag).HasColumnName("display_flag");
            entity.Property(x => x.RankPosition).HasColumnName("rank_position");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TournamentPlayerRatingEntity>(entity =>
        {
            entity.ToTable("tournament_player_rating");
            entity.HasKey(x => new { x.CupId, x.Sequence, x.MemberNo });
            entity.Property(x => x.CupId).HasColumnName("cup_id");
            entity.Property(x => x.Sequence).HasColumnName("seq");
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.TotalPoint).HasColumnName("total_point");
            entity.Property(x => x.MatchCount).HasColumnName("match_count");
            entity.Property(x => x.Point1).HasColumnName("point_slot_1");
            entity.Property(x => x.Point2).HasColumnName("point_slot_2");
            entity.Property(x => x.Point3).HasColumnName("point_slot_3");
            entity.Property(x => x.Point4).HasColumnName("point_slot_4");
            entity.Property(x => x.Point5).HasColumnName("point_slot_5");
            entity.Property(x => x.Point6).HasColumnName("point_slot_6");
            entity.Property(x => x.Point7).HasColumnName("point_slot_7");
            entity.Property(x => x.BoughtAt).HasColumnName("bought_at");
            entity.Property(x => x.JoinedAt).HasColumnName("joined_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerSkinEntity>(entity =>
        {
            entity.ToTable("player_skin");
            entity.HasKey(x => new { x.MemberNo, x.SkinNo });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.SkinNo).HasColumnName("skin_no");
            entity.Property(x => x.IsAttached).HasColumnName("is_attached");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlayerShopEntity>(entity =>
        {
            entity.ToTable("player_shop");
            entity.HasKey(x => new { x.MemberNo, x.ShopId });
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.ShopId).HasColumnName("shop_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.OpenedAt).HasColumnName("opened_at");
        });

        modelBuilder.Entity<EventGiftMasterEntity>(entity =>
        {
            entity.ToTable("event_gift_master");
            entity.HasKey(x => new { x.EventCode, x.EventNo, x.GiftCode });
            entity.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(20);
            entity.Property(x => x.EventNo).HasColumnName("event_no");
            entity.Property(x => x.GiftCode).HasColumnName("gift_code").HasMaxLength(20);
            entity.Property(x => x.GiftName).HasColumnName("gift_name").HasMaxLength(100);
            entity.Property(x => x.GiftValue).HasColumnName("gift_value");
            entity.Property(x => x.GiftType).HasColumnName("gift_type").HasMaxLength(1);
            entity.Property(x => x.TotalLimitCount).HasColumnName("total_limit_count");
            entity.Property(x => x.DailyLimitCount).HasColumnName("daily_limit_count");
            entity.Property(x => x.MissionNo).HasColumnName("mission_no");
            entity.Property(x => x.GiftMessage).HasColumnName("gift_message").HasMaxLength(500);
            entity.Property(x => x.GiftAvatarId).HasColumnName("gift_avatar_id").HasMaxLength(300);
            entity.Property(x => x.GiftGroup).HasColumnName("gift_group").HasMaxLength(10);
            entity.Property(x => x.GiftSenderId).HasColumnName("gift_sender_id").HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<SerialExchangeItemEntity>(entity =>
        {
            entity.ToTable("serial_exchange_item");
            entity.HasKey(x => new { x.EventCode, x.EventNo, x.ServiceId, x.MemberNo, x.GiftCode });
            entity.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(20);
            entity.Property(x => x.EventNo).HasColumnName("event_no");
            entity.Property(x => x.ServiceId).HasColumnName("service_id").HasMaxLength(20);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.GiftCode).HasColumnName("gift_code").HasMaxLength(20);
            entity.Property(x => x.GiftValue).HasColumnName("gift_value");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<SerialCouponEntity>(entity =>
        {
            entity.ToTable("serial_coupon");
            entity.HasKey(x => new { x.EventCode, x.EventNo, x.MissionNo, x.CouponNo });
            entity.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(20);
            entity.Property(x => x.EventNo).HasColumnName("event_no");
            entity.Property(x => x.MissionNo).HasColumnName("mission_no");
            entity.Property(x => x.CouponNo).HasColumnName("coupon_no").HasMaxLength(100);
            entity.Property(x => x.InquiryCheckNo).HasColumnName("inquiry_check_no").HasMaxLength(30);
            entity.Property(x => x.GiftCode).HasColumnName("gift_code").HasMaxLength(20);
            entity.Property(x => x.InquiryComment).HasColumnName("inquiry_comment").HasMaxLength(400);
            entity.Property(x => x.ValidCheck).HasColumnName("valid_check").HasMaxLength(1);
            entity.Property(x => x.MemberNo).HasColumnName("member_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<GameClearCountEntity>(entity =>
        {
            entity.ToTable("game_clear_count");
            entity.HasKey(x => x.GameId);
            entity.Property(x => x.GameId).HasColumnName("game_id").HasMaxLength(20);
            entity.Property(x => x.GameDescription).HasColumnName("game_description").HasMaxLength(256);
            entity.Property(x => x.CountDescription).HasColumnName("count_description").HasMaxLength(256);
            entity.Property(x => x.CountImageUrl).HasColumnName("count_image_url").HasMaxLength(256);
            entity.Property(x => x.Count).HasColumnName("clear_count");
            entity.Property(x => x.AdminNo).HasColumnName("admin_no").HasColumnType("bigint unsigned");
            entity.Property(x => x.CountStatus).HasColumnName("count_status");
            entity.Property(x => x.IsValid).HasColumnName("is_valid");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }
}

public sealed class GameDataContextFactory
{
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 0));
    private readonly GameDbContext _connections;

    public GameDataContextFactory(GameDbContext connections)
    {
        _connections = connections;
    }

    public async Task<GameDataContext> CreateAsync()
    {
        var connectionString = await _connections.GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder<GameDataContext>()
            .UseMySql(connectionString, ServerVersion, options => options.EnableRetryOnFailure())
            .Options;
        return new GameDataContext(options);
    }
}