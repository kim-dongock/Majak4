namespace MajakServer.Repositories.MySQL.Entities;

public sealed class PlayerDailyMissionEntity
{
    public ulong MemberNo { get; set; }
    public byte MissionId { get; set; }
    public ushort ProgressCount { get; set; }
    public byte MissionState { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerDailyMissionHistoryEntity
{
    public ulong MemberNo { get; set; }
    public DateOnly TargetDate { get; set; }
    public byte MissionId { get; set; }
    public ushort ProgressCount { get; set; }
    public byte MissionState { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerWeeklyRewardEntity
{
    public ulong MemberNo { get; set; }
    public DateOnly RewardWeek { get; set; }
    public byte RewardId { get; set; }
    public byte ReceiveStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerGradeRankEntity
{
    public DateOnly RankDate { get; set; }
    public byte RankKind { get; set; }
    public ulong MemberNo { get; set; }
    public int Rating { get; set; }
    public int GradeLevel { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public uint ExtraCount { get; set; }
    public DateTime? LastExtraAt { get; set; }
    public string AvatarId { get; set; } = string.Empty;
    public byte DisplayFlag { get; set; }
    public int? RankPosition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TournamentPlayerRatingEntity
{
    public uint CupId { get; set; }
    public uint Sequence { get; set; }
    public ulong MemberNo { get; set; }
    public long TotalPoint { get; set; }
    public ushort MatchCount { get; set; }
    public long? Point1 { get; set; }
    public long? Point2 { get; set; }
    public long? Point3 { get; set; }
    public long? Point4 { get; set; }
    public long? Point5 { get; set; }
    public long? Point6 { get; set; }
    public long? Point7 { get; set; }
    public DateTime? BoughtAt { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerSkinEntity
{
    public ulong MemberNo { get; set; }
    public ushort SkinNo { get; set; }
    public bool IsAttached { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerShopEntity
{
    public ulong MemberNo { get; set; }
    public ushort ShopId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime OpenedAt { get; set; }
}

public sealed class EventGiftMasterEntity
{
    public string EventCode { get; set; } = string.Empty;
    public uint EventNo { get; set; }
    public string GiftCode { get; set; } = string.Empty;
    public string? GiftName { get; set; }
    public long? GiftValue { get; set; }
    public string? GiftType { get; set; }
    public uint? TotalLimitCount { get; set; }
    public uint? DailyLimitCount { get; set; }
    public int MissionNo { get; set; }
    public string? GiftMessage { get; set; }
    public string? GiftAvatarId { get; set; }
    public string? GiftGroup { get; set; }
    public string? GiftSenderId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class SerialExchangeItemEntity
{
    public string EventCode { get; set; } = string.Empty;
    public uint EventNo { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public ulong MemberNo { get; set; }
    public string GiftCode { get; set; } = string.Empty;
    public long GiftValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SerialCouponEntity
{
    public string EventCode { get; set; } = string.Empty;
    public uint EventNo { get; set; }
    public int MissionNo { get; set; }
    public string CouponNo { get; set; } = string.Empty;
    public string? InquiryCheckNo { get; set; }
    public string? GiftCode { get; set; }
    public string? InquiryComment { get; set; }
    public string? ValidCheck { get; set; }
    public ulong? MemberNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class GameClearCountEntity
{
    public string GameId { get; set; } = string.Empty;
    public string? GameDescription { get; set; }
    public string? CountDescription { get; set; }
    public string? CountImageUrl { get; set; }
    public long Count { get; set; }
    public ulong? AdminNo { get; set; }
    public byte CountStatus { get; set; }
    public bool IsValid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
