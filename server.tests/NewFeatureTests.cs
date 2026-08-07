using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL.Entities;

namespace MajakServer.Tests;

/// <summary>
/// 新規実装コードのユニットテスト
///
/// 対象:
///   - GradeLevelTable.GetMaxPoint() — s_stLevelGradeMode テーブル
///   - MajakPlayer.GetRichiEffect() — CAT_RICHI アイテム判定
///   - BanishInfo — バニシュ状態管理
/// </summary>

// ═══════════════════════════════════════════════════════════════════════════
// GradeLevelTable テスト
// ═══════════════════════════════════════════════════════════════════════════
public class GradeLevelTableTests
{
    // 公式段位ポイント表の全 19 エントリを検証
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 30)]
    [InlineData(2, 30)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(5, 60)]
    [InlineData(6, 60)]
    [InlineData(7, 90)]
    [InlineData(8, 90)]
    [InlineData(9, 90)]
    [InlineData(10, 600)]
    [InlineData(11, 1200)]
    [InlineData(12, 1200)]
    [InlineData(13, 2400)]
    [InlineData(14, 2400)]
    [InlineData(15, 2400)]
    [InlineData(16, 4800)]
    [InlineData(17, 4800)]
    [InlineData(18, 4800)]
    public void GetMaxPoint_KnownGrade_ReturnsCorrectValue(int grade, int expected)
    {
        Assert.Equal(expected, GradeLevelTable.GetMaxPoint(grade));
    }

    [Fact]
    public void GetMaxPoint_UnknownGrade_ReturnsZero()
    {
        Assert.Equal(0, GradeLevelTable.GetMaxPoint(-1));
        Assert.Equal(0, GradeLevelTable.GetMaxPoint(99));
        Assert.Equal(0, GradeLevelTable.GetMaxPoint(999));
    }

    [Fact]
    public void GetMaxPoint_Grade1Dan_Is600()
    {
        Assert.Equal(600, GradeLevelTable.GetMaxPoint(10));
    }

    [Fact]
    public void GetMaxPoint_TopGrade_Is4800()
    {
        Assert.Equal(4800, GradeLevelTable.GetMaxPoint(18));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakPlayer.GetRichiEffect() テスト
// ═══════════════════════════════════════════════════════════════════════════
public class GetRichiEffectTests
{
    // シナリオ1: アイテム未所持 → 0
    [Fact]
    public void GetRichiEffect_NoItems_Returns0()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        Assert.Equal(0, player.GetRichiEffect());
    }

    // シナリオ2: UseFlag=false のアイテム → 無効 → 0
    [Fact]
    public void GetRichiEffect_ItemNotActive_Returns0()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item001",
            UseFlag  = false,   // 使用中でない
            Qty      = 1,
            BuyDt    = DateTime.Now,
            EndDt    = DateTime.Now.AddDays(7)
        });
        Assert.Equal(0, player.GetRichiEffect());
    }

    // シナリオ3: item001 使用中 → subCode=1 (普通リーチ)
    [Fact]
    public void GetRichiEffect_Item001Active_Returns1()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item001",
            UseFlag  = true,
            Qty      = 1,
            BuyDt    = DateTime.Now,
            EndDt    = DateTime.Now.AddDays(7)
        });
        Assert.Equal(1, player.GetRichiEffect());
    }

    // シナリオ4: item002 使用中 → subCode=2 (重リーチ)
    [Fact]
    public void GetRichiEffect_Item002Active_Returns2()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item002",
            UseFlag  = true,
            Qty      = 1,
            BuyDt    = DateTime.Now,
            EndDt    = DateTime.Now.AddDays(7)
        });
        Assert.Equal(2, player.GetRichiEffect());
    }

    // シナリオ5: item004 使用中 → subCode=3 (一点リーチ)
    [Fact]
    public void GetRichiEffect_Item004Active_Returns3()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item004",
            UseFlag  = true,
            Qty      = 1,
            BuyDt    = DateTime.Now,
            EndDt    = DateTime.Now.AddDays(7)
        });
        Assert.Equal(3, player.GetRichiEffect());
    }

    // シナリオ6: CAT_RICHI 以外のアイテムは無視
    [Fact]
    public void GetRichiEffect_NonRichiItem_Returns0()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item003",  // CAT_RICHI でないアイテム
            UseFlag  = true,
            Qty      = 1,
            BuyDt    = DateTime.Now,
            EndDt    = DateTime.Now.AddDays(7)
        });
        Assert.Equal(0, player.GetRichiEffect());
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// BanishInfo テスト
// ═══════════════════════════════════════════════════════════════════════════
public class BanishInfoTests
{
    // シナリオ1: 初期状態 — 全フィールドが無効値
    [Fact]
    public void Default_AllFieldsFalseOrNull()
    {
        var info = new BanishInfo();
        Assert.False(info.PreBanishing);
        Assert.False(info.ReserveBanishing);
        Assert.Null(info.ReserveMemberNo);
    }

    // シナリオ2: Reset() で全フィールドが初期化される
    [Fact]
    public void Reset_ClearsAllFields()
    {
        var info = new BanishInfo
        {
            PreBanishing     = true,
            ReserveBanishing = true,
            ReserveMemberNo  = "user99"
        };

        info.Reset();

        Assert.False(info.PreBanishing);
        Assert.False(info.ReserveBanishing);
        Assert.Null(info.ReserveMemberNo);
    }

    // シナリオ3: GameRoom の BanishInfo は初期状態で非バニシュ
    [Fact]
    public void GameRoom_InitialBanishInfo_IsDefault()
    {
        var room = new GameRoom { RoomId = 1, ChannelId = "ch1" };
        Assert.False(room.BanishInfo.PreBanishing);
        Assert.False(room.BanishInfo.ReserveBanishing);
    }

    // シナリオ4: バニシュ予約を設定できる
    [Fact]
    public void GameRoom_SetBanishInfo_Persists()
    {
        var room = new GameRoom { RoomId = 1, ChannelId = "ch1" };
        room.BanishInfo.ReserveBanishing = true;
        room.BanishInfo.ReserveMemberNo  = "user01";

        Assert.True(room.BanishInfo.ReserveBanishing);
        Assert.Equal("user01", room.BanishInfo.ReserveMemberNo);
    }
}

public class CashBalanceTests
{
    [Fact]
    public void SpendCash_UsesFreeBalanceBeforePaidBalance()
    {
        var wallet = new PlayerWalletEntity
        {
            CashCount = 150,
            PaidCashCount = 100,
            FreeCashCount = 50,
        };

        wallet.SpendCash(40);

        Assert.Equal(110, wallet.CashCount);
        Assert.Equal(100, wallet.PaidCashCount);
        Assert.Equal(10, wallet.FreeCashCount);
    }

    [Fact]
    public void SpendCash_UsesPaidBalanceAfterFreeBalanceIsExhausted()
    {
        var wallet = new PlayerWalletEntity
        {
            CashCount = 150,
            PaidCashCount = 100,
            FreeCashCount = 50,
        };

        wallet.SpendCash(70);

        Assert.Equal(80, wallet.CashCount);
        Assert.Equal(80, wallet.PaidCashCount);
        Assert.Equal(0, wallet.FreeCashCount);
    }
}
