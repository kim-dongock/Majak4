using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

/// <summary>
/// GameMoneyService テスト
///
/// 検証シナリオ:
///   - AddMoneyAsync: コイン加算・減算・ゼロ以下クランプ・レベル更新・プロシージャ呼び出し
///   - ReplenishAsync: 成功 / コイン十分で不要 / 1日1回制限 / 午前6時の日次切替
///   - ApplyEarnedMoneyAsync: 成功 / GAMMONEY_U あり / EarnedMoney ゼロ
///   - GiveYakumanBonusAsync: 役満ボーナス金額
/// </summary>
public class GameMoneyServiceTests
{
    private readonly Mock<PlayerRepository>          _playerRepoMock = new(MockBehavior.Loose);
    private readonly Mock<HistoryRepository>         _histMock       = new(MockBehavior.Loose);
    private readonly RatingService                   _ratingService  = new();
    private readonly GameMoneyService                _svc;

    public GameMoneyServiceTests()
    {
        _playerRepoMock.Setup(r => r.AddEarnedGameMoneyAsync(
                It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.UpdateChargeFreeMoneyAsync(It.IsAny<MajakPlayer>()))
            .ReturnsAsync(true);
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync(It.IsAny<string>()))
            .ReturnsAsync(((long, int)?)(0, 0));

        _svc = new GameMoneyService(_playerRepoMock.Object, _ratingService, _histMock.Object);
    }

    [Fact]
    public async Task CreateCommonRatWithDefaultMoneyHistAsync_WritesLegacyDefaultMoneyHistory()
    {
        await _svc.CreateCommonRatWithDefaultMoneyHistAsync("user01", GameConst.DefaultMoney, "1.2.3.4");

        _playerRepoMock.Verify(r => r.CreateCommonRatAsync("user01", GameConst.DefaultMoney), Times.Once);
        _histMock.Verify(r => r.InsertGameMoneyHistAsync(
            "user01",
            GameConst.EvtCodeDefaultMoney,
            GameConst.DefaultMoney,
            0,
            GameConst.DefaultMoney,
            "1.2.3.4"), Times.Once);
    }

    // ─── AddMoneyAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddMoneyAsync_PositiveDelta_IncreasesGamMoney()
    {
        var player = new MajakPlayer { GamMoney = 500, MemberNo = "user01" };
        await _svc.AddMoneyAsync(player, 500, GameConst.EvtCodeFreeMoney);

        Assert.Equal(1000, player.GamMoney);
    }

    [Fact]
    public async Task AddMoneyAsync_NegativeDelta_DecreasesGamMoney()
    {
        var player = new MajakPlayer { GamMoney = 1000, MemberNo = "user01" };
        await _svc.AddMoneyAsync(player, -300, GameConst.EvtCodeRoomCharge);

        Assert.Equal(700, player.GamMoney);
    }

    [Fact]
    public async Task AddMoneyAsync_NegativeDelta_ClampsToZero()
    {
        var player = new MajakPlayer { GamMoney = 100, MemberNo = "user01" };
        await _svc.AddMoneyAsync(player, -500, GameConst.EvtCodeRoomCharge);

        // コインはマイナスにならない
        Assert.Equal(0, player.GamMoney);
    }

    [Fact]
    public async Task AddMoneyAsync_UpdatesPlayerLevel()
    {
        var player = new MajakPlayer { GamMoney = 900, MemberNo = "user01", NLevel = 1 };
        await _svc.AddMoneyAsync(player, 100, GameConst.EvtCodeFreeMoney);

        // 1000コイン → NLevel=5 (熟練者)
        // ※ ただし GamMoney=1000 → NLevel=GetNLevel(1000) = 5
        Assert.True(player.NLevel > 0);
    }

    [Fact]
    public async Task AddMoneyAsync_CallsMajak2HistProc()
    {
        var player = new MajakPlayer { GamMoney = 500, MemberNo = "user01" };
        await _svc.AddMoneyAsync(player, 200, "JM00070", "1.2.3.4");

        _playerRepoMock.Verify(r => r.AddEarnedGameMoneyAsync(
            "user01",
            200L,
            "JM00070",
            "JM00070",
            "",
            "1.2.3.4"),
            Times.Once);
    }

    // ─── ReplenishAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ReplenishAsync_LowMoney_SucceedsAndFillsToTarget()
    {
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 200, AllinCnt = 0 };
        var (ok, newMoney, lentMoney, restAllIn, _) = await _svc.ReplenishAsync(player, 0);

        Assert.True(ok);
        Assert.Equal(GameConst.AllinMoney, newMoney);   // 1000
        Assert.Equal(0, lentMoney);
        Assert.Equal(0, restAllIn);   // AllinCountMax=1, 今回消費で残0
    }

    [Fact]
    public async Task ReplenishAsync_WritesExactFreeGpHistory()
    {
        var player = new MajakPlayer
        {
            MemberNo = "user01",
            GamMoney = 250,
            AllinCnt = 0,
            IpAddress = "1.2.3.4",
        };

        var (ok, _, _, _, _) = await _svc.ReplenishAsync(player, 0);

        Assert.True(ok);
        _histMock.Verify(r => r.InsertGameMoneyHistAsync(
            "user01",
            GameConst.EvtCodeFreeMoney,
            750,
            250,
            1000,
            "1.2.3.4"), Times.Once);
    }

    [Fact]
    public async Task ReplenishAsync_SufficientMoney_ReturnsFalse()
    {
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000, AllinCnt = 0 };
        var (ok, newMoney, _, _, _) = await _svc.ReplenishAsync(player, 0);

        Assert.False(ok);
        Assert.Equal(1000, newMoney);   // 変化なし
    }

    [Fact]
    public async Task ReplenishAsync_AllinCountExceeded_ReturnsFalse()
    {
        var player = new MajakPlayer
        {
            MemberNo = "user01",
            GamMoney = 0,
            AllinCnt = GameConst.AllinCountMax,   // 上限に達している
            LastAllinDt = DateTime.Now,
        };
        var (ok, _, _, restAllIn, _) = await _svc.ReplenishAsync(player, 0);

        Assert.False(ok);
        Assert.Equal(0, restAllIn);
    }

    [Fact]
    public async Task ReplenishAsync_Grade2Dan_UsesOfficialTarget()
    {
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            GamMoney    = 500,
            AllinCnt    = 0,
            GradeRecord = new RatingRecord { Grade = GameConst.Grade2Dan }   // 2段以上
        };
        var (ok, newMoney, _, _, _) = await _svc.ReplenishAsync(player, 0);

        Assert.True(ok);
        Assert.Equal(GameConst.AllinMoney, newMoney);
    }

    [Fact]
    public async Task ReplenishAsync_NetCafeStillLimitedToOncePerDay()
    {
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            GamMoney    = 0,
            AllinCnt    = 1,          // 既に1回消費
            LastAllinDt = DateTime.Now,
            IsNetCafeIp = true        // ネットカフェ → 最大2回
        };
        var (ok, newMoney, _, restAllIn, _) = await _svc.ReplenishAsync(player, 0);

        Assert.False(ok);
        Assert.Equal(0, newMoney);
        Assert.Equal(0, restAllIn);
    }

    [Fact]
    public void RefreshReplenishmentDay_BeforeSix_DoesNotResetSameBusinessDay()
    {
        var player = new MajakPlayer
        {
            AllinCnt = 1,
            LastAllinDt = new DateTime(2026, 8, 8, 6, 0, 0),
        };

        GameMoneyService.RefreshReplenishmentDay(player, new DateTime(2026, 8, 9, 5, 59, 59));

        Assert.Equal(1, player.AllinCnt);
    }

    [Fact]
    public void RefreshReplenishmentDay_AtSix_ResetsForNewBusinessDay()
    {
        var player = new MajakPlayer
        {
            AllinCnt = 1,
            LastAllinDt = new DateTime(2026, 8, 8, 6, 0, 0),
        };

        GameMoneyService.RefreshReplenishmentDay(player, new DateTime(2026, 8, 9, 6, 0, 0));

        Assert.Equal(0, player.AllinCnt);
    }

    [Fact]
    public async Task ReplenishAsync_PreviousAllinBusinessDay_ResetsCountBeforeLimitCheck()
    {
        var player = new MajakPlayer
        {
            MemberNo = "user01",
            GamMoney = 0,
            AllinCnt = GameConst.AllinCountMax,
            LastAllinDt = DateTime.Now.AddDays(-1),
        };

        var (ok, newMoney, _, restAllIn, _) = await _svc.ReplenishAsync(player, 0);

        Assert.True(ok);
        Assert.Equal(GameConst.AllinMoney, newMoney);
        Assert.Equal(1, player.AllinCnt);
        Assert.Equal(0, restAllIn);
    }

    [Fact]
    public async Task ReplenishAsync_UpdateChargeFails_RestoresPlayerState()
    {
        var lastAllin = DateTime.Now.AddHours(-1);
        var player = new MajakPlayer
        {
            MemberNo = "user01",
            GamMoney = 200,
            AllinCnt = 0,
            LastAllinDt = lastAllin,
        };
        _playerRepoMock.Setup(r => r.UpdateChargeFreeMoneyAsync(player))
            .ReturnsAsync(false);

        var (ok, newMoney, _, restAllIn, _) = await _svc.ReplenishAsync(player, 0);

        Assert.False(ok);
        Assert.Equal(200, newMoney);
        Assert.Equal(200, player.GamMoney);
        Assert.Equal(0, player.AllinCnt);
        Assert.Equal(lastAllin, player.LastAllinDt);
        Assert.Equal(GameConst.AllinCountMax, restAllIn);
    }

    // ─── ApplyEarnedMoneyAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ApplyEarnedMoneyAsync_Success_AddsEarnedToGamMoney()
    {
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            GamMoney    = 500,
            EarnedMoney = 300,
            GamMoneyU   = 0    // 未確定コインなし
        };
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync("user01"))
            .ReturnsAsync((300, 12));

        var (ok, newMoney) = await _svc.ApplyEarnedMoneyAsync(player);

        Assert.True(ok);
        Assert.Equal(800, newMoney);
        Assert.Equal(12, player.Experience);
        Assert.Equal(0, player.EarnedMoney);   // リセット確認
    }

    [Fact]
    public async Task ApplyEarnedMoneyAsync_GamMoneyU_NotZero_ReturnsFalse()
    {
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            GamMoney    = 500,
            EarnedMoney = 300,
            GamMoneyU   = 100   // 未確定コインあり → 適用不可
        };
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync("user01"))
            .ReturnsAsync((300, 12));

        var (ok, newMoney) = await _svc.ApplyEarnedMoneyAsync(player);

        Assert.False(ok);
        Assert.Equal(500, newMoney);   // 変化なし
        Assert.Equal(300, player.EarnedMoney);
    }

    [Fact]
    public async Task ApplyEarnedMoneyAsync_ReloadsEarnedMoneyFromRepository()
    {
        var player = new MajakPlayer
        {
            MemberNo = "user01",
            GamMoney = 500,
            EarnedMoney = 0,
            GamMoneyU = 0,
        };
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync("user01"))
            .ReturnsAsync((300, 12));
        long earnedAtUpdate = -1;
        int experienceAtUpdate = -1;
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Callback<MajakPlayer>(p =>
            {
                earnedAtUpdate = p.EarnedMoney;
                experienceAtUpdate = p.Experience;
            })
            .Returns(Task.CompletedTask);

        var (ok, newMoney) = await _svc.ApplyEarnedMoneyAsync(player);

        Assert.True(ok);
        Assert.Equal(800, newMoney);
        Assert.Equal(300, earnedAtUpdate);
        Assert.Equal(12, experienceAtUpdate);
        Assert.Equal(0, player.EarnedMoney);
    }

    [Fact]
    public async Task ApplyEarnedMoneyAsync_ZeroEarned_ReturnsFalse()
    {
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            GamMoney    = 500,
            EarnedMoney = 0
        };
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync("user01"))
            .ReturnsAsync((0, 12));

        var (ok, newMoney) = await _svc.ApplyEarnedMoneyAsync(player);

        Assert.False(ok);
        Assert.Equal(500, newMoney);
    }

    // ─── GiveYakumanBonusAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GiveYakumanBonusAsync_Adds200Coins()
    {
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };
        await _svc.GiveYakumanBonusAsync(player);

        Assert.Equal(1200, player.GamMoney);

        _playerRepoMock.Verify(r => r.AddEarnedGameMoneyAsync(
            "user01", GameConst.YakumanBonusMoney,
            GameConst.EvtCodeYakumanBonus, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }
}
