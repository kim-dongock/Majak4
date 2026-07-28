using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;

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
    // 原典 s_stLevelGradeMode の全 19 エントリを検証
    [Theory]
    [InlineData(100, 30)]   // GRADE_10_KYU
    [InlineData(101, 30)]   // GRADE_9_KYU
    [InlineData(102, 30)]   // GRADE_8_KYU
    [InlineData(103, 30)]   // GRADE_7_KYU
    [InlineData(104, 60)]   // GRADE_6_KYU
    [InlineData(105, 60)]   // GRADE_5_KYU
    [InlineData(106, 60)]   // GRADE_4_KYU
    [InlineData(107, 90)]   // GRADE_3_KYU
    [InlineData(108, 90)]   // GRADE_2_KYU
    [InlineData(109, 90)]   // GRADE_1_KYU
    [InlineData(1,   600)]  // GRADE_1_DAN
    [InlineData(2,   1200)] // GRADE_2_DAN
    [InlineData(3,   1200)] // GRADE_3_DAN
    [InlineData(4,   2400)] // GRADE_4_DAN
    [InlineData(5,   2400)] // GRADE_5_DAN
    [InlineData(6,   2400)] // GRADE_6_DAN
    [InlineData(7,   4800)] // GRADE_7_DAN
    [InlineData(8,   4800)] // GRADE_8_DAN
    [InlineData(9,   4800)] // GRADE_9_DAN
    public void GetMaxPoint_KnownGrade_ReturnsCorrectValue(int grade, int expected)
    {
        Assert.Equal(expected, GradeLevelTable.GetMaxPoint(grade));
    }

    [Fact]
    public void GetMaxPoint_UnknownGrade_ReturnsZero()
    {
        Assert.Equal(0, GradeLevelTable.GetMaxPoint(0));
        Assert.Equal(0, GradeLevelTable.GetMaxPoint(99));
        Assert.Equal(0, GradeLevelTable.GetMaxPoint(999));
    }

    [Fact]
    public void GetMaxPoint_Grade1Dan_Is600()
    {
        // GRADE_1_DAN (値=1) の MaxPoint は 600
        Assert.Equal(600, GradeLevelTable.GetMaxPoint(1));
    }

    [Fact]
    public void GetMaxPoint_TopGrade_Is4800()
    {
        // GRADE_9_DAN (値=9) の MaxPoint は 4800
        Assert.Equal(4800, GradeLevelTable.GetMaxPoint(9));
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
