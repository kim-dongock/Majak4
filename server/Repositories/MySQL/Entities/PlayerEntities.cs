namespace MajakServer.Repositories.MySQL.Entities;

public sealed class PlayerAccountEntity
{
    public ulong MemberNo { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? GoogleSub { get; set; }
    public string SexCode { get; set; } = "U";
    public string AvatarId { get; set; } = string.Empty;
    public DateTime? TermsAgreedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public ulong? ApprovedBy { get; set; }
    public string? RejectReason { get; set; }
    public byte AccountStatus { get; set; }
    public string SourceEnvironment { get; set; } = "production";
    public DateTime FirstLoginAt { get; set; }
    public DateTime LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerWalletEntity
{
    public ulong MemberNo { get; set; }
    public long GameMoney { get; set; } = 1000;
    public long PendingGameMoney { get; set; }
    public long EarnedGameMoney { get; set; }
    public long LoanedGameMoney { get; set; }
    public int GemCount { get; set; }
    public int CashCount { get; set; }
    public int PaidCashCount { get; set; }
    public int FreeCashCount { get; set; }
    public ulong RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void GrantFreeCash(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        FreeCashCount = checked(FreeCashCount + amount);
        CashCount = checked(CashCount + amount);
    }

    public void SpendCash(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (CashCount < amount) throw new InvalidOperationException("Majak Cash balance is insufficient.");

        int freeSpent = Math.Min(FreeCashCount, amount);
        FreeCashCount -= freeSpent;
        PaidCashCount -= amount - freeSpent;
        CashCount -= amount;
    }
}

public sealed class PlayerAvatarInventoryEntity
{
    public ulong InventoryId { get; set; }
    public ulong MemberNo { get; set; }
    public string AvatarCode { get; set; } = string.Empty;
    public long CostMoney { get; set; }
    public int CostGem { get; set; }
    public DateTime AcquiredAt { get; set; }
}

public sealed class PlayerProfileEntity
{
    public ulong MemberNo { get; set; }
    public int CommonRating { get; set; } = 1400;
    public int Experience { get; set; }
    public byte BestMoneyLevel { get; set; } = 2;
    public int ConsecutiveWinLoss { get; set; }
    public uint AllInCount { get; set; }
    public DateTime? LastAllInAt { get; set; }
    public string? TrickTitleCode { get; set; }
    public string? MajakTitleCode { get; set; }
    public string? EventOpenFlag { get; set; }
    public int WeeklyPoint { get; set; }
    public DateOnly WeeklyTargetDate { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public ulong RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerModeStatsEntity
{
    public ulong MemberNo { get; set; }
    public string ModeCode { get; set; } = string.Empty;
    public int Rating { get; set; } = 1400;
    public uint MatchCount { get; set; }
    public uint WinCount { get; set; }
    public uint DefeatCount { get; set; }
    public uint DrawCount { get; set; }
    public uint FirstCount { get; set; }
    public uint SecondCount { get; set; }
    public uint ThirdCount { get; set; }
    public uint FourthCount { get; set; }
    public uint TurnCount { get; set; }
    public uint DealerCount { get; set; }
    public long PointSum { get; set; }
    public uint RoundCount { get; set; }
    public uint WinHandCount { get; set; }
    public long WinHandPoints { get; set; }
    public uint DealInCount { get; set; }
    public long DealInPoints { get; set; }
    public uint RiichiCount { get; set; }
    public uint MeldCount { get; set; }
    public long TipPoint { get; set; }
    public uint TipMatchCount { get; set; }
    public uint BustCount { get; set; }
    public uint BustOtherCount { get; set; }
    public uint DoraCount { get; set; }
    public uint UraDoraCount { get; set; }
    public uint RiichiWinCount { get; set; }
    public uint DisconnectCount { get; set; }
    public DateTime? LastDisconnectAt { get; set; }
    public string? LastChannelId { get; set; }
    public int GradeLevel { get; set; }
    public int GradePoint { get; set; }
    public uint ExtraCount { get; set; }
    public DateTime? LastExtraAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerHighClassSummaryEntity
{
    public ulong MemberNo { get; set; }
    public int? ScoreMax { get; set; }
    public int? ScoreMin { get; set; }
    public long? MoneyMax { get; set; }
    public long? MoneyMin { get; set; }
    public int WinHandDoraMax { get; set; }
    public uint ConsecutiveTopMax { get; set; }
    public uint ConsecutiveTopCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlayerHighClassYakuEntity
{
    public ulong MemberNo { get; set; }
    public ushort YakuId { get; set; }
    public uint Count { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CupPlayerRatingEntity
{
    public uint CupId { get; set; }
    public ulong MemberNo { get; set; }
    public int CupPoint { get; set; }
    public ushort MatchCount { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime LastPlayedAt { get; set; }
}