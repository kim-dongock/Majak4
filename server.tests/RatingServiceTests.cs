using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Tests;

/// <summary>
/// RatingService テスト
/// NLevel / SLevel / UpdatePlayerLevel の境界値・異常系を網羅する。
/// </summary>
public class RatingServiceTests
{
    private readonly RatingService _svc = new();

    // ─── GetNLevel ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,       0)]   // 0コイン → 見習い
    [InlineData(1,       1)]   // 1コイン → 初心者
    [InlineData(499,     1)]   // 初心者上限
    [InlineData(500,     2)]   // 平均
    [InlineData(1499,    2)]   // 平均上限
    [InlineData(1500,    3)]   // 中級者
    [InlineData(2999,    3)]   // 中級者上限
    [InlineData(3000,    4)]   // 上級者
    [InlineData(9999,    4)]   // 上級者上限
    [InlineData(10000,   5)]   // 熟練者
    [InlineData(29999,   5)]   // 熟練者上限
    [InlineData(30000,   6)]   // 達人
    [InlineData(99999,   6)]   // 達人上限
    [InlineData(100000,  7)]   // 名人
    [InlineData(499999,  7)]   // 名人上限
    [InlineData(500000,  8)]   // 宗師
    [InlineData(999999,  8)]   // 宗師上限
    [InlineData(1000000, 9)]   // 王
    [InlineData(4999999, 9)]   // 王上限
    [InlineData(5000000, 10)]  // 皇帝
    [InlineData(9999999, 10)]  // 皇帝 (最大)
    public void GetNLevel_ReturnsCorrectLevel(long gamMoney, int expectedLevel)
    {
        Assert.Equal(expectedLevel, _svc.GetNLevel(gamMoney));
    }

    [Fact]
    public void GetNLevel_NegativeMoney_Returns0()
    {
        // コインがマイナスになることはないが、念のため境界確認
        Assert.Equal(0, _svc.GetNLevel(-1));
    }

    // ─── GetSLevel ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  "無一文")]
    [InlineData(1,  "金欠")]
    [InlineData(2,  "庶民")]
    [InlineData(3,  "平民")]
    [InlineData(4,  "一般人")]
    [InlineData(5,  "中流")]
    [InlineData(6,  "上流")]
    [InlineData(7,  "金持ち")]
    [InlineData(8,  "富豪")]
    [InlineData(9,  "大富豪")]
    [InlineData(10, "財閥")]
    public void GetSLevel_ReturnsCorrectName(int nLevel, string expectedName)
    {
        Assert.Equal(expectedName, _svc.GetSLevel(nLevel));
    }

    [Fact]
    public void GetSLevel_OutOfRange_ReturnsFallback()
    {
        // 範囲外は最小値 ("無一文") を返すこと
        Assert.Equal("無一文", _svc.GetSLevel(-1));
        Assert.Equal("無一文", _svc.GetSLevel(99));
    }

    // ─── UpdatePlayerLevel ─────────────────────────────────────────────────

    [Fact]
    public void UpdatePlayerLevel_UpdatesNLevelAndSLevel()
    {
        var player = new MajakPlayer { GamMoney = 10000 };
        _svc.UpdatePlayerLevel(player);

        Assert.Equal(5, player.NLevel);
        Assert.Equal("中流", player.SLevel);
    }

    [Fact]
    public void UpdatePlayerLevel_ZeroMoney_SetsLevel0()
    {
        var player = new MajakPlayer { GamMoney = 0 };
        _svc.UpdatePlayerLevel(player);

        Assert.Equal(0, player.NLevel);
        Assert.Equal("無一文", player.SLevel);
    }

    [Fact]
    public void UpdatePlayerLevel_MaxMoney_SetsLevel10()
    {
        var player = new MajakPlayer { GamMoney = 5_000_000 };
        _svc.UpdatePlayerLevel(player);

        Assert.Equal(10, player.NLevel);
        Assert.Equal("財閥", player.SLevel);
    }

    // ─── NLevel は PC_MAJAK2_HIST 内部でのみ「下がらない」制約 ────────────
    // UpdatePlayerLevel 自体はコインに基づき常に再計算する。

    [Fact]
    public void UpdatePlayerLevel_RecalculatesFromCurrentMoney()
    {
        // コインが減った場合、UpdatePlayerLevel はレベルを下げる
        // (PC_MAJAK2_HIST 内の IF V_NLEVEL < TEMP_NLEVEL はプロシージャ側の制約)
        var player = new MajakPlayer { GamMoney = 500, NLevel = 8 };
        _svc.UpdatePlayerLevel(player);

        Assert.Equal(2, player.NLevel);   // 500コイン → NLevel=2
        Assert.Equal("庶民", player.SLevel);
    }

    [Theory]
    [InlineData(0, 499, "0ZG6A", false)]
    [InlineData(0, 500, "0ZG6A", true)]
    [InlineData(12, 500, "0ZG6A", true)]
    [InlineData(13, 500, "0ZG6A", false)]
    [InlineData(10, 4_999, "0ZG6B", false)]
    [InlineData(10, 5_000, "0ZG6B", true)]
    [InlineData(18, 5_000, "0ZG6B", true)]
    [InlineData(13, 9_999, "0ZG6C", false)]
    [InlineData(12, 10_000, "0ZG6C", false)]
    [InlineData(13, 10_000, "0ZG6C", true)]
    [InlineData(16, 29_999, "0ZG6D", false)]
    [InlineData(15, 30_000, "0ZG6D", false)]
    [InlineData(16, 30_000, "0ZG6D", true)]
    [InlineData(18, 30_000, "0ZG6D", true)]
    public void CheckEnterGradeMode_UsesOfficialRoomBoundaries(
        int gradeLevel, long gamMoney, string subId, bool expected)
    {
        Assert.Equal(expected, _svc.CheckEnterGradeMode(gradeLevel, gamMoney, subId));
    }
}
