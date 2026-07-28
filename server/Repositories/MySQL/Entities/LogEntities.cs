namespace MajakServer.Repositories.MySQL.Entities;

public sealed class GameSessionLogEntity
{
    public ulong GameSessionId { get; set; }
    public DateTime PlayedAt { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public uint RoomId { get; set; }
    public bool IsPrivate { get; set; }
    public string RoomOption { get; set; } = string.Empty;
    public long MoneyRate { get; set; }
    public long MinimumMoney { get; set; }
    public long MaximumMoney { get; set; }
    public byte? MinimumClass { get; set; }
    public byte? MaximumClass { get; set; }
    public ulong? CupId { get; set; }
    public ushort? RuleId { get; set; }
    public ulong? CupSequence { get; set; }
    public ushort? UsedTicket { get; set; }
    public byte? CupRule { get; set; }
}

public sealed class GamePlayerResultLogEntity
{
    public ulong GamePlayerResultId { get; set; }
    public ulong GameSessionId { get; set; }
    public DateTime PlayedAt { get; set; }
    public ulong MemberNo { get; set; }
    public bool WasConnected { get; set; }
    public byte Ranking { get; set; }
    public int Score { get; set; }
    public int Point { get; set; }
    public bool HadYakitori { get; set; }
    public int Chip { get; set; }
    public long MoneyBefore { get; set; }
    public long LentMoneyBefore { get; set; }
    public long DealerFee { get; set; }
    public long MoneyChange { get; set; }
    public long MoneyAfter { get; set; }
    public long LentMoneyAfter { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public long? PreviousTicket { get; set; }
    public long? ReturnedTicket { get; set; }
    public byte? PreviousClass { get; set; }
    public byte? CurrentClass { get; set; }
    public long? CurrentTicket { get; set; }
}

public sealed class TrainingSessionLogEntity
{
    public ulong TrainingSessionId { get; set; }
    public DateTime PlayedAt { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public uint RoomId { get; set; }
    public string RoomOption { get; set; } = string.Empty;
    public byte PlayerCount { get; set; }
}

public sealed class TrainingPlayerResultLogEntity
{
    public ulong TrainingPlayerResultId { get; set; }
    public ulong TrainingSessionId { get; set; }
    public byte SeatOrder { get; set; }
    public ulong? MemberNo { get; set; }
    public int Point { get; set; }
}

public sealed class WeeklyRewardClaimLogEntity
{
    public ulong WeeklyRewardClaimId { get; set; }
    public ulong MemberNo { get; set; }
    public DateOnly RewardWeek { get; set; }
    public uint RewardId { get; set; }
    public byte ReceiveStatus { get; set; }
    public DateTime ClaimedAt { get; set; }
}

public sealed class MoneyTransactionLogEntity
{
    public ulong MoneyTransactionId { get; set; }
    public DateTime OccurredAt { get; set; }
    public ulong MemberNo { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long BalanceBefore { get; set; }
    public long BalanceAfter { get; set; }
    public bool IsValid { get; set; }
    public string? OrderNumber { get; set; }
    public string? AdditionalInfo { get; set; }
    public string? BillingOrderNumber { get; set; }
    public uint UnitCount { get; set; } = 1;
    public string RemoteAddress { get; set; } = string.Empty;
}

public sealed class WinningYakuLogEntity
{
    public ulong WinningYakuLogId { get; set; }
    public DateTime OccurredAt { get; set; }
    public ulong MemberNo { get; set; }
    public string GameId { get; set; } = string.Empty;
    public int YakuCode { get; set; }
}

public sealed class ItemPurchaseLogEntity
{
    public ulong ItemPurchaseId { get; set; }
    public DateTime PurchasedAt { get; set; }
    public ulong MemberNo { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public uint Quantity { get; set; }
    public long UnitPrice { get; set; }
    public string? ExternalUserNo { get; set; }
    public uint PurchaseChannel { get; set; }
    public string? OrderNumber { get; set; }
}

public sealed class PlayerLoginLogEntity
{
    public ulong LoginLogId { get; set; }
    public DateTime OccurredAt { get; set; }
    public ulong MemberNo { get; set; }
    public byte EventType { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}