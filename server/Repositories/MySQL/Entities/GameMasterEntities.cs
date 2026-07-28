namespace MajakServer.Repositories.MySQL.Entities;

public sealed class TransactionCodeMasterEntity
{
    public string TransactionCode { get; set; } = string.Empty;
    public string? CodeTitle { get; set; }
    public bool IsHistoryEnabled { get; set; }
    public bool IsCumulative { get; set; }
    public string? OpenStatus { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? Content { get; set; }
    public string? ServiceCode { get; set; }
    public string? ServiceName { get; set; }
    public bool IsServiceEnabled { get; set; }
    public string? GameId { get; set; }
    public string? RegistrantName { get; set; }
    public string? PlannerName { get; set; }
    public string? DeveloperName { get; set; }
    public string? DirectionCode { get; set; }
    public string? AvatarCode { get; set; }
}

public sealed class MemorialShopMasterEntity
{
    public ushort ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TitleMasterEntity
{
    public string TitleId { get; set; } = string.Empty;
    public string TitleName { get; set; } = string.Empty;
}

public sealed class DailyMissionMasterEntity
{
    public byte MissionId { get; set; }
    public byte ConditionType { get; set; }
    public ushort ConditionCount { get; set; }
    public byte Point { get; set; }
}

public sealed class WeeklyRewardMasterEntity
{
    public byte RewardId { get; set; }
    public byte RewardType { get; set; }
    public uint RewardCount { get; set; }
    public ushort RequiredPoint { get; set; }
}

public sealed class GradeRankScheduleEntity
{
    public DateTime RankDate { get; set; }
    public byte BatchFlag { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ChannelMasterEntity
{
    public string ChannelId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string SubId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public uint MaxMember { get; set; }
    public uint MaxRoom { get; set; }
    public uint UnitMoney { get; set; }
    public byte ChannelType { get; set; }
    public string Environment { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ChannelRuntimeEntity
{
    public string ChannelId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string SubId { get; set; } = string.Empty;
    public string GoService { get; set; } = string.Empty;
    public string ServerIp { get; set; } = string.Empty;
    public uint ServerPort { get; set; }
    public uint GamePort { get; set; }
    public uint QueryPort { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public ushort MaxMember { get; set; }
    public ushort MaxRoom { get; set; }
    public uint UnitMoney { get; set; }
    public ushort MemberCount { get; set; }
    public ushort UsedRoom { get; set; }
    public ushort ItemYesCount { get; set; }
    public ushort ItemNoCount { get; set; }
    public ushort MemberMale { get; set; }
    public ushort MemberFemale { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public DateTime? ChannelServerVersion { get; set; }
    public DateTime? RoomServerVersion { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string ZoneId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public byte ServiceMask { get; set; }
    public bool IsLocked { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class RuleMasterEntity
{
    public ushort RuleId { get; set; }
    public byte JudgementType { get; set; }
    public string RoomOption { get; set; } = string.Empty;
    public string NormalYakuCondition { get; set; } = string.Empty;
    public string YakumanCondition { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleDetail { get; set; } = string.Empty;
    public uint? EventSumType { get; set; }
}

public sealed class CupMasterEntity
{
    public uint CupId { get; set; }
    public string CupName { get; set; } = string.Empty;
    public string ShortCupName { get; set; } = string.Empty;
    public ushort RuleId { get; set; }
    public short ConditionMatchCount { get; set; }
    public byte ConditionRegular { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public DateTime NicknameStartAt { get; set; }
    public DateTime NicknameEndAt { get; set; }
    public string Prize { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public byte Status { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CupChannelEntity
{
    public uint CupId { get; set; }
    public string ChannelId { get; set; } = string.Empty;
}

public sealed class TournamentPlanMasterEntity
{
    public uint CupId { get; set; }
    public uint Sequence { get; set; }
    public string CupName { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public byte MinLevel { get; set; }
    public byte MaxLevel { get; set; }
    public int UnitMoney { get; set; }
    public short MaxMatchCount { get; set; }
    public ushort MinMatchCount { get; set; }
    public string Prize { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string AdminComment { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public ushort RuleId { get; set; }
    public string NoticeUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public byte BillingStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class EventMasterEntity
{
    public string EventCode { get; set; } = string.Empty;
    public uint EventNo { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string TableInfo { get; set; } = string.Empty;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class EventUserEntity
{
    public string EventCode { get; set; } = string.Empty;
    public uint EventNo { get; set; }
    public ulong MemberNo { get; set; }
    public long TotalEarnedPoint { get; set; }
    public long DailyEarnedPoint { get; set; }
    public long TotalUsedPoint { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime RegisteredAt { get; set; }
    public long ExtraValue1 { get; set; }
    public long ExtraValue2 { get; set; }
    public long ExtraValue3 { get; set; }
    public long ExtraValue4 { get; set; }
    public long ExtraValue5 { get; set; }
    public long ExtraValue6 { get; set; }
    public long ExtraValue7 { get; set; }
    public string ExtraInfo1 { get; set; } = string.Empty;
    public string ExtraInfo2 { get; set; } = string.Empty;
    public string ExtraInfo3 { get; set; } = string.Empty;
    public string ExtraInfo4 { get; set; } = string.Empty;
}

public sealed class GameAdminMemberEntity
{
    public ulong MemberNo { get; set; }
    public uint AdminStatus { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TournamentSessionEntity
{
    public ulong SessionId { get; set; }
    public DateTime JoinStartAt { get; set; }
    public DateTime MatchStartAt { get; set; }
    public DateTime PlayStartAt { get; set; }
    public DateTime PlayEndAt { get; set; }
    public DateTime ViewEndAt { get; set; }
    public DateTime NextStartAt { get; set; }
    public DateTime NextCutAt { get; set; }
    public string PlaySchedule { get; set; } = string.Empty;
    public byte PlayStatus { get; set; }
    public byte PlayPhase { get; set; }
    public ushort PlayerCount { get; set; }
    public ushort MaxPlayerCount { get; set; }
    public ushort MaxRoomCount { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string RoomOption { get; set; } = string.Empty;
    public string? PrivateInfo { get; set; }
    public ushort MaxViewerCount { get; set; }
    public byte PlayCount { get; set; }
    public byte PlayTime { get; set; }
    public byte PlayMode { get; set; }
    public long JoinMoney { get; set; }
    public long PrizeMoney1 { get; set; }
    public long PrizeMoney2 { get; set; }
    public long PrizeMoney3 { get; set; }
    public long PrizeMoney4 { get; set; }
    public ulong? PlanMemberNo { get; set; }
    public ulong? ResultMemberNo1 { get; set; }
    public ulong? ResultMemberNo2 { get; set; }
    public ulong? ResultMemberNo3 { get; set; }
    public ulong? ResultMemberNo4 { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TournamentRoomEntity
{
    public ulong SessionId { get; set; }
    public ushort SubId { get; set; }
    public ushort RoomId { get; set; }
    public DateTime PlanStartAt { get; set; }
    public DateTime PlanEndAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public ulong? MemberNo1 { get; set; }
    public ulong? MemberNo2 { get; set; }
    public ulong? MemberNo3 { get; set; }
    public ulong? MemberNo4 { get; set; }
    public string? JoinMemberNo1 { get; set; }
    public string? JoinMemberNo2 { get; set; }
    public string? JoinMemberNo3 { get; set; }
    public string? JoinMemberNo4 { get; set; }
    public int ScoreTmp1 { get; set; }
    public int ScoreTmp2 { get; set; }
    public int ScoreTmp3 { get; set; }
    public int ScoreTmp4 { get; set; }
    public int Score1 { get; set; }
    public int Score2 { get; set; }
    public int Score3 { get; set; }
    public int Score4 { get; set; }
    public ulong? Rank1MemberNo { get; set; }
    public ulong? Rank2MemberNo { get; set; }
    public ulong? Rank3MemberNo { get; set; }
    public ulong? Rank4MemberNo { get; set; }
    public string? Grade1MemberNo { get; set; }
    public string? Grade2MemberNo { get; set; }
    public string? Grade3MemberNo { get; set; }
    public string? Grade4MemberNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TournamentLimitEntity
{
    public byte LimitNo { get; set; }
    public bool IsValid { get; set; }
    public DateTime LimitStartAt { get; set; }
    public DateTime LimitEndAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TournamentParticipantEntity
{
    public ulong MemberNo { get; set; }
    public ulong SessionId { get; set; }
    public ulong JoinSequenceNo { get; set; }
    public string JoinMemberNo { get; set; } = string.Empty;
    public byte JoinStatus { get; set; }
    public uint TotalManageCount { get; set; }
    public uint ManageCount { get; set; }
    public DateTime? LastManageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerPresentEntity
{
    public ulong PresentId { get; set; }
    public ulong MemberNo { get; set; }
    public byte ReceiveStatus { get; set; }
    public long PresentAmount { get; set; }
    public byte PresentType { get; set; }
    public byte PresentKind { get; set; }
    public string? PresentInfo { get; set; }
    public string? PresentRefId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
}

public sealed class CustomItemMasterEntity
{
    public uint CustomId { get; set; }
    public byte Kind { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class BillingItemMasterEntity
{
    public string ItemCode { get; set; } = string.Empty;
    public string SubCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public int? FullCount { get; set; }
    public uint? UnitMoney { get; set; }
    public long? RepayAmount { get; set; }
    public string? InternalComment { get; set; }
    public string? SecondaryComment { get; set; }
    public bool IsOnSale { get; set; }
    public bool IsUsable { get; set; }
    public ushort? AgeLimit { get; set; }
    public string? SexCode { get; set; }
    public string? ItemDescription { get; set; }
    public string? GiveResource { get; set; }
    public string? GiveMoneyType { get; set; }
    public string? FunctionBox { get; set; }
    public bool? IsClientOnly { get; set; }
    public bool? IsUsedOnPurchase { get; set; }
    public ushort? MaxPurchaseCount { get; set; }
    public int? AvailableDays { get; set; }
    public bool? IsResellable { get; set; }
    public bool? IsPresentable { get; set; }
    public bool? IsPresentableInBag { get; set; }
    public bool? IsExposed { get; set; }
    public string? MoneyUnit { get; set; }
    public string? AvCode { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public sealed class CustomShopMasterEntity
{
    public uint ShopNo { get; set; }
    public uint CustomId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public uint HcPrice { get; set; }
    public uint GameMoney { get; set; }
    public string? AvCode { get; set; }
    public DateTime? SaleStartAt { get; set; }
    public DateTime? SaleEndAt { get; set; }
    public bool IsValid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CustomItemSetEntity
{
    public uint SetId { get; set; }
    public uint CustomId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerCustomItemEntity
{
    public ulong MemberNo { get; set; }
    public uint CustomId { get; set; }
    public ushort Quantity { get; set; }
    public ushort EquipSlot { get; set; }
    public DateTime AcquiredAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerFunctionItemEntity
{
    public ulong MemberNo { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public uint Quantity { get; set; }
    public DateTime BoughtAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsEquipped { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerTitleEntity
{
    public ulong MemberNo { get; set; }
    public string TitleId { get; set; } = string.Empty;
    public string? ValidFlag { get; set; }
    public DateTime AcquiredAt { get; set; }
}