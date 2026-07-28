using Moq;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// AdminIdService 単体テスト
// 原典: HMajAdminId.cpp ADCMD_IsAdminId / HMajDBObject::LoadAdminIdInfo — BZB_ADMINIDLIST から管理者判定
// ═══════════════════════════════════════════════════════════════════════════
public class AdminIdServiceTests
{
    private readonly Mock<PlayerRepository> _playerRepoMock
        = new(MockBehavior.Loose);

    private AdminIdService BuildService(List<AdminIdInfo>? adminList = null)
    {
        _playerRepoMock.Setup(r => r.GetAdminIdListAsync())
            .ReturnsAsync(adminList ?? new List<AdminIdInfo>
            {
                new AdminIdInfo { MemberNo = "admin01", AdminSts = 1 },
                new AdminIdInfo { MemberNo = "admin02", AdminSts = 2 },
            });
        return new AdminIdService(TestMasterCacheFactory.Create(playerRepo: _playerRepoMock.Object));
    }

    // シナリオ1: 管理者 ID が存在する → IsAdminId = true
    // 原典: ADCMD_IsAdminId — m_plistAdminId から strncmp で照合
    [Fact]
    public async Task IsAdminId_RegisteredAdmin_ReturnsTrue()
    {
        var svc = BuildService();
        await svc.InitAsync();

        Assert.True(svc.IsAdminId("admin01"));
        Assert.True(svc.IsAdminId("admin02"));
    }

    // シナリオ2: 非管理者 ID → IsAdminId = false
    [Fact]
    public async Task IsAdminId_NonAdmin_ReturnsFalse()
    {
        var svc = BuildService();
        await svc.InitAsync();

        Assert.False(svc.IsAdminId("user01"));
        Assert.False(svc.IsAdminId(""));
    }

    // シナリオ3: 大文字小文字を区別する (strncmp)
    [Fact]
    public async Task IsAdminId_DifferentCase_ReturnsFalse()
    {
        var svc = BuildService();
        await svc.InitAsync();

        Assert.False(svc.IsAdminId("ADMIN01"));
        Assert.False(svc.IsAdminId("Admin01"));
    }

    [Fact]
    public async Task IsAdminId_MemberNoLongerThanLegacyLimit_UsesFirst24Characters()
    {
        var legacyLengthId = "abcdefghijklmnopqrstuvwx";
        var svc = BuildService(new List<AdminIdInfo>
        {
            new AdminIdInfo { MemberNo = legacyLengthId + "Z", AdminSts = 3 },
        });
        await svc.InitAsync();

        Assert.True(svc.IsAdminId(legacyLengthId));
        Assert.True(svc.IsAdminId(legacyLengthId + "Y"));
        Assert.Equal(3, svc.GetAdminStatus(legacyLengthId + "Y"));
    }

    // シナリオ4: GetAdminStatus — 登録済み管理者のステータス値を返す
    [Fact]
    public async Task GetAdminStatus_RegisteredAdmin_ReturnsStatus()
    {
        var svc = BuildService();
        await svc.InitAsync();

        Assert.Equal(1, svc.GetAdminStatus("admin01"));
        Assert.Equal(2, svc.GetAdminStatus("admin02"));
    }

    // シナリオ5: GetAdminStatus — 未登録 → 0 を返す
    [Fact]
    public async Task GetAdminStatus_NonAdmin_ReturnsZero()
    {
        var svc = BuildService();
        await svc.InitAsync();

        Assert.Equal(0, svc.GetAdminStatus("user01"));
    }

    // シナリオ6: InitAsync 前はすべて false (空キャッシュ)
    [Fact]
    public void IsAdminId_BeforeInit_ReturnsFalse()
    {
        var svc = BuildService(); // InitAsync 未呼び出し

        Assert.False(svc.IsAdminId("admin01"));
    }

    // シナリオ7: DB 例外 → 空キャッシュで続行 (エラーを飲み込む)
    [Fact]
    public async Task InitAsync_DbError_EmptyCache()
    {
        _playerRepoMock.Setup(r => r.GetAdminIdListAsync())
            .ThrowsAsync(new Exception("DB Error"));

        var svc = new AdminIdService(TestMasterCacheFactory.Create(playerRepo: _playerRepoMock.Object));
        await svc.InitAsync(); // 例外を投げないこと

        Assert.False(svc.IsAdminId("admin01"));
    }

    // シナリオ8: ReloadAsync → キャッシュが更新される
    [Fact]
    public async Task ReloadAsync_UpdatesCache()
    {
        var svc = BuildService(new List<AdminIdInfo>()); // 初期は空
        await svc.InitAsync();
        Assert.False(svc.IsAdminId("admin99"));

        // 再ロードで admin99 が追加される
        _playerRepoMock.Setup(r => r.GetAdminIdListAsync())
            .ReturnsAsync(new List<AdminIdInfo>
            {
                new AdminIdInfo { MemberNo = "admin99", AdminSts = 9 }
            });
        await svc.ReloadAsync();

        Assert.True(svc.IsAdminId("admin99"));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MissionService 単体テスト
// 原典: ProcessCommand_GetMissionInfo / RcvWeeklyReward / RcvSerialBonus
// ═══════════════════════════════════════════════════════════════════════════
public class MissionServiceTests
{
    private readonly Mock<PlayerRepository> _playerRepoMock
        = new(MockBehavior.Loose);
    private readonly Mock<LogRepository> _logRepoMock
        = new(MockBehavior.Loose, (MySqlDbContext)null!);

    private MissionService BuildService()
    {
        _logRepoMock.Setup(r => r.InsertWeeklyRewardHistAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        return new MissionService(_logRepoMock.Object, _playerRepoMock.Object, TestMasterCacheFactory.Create(playerRepo: _playerRepoMock.Object));
    }

    [Fact]
    public async Task MasterCache_MissionMasts_ReturnRepositoryDataWhenRedisMisses()
    {
        _playerRepoMock.Setup(r => r.GetDailyMissionMastAsync())
            .ReturnsAsync(new Dictionary<int, DailyMissionMastInfo>
            {
                [1] = new() { MissionId = 1, ConditionType = 2, ConditionCnt = 3, Point = 4 },
            });
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new() { RewardId = 1, RewardType = 2, RewardCnt = 10, MustPoint = 7 },
            });

        var cache = TestMasterCacheFactory.Create(playerRepo: _playerRepoMock.Object);

        var daily = await cache.GetDailyMissionMastAsync();
        var weekly = await cache.GetWeeklyRewardMastAsync();

        Assert.Equal(4, daily[1].Point);
        Assert.Equal(10, weekly[1].RewardCnt);
    }

    private static Dictionary<int, DailyMissionMastInfo> DailyMast(int point = 1)
        => Enumerable.Range(1, 11).ToDictionary(i => i, i => new DailyMissionMastInfo
        {
            MissionId = i,
            ConditionType = i,
            ConditionCnt = 1,
            Point = point,
        });

    private GameMoneyService BuildMoneyService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        return new GameMoneyService(_playerRepoMock.Object, new RatingService());
    }

    // ─── GetMissionListAsync ─────────────────────────────────────────────

    // シナリオ1: デイリーミッション 3件完了 → PointDayOwn=3
    [Fact]
    public async Task GetMissionListAsync_DailyMissions_CountsCompleted()
    {
        _playerRepoMock.Setup(r => r.GetDailyMissionListForTodayAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1 });
        _playerRepoMock.Setup(r => r.GetDailyMissionMastAsync())
            .ReturnsAsync(DailyMast());
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(5);
        _playerRepoMock.Setup(r => r.GetWeeklyRewardListForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>());

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var result = await svc.GetMissionListAsync(player);

        Assert.Equal(3, result.PointDayOwn);
        Assert.Equal(11, result.PointDayMax);
        Assert.Equal(5, result.PointWeekOwn);
    }

    // シナリオ2: 週間報酬ポイント不足 → weeklyRewards[i]=1 (MSN_RS_RCV)
    // 原典: nWeeklyPoint < stMjkWeeklyRewardMast.m_nMustPoint → MSN_RS_RCV
    [Fact]
    public async Task GetMissionListAsync_WeeklyPointInsufficient_Returns1()
    {
        _playerRepoMock.Setup(r => r.GetDailyMissionListForTodayAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _playerRepoMock.Setup(r => r.GetDailyMissionMastAsync())
            .ReturnsAsync(DailyMast());
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(0); // ポイントなし
        _playerRepoMock.Setup(r => r.GetWeeklyRewardListForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new WeeklyRewardMastInfo { RewardId = 1, MustPoint = 7, RewardType = 1, RewardCnt = 500 },
            });

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var result = await svc.GetMissionListAsync(player);

        Assert.Equal(1, result.WeeklyRewards[0]); // ID=1 → MSN_RS_RCV
    }

    // シナリオ3: 週間報酬ポイント十分 + 未受取 → weeklyRewards[i]=0
    [Fact]
    public async Task GetMissionListAsync_WeeklyPointSufficient_Unreceived_Returns0()
    {
        _playerRepoMock.Setup(r => r.GetDailyMissionListForTodayAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _playerRepoMock.Setup(r => r.GetDailyMissionMastAsync())
            .ReturnsAsync(DailyMast());
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(7); // 十分なポイント
        _playerRepoMock.Setup(r => r.GetWeeklyRewardListForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<int, int>()); // 受取記録なし
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new WeeklyRewardMastInfo { RewardId = 1, MustPoint = 7, RewardType = 1, RewardCnt = 500 },
            });

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var result = await svc.GetMissionListAsync(player);

        Assert.Equal(0, result.WeeklyRewards[0]); // 未受取
    }

    // ─── ReceiveWeeklyRewardAsync ────────────────────────────────────────

    // シナリオ4: マスター未定義 rewardId → false
    [Fact]
    public async Task ReceiveWeeklyRewardAsync_InvalidRewardId_ReturnsFalse()
    {
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>());
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(0);

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 1000 };
        var svc    = BuildService();

        var (ok, _, _, _) = await svc.ReceiveWeeklyRewardAsync(player, 99, BuildMoneyService());

        Assert.False(ok);
    }

    // シナリオ5: ポイント不足 → false (原典: Not Enough Point)
    [Fact]
    public async Task ReceiveWeeklyRewardAsync_NotEnoughPoint_ReturnsFalse()
    {
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new WeeklyRewardMastInfo { RewardId = 1, MustPoint = 10, RewardType = 1, RewardCnt = 500 },
            });
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(5); // 不足

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var (ok, _, _, _) = await svc.ReceiveWeeklyRewardAsync(player, 1, BuildMoneyService());

        Assert.False(ok);
    }

    // シナリオ6: 受取済み (MERGE 失敗) → false (原典: Already Received)
    [Fact]
    public async Task ReceiveWeeklyRewardAsync_AlreadyReceived_ReturnsFalse()
    {
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new WeeklyRewardMastInfo { RewardId = 1, MustPoint = 3, RewardType = 1, RewardCnt = 500 },
            });
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>())).ReturnsAsync(5);
        _playerRepoMock.Setup(r => r.GetWeeklyRewardStatusForWeekAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(1); // 既に受取済み

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var (ok, _, _, _) = await svc.ReceiveWeeklyRewardAsync(player, 1, BuildMoneyService());

        Assert.False(ok);
    }

    // シナリオ7: コイン報酬 (RewardType=1) → GamMoney 増加
    // 原典: case MSN_RT_COIN: pPlayer->AddGamMoney(...)
    [Fact]
    public async Task ReceiveWeeklyRewardAsync_CoinReward_IncreasesGamMoney()
    {
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new WeeklyRewardMastInfo { RewardId = 1, MustPoint = 3, RewardType = 1, RewardCnt = 500 },
            });
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>())).ReturnsAsync(5);
        _playerRepoMock.Setup(r => r.ReflectWeeklyRewardAsync(It.IsAny<MajakPlayer>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 1000 };
        var svc    = BuildService();

        var (ok, newMoney, _, _) = await svc.ReceiveWeeklyRewardAsync(player, 1, BuildMoneyService());

        Assert.True(ok);
        Assert.Equal(1500L, newMoney); // 1000 + 500
    }

    // シナリオ8: 宝石報酬 (RewardType=2) → GemCount 増加
    // 原典: case MSN_RT_GEM: pPlayer->m_nGemCount += stMjkWeeklyRewardMast.m_nRewardCnt
    [Fact]
    public async Task ReceiveWeeklyRewardAsync_GemReward_IncreasesGemCount()
    {
        _playerRepoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new WeeklyRewardMastInfo { RewardId = 1, MustPoint = 3, RewardType = 2, RewardCnt = 10 },
            });
        _playerRepoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>())).ReturnsAsync(5);
        _playerRepoMock.Setup(r => r.ReflectWeeklyRewardAsync(It.IsAny<MajakPlayer>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        _playerRepoMock.Setup(r => r.UpdateGemCountAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { MemberNo = "u1", GemCount = 5 };
        var svc    = BuildService();

        var (ok, _, gemCount, _) = await svc.ReceiveWeeklyRewardAsync(player, 1, BuildMoneyService());

        Assert.True(ok);
        Assert.Equal(15, gemCount); // 5 + 10
    }

    // ─── ReceiveSerialBonusAsync ────────────────────────────────────────

    // シナリオ9: 空のシリアルコード → result=0
    [Fact]
    public async Task ReceiveSerialBonusAsync_EmptyCode_Returns0()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var (result, _, _) = await svc.ReceiveSerialBonusAsync(player, "", BuildMoneyService());

        Assert.Equal(0, result);
    }

    // シナリオ10: 存在しないシリアルコード → result=0
    [Fact]
    public async Task ReceiveSerialBonusAsync_NotFoundCode_Returns0()
    {
        _playerRepoMock.Setup(r => r.GetSerialMastsAsync())
            .ReturnsAsync(new List<SerialMastInfo>());

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var (result, _, _) = await svc.ReceiveSerialBonusAsync(player, "INVALID", BuildMoneyService());

        Assert.Equal(0, result);
    }

    // シナリオ11: 使用済みシリアルコード → result=0
    // 原典: SelectEvtExchgItem → 既に使用済み
    [Fact]
    public async Task ReceiveSerialBonusAsync_AlreadyUsed_Returns0()
    {
        _playerRepoMock.Setup(r => r.GetSerialMastsAsync())
            .ReturnsAsync(new List<SerialMastInfo>
            {
                new() { GiftCode = "SERIAL01", EvtCode = "EVT01", EvtNo = 1, MissionNo = 1, GiftValue = 1000 }
            });
        _playerRepoMock.Setup(r => r.SerialExchangeItemExistsAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true); // 使用済み

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 500 };
        var svc    = BuildService();

        var (result, _, _) = await svc.ReceiveSerialBonusAsync(player, "SERIAL01", BuildMoneyService());

        Assert.Equal(0, result);
    }

    // シナリオ12: 正常受取 → result=1 + コイン付与 + メッセージ
    [Fact]
    public async Task ReceiveSerialBonusAsync_ValidCode_Returns1WithBonus()
    {
        _playerRepoMock.Setup(r => r.GetSerialMastsAsync())
            .ReturnsAsync(new List<SerialMastInfo>
            {
                new() { GiftCode = "SERIAL01", EvtCode = "EVT01", EvtNo = 1, MissionNo = 1, GiftValue = 1000, GiftMessage = "おめでとう！" }
            });
        _playerRepoMock.Setup(r => r.SerialExchangeItemExistsAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        _playerRepoMock.Setup(r => r.InsertSerialExchangeItemAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        _playerRepoMock.Setup(r => r.UpdateCommonRatSerialResourceAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 500 };
        var svc    = BuildService();

        var (result, newMoney, msg) = await svc.ReceiveSerialBonusAsync(
            player, "SERIAL01", BuildMoneyService());

        Assert.Equal(1, result);
        Assert.Equal(1500L, newMoney); // 500 + 1000
        Assert.Equal("おめでとう！", msg);
    }

    [Fact]
    public async Task ReceiveSerialBonusAsync_ItemReward_UsesLegacySell042ItemGrant()
    {
        _playerRepoMock.Setup(r => r.GetSerialMastsAsync())
            .ReturnsAsync(new List<SerialMastInfo>
            {
                new() { GiftCode = "ITEM01", EvtCode = "EVT01", EvtNo = 1, MissionNo = 3, GiftValue = 0, GiftMessage = "item" }
            });
        _playerRepoMock.Setup(r => r.SerialExchangeItemExistsAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        _playerRepoMock.Setup(r => r.AddSerialBonusItemAsync(It.IsAny<MajakPlayer>(), "MJ20", 0, 12))
            .ReturnsAsync(true);
        _playerRepoMock.Setup(r => r.InsertSerialExchangeItemAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        _playerRepoMock.Setup(r => r.UpdateCommonRatSerialResourceAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc = BuildService();

        var (result, _, msg) = await svc.ReceiveSerialBonusAsync(player, "ITEM01", BuildMoneyService());

        Assert.Equal(1, result);
        Assert.Equal("item", msg);
        _playerRepoMock.Verify(r => r.AddSerialBonusItemAsync(player, "MJ20", 0, 12), Times.Once);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// HangameCookieDecryptor 単体テスト
// 原典: Java LoginCookieEncryptor / HangameLoginCookieOrder
// ═══════════════════════════════════════════════════════════════════════════
public class HangameCookieDecryptorTests
{
    // ─── ParseCookie ────────────────────────────────────────────────────

    // シナリオ1: null → null
    [Fact]
    public void ParseCookie_Null_ReturnsNull()
        => Assert.Null(HangameCookieDecryptor.ParseCookie(null!));

    // シナリオ2: 空文字 → null
    [Fact]
    public void ParseCookie_Empty_ReturnsNull()
        => Assert.Null(HangameCookieDecryptor.ParseCookie(""));

    // シナリオ3: "hangame=" prefix がない → null
    [Fact]
    public void ParseCookie_NoPrefix_ReturnsNull()
        => Assert.Null(HangameCookieDecryptor.ParseCookie("some_random_value"));

    // シナリオ4: フィールド数不足 → null (フィールドが 28 未満)
    [Fact]
    public void ParseCookie_InsufficientFields_ReturnsNull()
    {
        // hangame= + CSV 5フィールド のみ (28 未満)
        var cookie = "hangame=" + Uri.EscapeDataString("a,b,c,d,e");
        Assert.Null(HangameCookieDecryptor.ParseCookie(cookie));
    }

    // シナリオ5: "hangametest=" prefix でも解析される
    [Fact]
    public void ParseCookie_HangameTestPrefix_ReturnsNull_WhenInsufficient()
    {
        var cookie = "hangametest=" + Uri.EscapeDataString("a,b,c");
        Assert.Null(HangameCookieDecryptor.ParseCookie(cookie));
    }

    // シナリオ6: password フィールドも LoginCookieEncryptor packString として復号される
    [Fact]
    public void ParseCookie_UnpacksPasswordField()
    {
        var values = Enumerable.Repeat("", HangameCookieDecryptor.FieldNames.Length).ToArray();
        values[0] = PackString("member1");
        values[1] = PackString("e13f23e28c4346c914224461075bc8f7");
        values[2] = PackString("name1");
        values[3] = "M";
        values[4] = "25";
        values[5] = "Y";
        values[6] = "avatar1";

        var cookie = "hangame=" + Uri.EscapeDataString(string.Join(',', values));
        var fields = HangameCookieDecryptor.ParseCookie(cookie);

        Assert.NotNull(fields);
        Assert.Equal("member1", fields!["userid"]);
        Assert.Equal("e13f23e28c4346c914224461075bc8f7", fields["password"]);
        Assert.Equal("name1", fields["name"]);
    }

    // シナリオ7: password が packed でない場合は不正クッキーとして扱う
    [Fact]
    public void ParseCookie_RawPassword_ReturnsNull()
    {
        var values = Enumerable.Repeat("", HangameCookieDecryptor.FieldNames.Length).ToArray();
        values[0] = PackString("member1");
        values[1] = "raw-password";
        values[2] = PackString("name1");

        var cookie = "hangame=" + Uri.EscapeDataString(string.Join(',', values));
        Assert.Null(HangameCookieDecryptor.ParseCookie(cookie));
    }

    // ─── GetUserId ──────────────────────────────────────────────────────

    // シナリオ8: null → null
    [Fact]
    public void GetUserId_Null_ReturnsNull()
        => Assert.Null(HangameCookieDecryptor.GetUserId(null!));

    // シナリオ9: 空 → null
    [Fact]
    public void GetUserId_Empty_ReturnsNull()
        => Assert.Null(HangameCookieDecryptor.GetUserId(""));

    // ─── TryUnpackString ────────────────────────────────────────────────

    // シナリオ10: 短すぎる文字列 (< 8 文字) → false
    [Fact]
    public void TryUnpackString_TooShort_ReturnsFalse()
    {
        bool ok = HangameCookieDecryptor.TryUnpackString("ABCD", out _);
        Assert.False(ok);
    }

    // シナリオ11: 不正なチェックサム → false
    [Fact]
    public void TryUnpackString_InvalidChecksum_ReturnsFalse()
    {
        // 末尾 4 文字が有効な hex でない
        bool ok = HangameCookieDecryptor.TryUnpackString("URnsURnsXXXX", out _);
        Assert.False(ok);
    }

    // シナリオ12: body 長さが 4 の倍数でない → false
    [Fact]
    public void TryUnpackString_NonMultipleOf4Body_ReturnsFalse()
    {
        // "URns" (4) + "URn" (3) + "0000" (4) = body は 7 (4の倍数でない)
        bool ok = HangameCookieDecryptor.TryUnpackString("URnURn0000", out _);
        Assert.False(ok);
    }

    // ─── FieldNames 定数確認 ────────────────────────────────────────────

    // シナリオ13: FieldNames に 28 フィールドが定義されていること
    [Fact]
    public void FieldNames_Has28Fields()
        => Assert.Equal(28, HangameCookieDecryptor.FieldNames.Length);

    // シナリオ14: FieldNames の先頭が "userid" であること
    [Fact]
    public void FieldNames_FirstField_IsUserId()
        => Assert.Equal("userid", HangameCookieDecryptor.FieldNames[0]);

    private static string PackString(string value)
    {
        var valueBytes = Encoding.GetEncoding(932).GetBytes(value);
        var stage1Size = ((1 + valueBytes.Length + 2) / 3) * 3;
        var stage1 = new List<byte>(stage1Size);
        byte random = 0x42;
        stage1.Add(random);
        foreach (var b in valueBytes)
        {
            random ^= b;
            stage1.Add(random);
        }
        while (stage1.Count < stage1Size)
            stage1.Add(random);

        const string charset = "URnsDa4jzCWrpP-hlt3M68OHfIXJZNGo7Ve_E2widBkcxqg51vmSKY0yAbFu9LTQ";
        var stage2 = new StringBuilder(stage1.Count / 3 * 4 + 4);
        for (var i = 0; i < stage1.Count; i += 3)
        {
            stage2.Append(charset[0x3f & (stage1[i] >> 2)]);
            stage2.Append(charset[(0x30 & (stage1[i] << 4)) | (0x0f & (stage1[i + 1] >> 4))]);
            stage2.Append(charset[(0x3c & (stage1[i + 1] << 2)) | (0x03 & (stage1[i + 2] >> 6))]);
            stage2.Append(charset[0x3f & stage1[i + 2]]);
        }

        var checksum = CheckSum(Encoding.ASCII.GetBytes(stage2.ToString()));
        stage2.Append(checksum.ToString("X4", CultureInfo.InvariantCulture));
        return stage2.ToString();
    }

    private static uint CheckSum(byte[] value)
    {
        const uint polynomial = 0x01102100;
        uint sum = 0;
        foreach (var b in value)
        {
            sum |= b;
            for (var i = 0; i < 8; i++)
            {
                sum <<= 1;
                if ((sum & 0x01000000) != 0)
                    sum ^= polynomial;
            }
        }

        for (var i = 0; i < 2; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                sum <<= 1;
                if ((sum & 0x01000000) != 0)
                    sum ^= polynomial;
            }
        }

        return (sum >> 8) & 0x0000ffff;
    }
}

public class AvatarCatalogTests
{
    [Theory]
    [InlineData("M", "m")]
    [InlineData("F", "f")]
    public void GetAvatars_ReturnsSixteenSexSpecificUrls(string sexCode, string suffix)
    {
        var avatars = AvatarCatalog.GetAvatars(sexCode);

        Assert.Equal(16, avatars.Count);
        Assert.Equal($"{AvatarCatalog.LocalBase}/thumbnail_01{suffix}.png", avatars[0]);
        Assert.Equal($"{AvatarCatalog.LocalBase}/thumbnail_16{suffix}.png", avatars[15]);
        Assert.All(avatars, avatar => Assert.True(AvatarCatalog.IsValid(sexCode, avatar)));
    }

    [Fact]
    public void IsValid_RejectsWrongSexAndUnknownUrl()
    {
        Assert.False(AvatarCatalog.IsValid("F", AvatarCatalog.GetAvatars("M")[0]));
        Assert.False(AvatarCatalog.IsValid("M", "https://example.com/avatar.png"));
        Assert.False(AvatarCatalog.IsValid("U", AvatarCatalog.GetAvatars("M")[0]));
    }
}
