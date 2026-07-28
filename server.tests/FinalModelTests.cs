using MajakServer.Engine;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// PlayerMode / RichiType / GameStatus enum テスト
// 原典: MajakDef.h MODE / RICHITYPE / GAMESTATUS enum
// ═══════════════════════════════════════════════════════════════════════════
public class EngineEnumTests
{
    // ─── PlayerMode ─────────────────────────────────────────────────────
    // 原典: MODE_NONE=0, MODE_TURN=1, MODE_FURO=2, MODE_CHAN=3, MODE_KYO=4, MODE_AGA=5

    [Fact] public void PlayerMode_None_Is0() => Assert.Equal(0, (int)PlayerMode.None);
    [Fact] public void PlayerMode_Turn_Is1() => Assert.Equal(1, (int)PlayerMode.Turn);
    [Fact] public void PlayerMode_Furo_Is2() => Assert.Equal(2, (int)PlayerMode.Furo);
    [Fact] public void PlayerMode_Chan_Is3() => Assert.Equal(3, (int)PlayerMode.Chan);
    [Fact] public void PlayerMode_Kyo_Is4()  => Assert.Equal(4, (int)PlayerMode.Kyo);
    [Fact] public void PlayerMode_Aga_Is5()  => Assert.Equal(5, (int)PlayerMode.Aga);

    // ─── RichiType ──────────────────────────────────────────────────────
    // 原典: NONE=0, RICHI=1, WRICHI=2

    [Fact] public void RichiType_None_Is0()   => Assert.Equal(0, (int)RichiType.None);
    [Fact] public void RichiType_Richi_Is1()  => Assert.Equal(1, (int)RichiType.Richi);
    [Fact] public void RichiType_Wrichi_Is2() => Assert.Equal(2, (int)RichiType.Wrichi);

    // ─── GameStatus ──────────────────────────────────────────────────────
    // 原典: GAMESTATUS_NOTPLAYING=0, GAMESTATUS_PLAYING=1, GAMESTATUS_NEWKYOKU=2, GAMESTATUS_ENDKYOKU=3

    [Fact] public void GameStatus_NotPlaying_Is0() => Assert.Equal(0, (int)GameStatus.NotPlaying);
    [Fact] public void GameStatus_Playing_Is1()    => Assert.Equal(1, (int)GameStatus.Playing);
    [Fact] public void GameStatus_NewKyoku_Is2()   => Assert.Equal(2, (int)GameStatus.NewKyoku);
    [Fact] public void GameStatus_EndKyoku_Is3()   => Assert.Equal(3, (int)GameStatus.EndKyoku);
}

// ═══════════════════════════════════════════════════════════════════════════
// RatingService テスト (残りの境界値)
// 原典: HMajRatingCommon.cpp s_llMajNLevel / s_nMajExperience
// ═══════════════════════════════════════════════════════════════════════════
public class RatingServiceRemainingTests
{
    private readonly RatingService _svc = new();

    // ─── GetNLevel 全境界値テスト ─────────────────────────────────────────
    // 原典: s_llMajNLevel[] = {0,1,500,1500,3000,10000,30000,100000,500000,1000000,5000000}

    [Theory]
    [InlineData(0L,       0)]   // 境界以下
    [InlineData(499L,     1)]   // 500未満
    [InlineData(500L,     2)]   // 境界
    [InlineData(1499L,    2)]
    [InlineData(1500L,    3)]
    [InlineData(2999L,    3)]
    [InlineData(3000L,    4)]
    [InlineData(9999L,    4)]
    [InlineData(10000L,   5)]
    [InlineData(29999L,   5)]
    [InlineData(30000L,   6)]
    [InlineData(99999L,   6)]
    [InlineData(100000L,  7)]
    [InlineData(499999L,  7)]
    [InlineData(500000L,  8)]
    [InlineData(999999L,  8)]
    [InlineData(1000000L, 9)]
    [InlineData(4999999L, 9)]
    [InlineData(5000000L, 10)]
    public void GetNLevel_Boundary(long money, int expected)
        => Assert.Equal(expected, _svc.GetNLevel(money));

    // ─── GetExperience の正しい公式を確認 ─────────────────────────────────
    // 原典: (nHoraSoten * 3 + nHojuSoten) / 100 【修正済み確認】

    // シナリオ: 和了点1000 + 放銃点0 → (1000*3+0)/100 = 30
    [Fact]
    public void GetExperience_CorrectFormula_Hora1000()
        => Assert.Equal(30, _svc.GetExperience(0, 1000, 0));

    // シナリオ: 和了点3000 + 放銃点1000 → (3000*3+1000)/100 = 100
    [Fact]
    public void GetExperience_CorrectFormula_Combined()
        => Assert.Equal(100, _svc.GetExperience(0, 3000, 1000));

    // シナリオ: 和了点0 + 放銃点0 → 0
    [Fact]
    public void GetExperience_Zero_Returns0()
        => Assert.Equal(0, _svc.GetExperience(0, 0, 0));

    // ─── CalcGradeRating テスト ─────────────────────────────────────────
    // 原典: fGetRate = fMatchCountCorrect * (nPointSum + fSelfCorrect) * SCALE 【修正済み確認】

    // シナリオ: matchCnt=400, pointSum=0, selfCorrect=0 → 0
    [Fact]
    public void CalcGradeRating_ZeroPointSum_Returns0()
    {
        int result = _svc.CalcGradeRating(1500, 0, 400, 1500);
        Assert.Equal(1500, result); // 変化なし
    }

    // シナリオ: matchCnt=0 → matchCorrect=1.0
    // fGetRate = 1.0 * (1000 + 0) * 0.5 = 500
    [Fact]
    public void CalcGradeRating_MatchCnt0_MatchCorrect1()
    {
        int result = _svc.CalcGradeRating(1500, 1000, 0, 1500);
        Assert.Equal(2000, result); // 1500 + 500
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PlayerRecord / CupRecord / UserCustomItem モデルテスト
// 原典: HMAJ_RATING_RECORD struct / MAJAKCUPRAT / MJK_USERCUSTOMITEM
// ═══════════════════════════════════════════════════════════════════════════
public class PlayerModelTests
{
    // ─── RatingRecord ────────────────────────────────────────────────────

    // シナリオ1: デフォルト値確認
    [Fact]
    public void RatingRecord_AllFieldsDefaultToZero()
    {
        var rec = new MajakServer.Models.Player.RatingRecord();
        Assert.Equal(0, rec.Rating);
        Assert.Equal(0, rec.MatchCnt);
        Assert.Equal(0, rec.WinCnt);
        Assert.Equal(0, rec.DefeatCnt);
        Assert.Equal(0, rec.HoraCnt);
        Assert.Equal(0, rec.HoraPoint);
        Assert.Equal(0, rec.HojuCnt);
        Assert.Equal(0, rec.RichiCnt);
        Assert.Equal(0, rec.FuroCnt);
        Assert.Equal(0, rec.TurnCnt);
        Assert.Equal(0, rec.KyokuCnt);
        Assert.Equal(0, rec.DoraCnt);
        Assert.Equal(0, rec.PointSum);
    }

    // シナリオ2: フィールド設定
    [Fact]
    public void RatingRecord_SetFields_Works()
    {
        var rec = new MajakServer.Models.Player.RatingRecord
        {
            Rating   = 1500,
            MatchCnt = 100,
            WinCnt   = 30,
            HoraCnt  = 50,
        };
        Assert.Equal(1500, rec.Rating);
        Assert.Equal(100,  rec.MatchCnt);
        Assert.Equal(30,   rec.WinCnt);
        Assert.Equal(50,   rec.HoraCnt);
    }

    // ─── CupRecord ───────────────────────────────────────────────────────

    // シナリオ3: CupRecord デフォルト
    [Fact]
    public void CupRecord_Default_ZeroFields()
    {
        var cup = new CupRecord();
        Assert.Equal(0, cup.CupPoint);
        Assert.Equal(0, cup.CupMatchCnt);
    }

    // シナリオ4: CupRecord 設定
    [Fact]
    public void CupRecord_SetFields_Works()
    {
        var cup = new CupRecord { CupPoint = 1200, CupMatchCnt = 5 };
        Assert.Equal(1200, cup.CupPoint);
        Assert.Equal(5,    cup.CupMatchCnt);
    }

    // ─── UserCustomItem ──────────────────────────────────────────────────

    // シナリオ5: UserCustomItem デフォルト
    [Fact]
    public void UserCustomItem_Default_ZeroEquip()
    {
        var item = new UserCustomItem();
        Assert.Equal(0, item.Equip);
        Assert.Equal(0, item.Kind);
    }

    // シナリオ6: 装備中フラグ
    [Fact]
    public void UserCustomItem_Equip1_IsEquipped()
    {
        var item = new UserCustomItem { Kind = 1, Equip = 1 };
        Assert.Equal(1, item.Equip);
        Assert.True(item.Equip == 1);
    }

    // ─── SkinInfo ────────────────────────────────────────────────────────

    // シナリオ7: SkinInfo デフォルト
    [Fact]
    public void SkinInfo_Default_Values()
    {
        var skin = new SkinInfo();
        Assert.Equal(0,    skin.SkinNo);
        Assert.False(skin.AttachFlag);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakPlayer.GetRichiEffect 詳細テスト
// 原典: HMajPlayer::GetRichiEffect — CAT_RICHI アイテムのサブコード返却
// ═══════════════════════════════════════════════════════════════════════════
public class MajakPlayerGetRichiEffectTests
{
    // シナリオ1: item001 使用中 → 1 (普通リーチ)
    [Fact]
    public void GetRichiEffect_Item001Active_Returns1()
    {
        var player = new MajakPlayer();
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item001",
            UseFlag  = true,
            EndDt    = DateTime.Now.AddDays(7),
            Qty      = 1,
        });
        Assert.Equal(1, player.GetRichiEffect());
    }

    // シナリオ2: item002 使用中 → 2 (重リーチ)
    [Fact]
    public void GetRichiEffect_Item002Active_Returns2()
    {
        var player = new MajakPlayer();
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item002",
            UseFlag  = true,
            EndDt    = DateTime.Now.AddDays(7),
            Qty      = 1,
        });
        Assert.Equal(2, player.GetRichiEffect());
    }

    // シナリオ3: item004 使用中 → 3 (一点リーチ)
    [Fact]
    public void GetRichiEffect_Item004Active_Returns3()
    {
        var player = new MajakPlayer();
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item004",
            UseFlag  = true,
            EndDt    = DateTime.Now.AddDays(7),
            Qty      = 1,
        });
        Assert.Equal(3, player.GetRichiEffect());
    }

    // シナリオ4: UseFlag=false → 0 (使用中でない)
    [Fact]
    public void GetRichiEffect_NotActive_Returns0()
    {
        var player = new MajakPlayer();
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item001",
            UseFlag  = false,   // 装備していない
            EndDt    = DateTime.Now.AddDays(7),
            Qty      = 1,
        });
        Assert.Equal(0, player.GetRichiEffect());
    }

    // シナリオ5: 該当アイテムなし → 0
    [Fact]
    public void GetRichiEffect_NoRelevantItem_Returns0()
    {
        var player = new MajakPlayer();
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item003", // item003 は非対象
            UseFlag  = true,
            EndDt    = DateTime.Now.AddDays(7),
            Qty      = 1,
        });
        Assert.Equal(0, player.GetRichiEffect());
    }

    // シナリオ6: MajItems が空 → 0
    [Fact]
    public void GetRichiEffect_EmptyList_Returns0()
    {
        var player = new MajakPlayer();
        Assert.Equal(0, player.GetRichiEffect());
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// EnginePlayer ResultRecord 統計トラッキングテスト
// 原典: HMajakGameLogic::ProcessHoraPlayer / ProcessEndKyoku
//   HoraCnt++ / HoraPoint += / HojuPoint += / DoraCnt +=
// ═══════════════════════════════════════════════════════════════════════════
public class EnginePlayerStatTrackingTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    // シナリオ1: ProcessEndKyoku → KyokuCnt++
    [Fact]
    public void ProcessEndKyoku_KyokuCntIncrement()
    {
        var logic  = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        int prev = logic.Player[0].ResultRecord.KyokuCnt;
        method.Invoke(logic, new object[] { false, false });

        Assert.Equal(prev + 1, logic.Player[0].ResultRecord.KyokuCnt);
    }

    // シナリオ2: ProcessEndKyoku → 全プレイヤー MODE_KYO に設定
    // 原典: pPlayer->SetMode(MODE_KYO)
    [Fact]
    public void ProcessEndKyoku_AllPlayersSetToModeKyo()
    {
        var logic  = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        method.Invoke(logic, new object[] { false, false });

        Assert.All(logic.Player, p => Assert.Equal(PlayerMode.Kyo, p.Mode));
    }

    // シナリオ3: ProcessEndKyoku → GameStatus=EndKyoku
    // 原典: m_eGameStatus = GAMESTATUS_ENDKYOKU
    [Fact]
    public void ProcessEndKyoku_SetsGameStatusEndKyoku()
    {
        var logic  = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        method.Invoke(logic, new object[] { true, false });

        Assert.Equal(GameStatus.EndKyoku, logic.GameStatus);
    }

    // シナリオ4: ProcessEndKyoku(hora=true) → EndKyokuWithHora=true
    // 原典: m_stKyokuInfo.m_bEndKyokuWithHora = bHora
    [Fact]
    public void ProcessEndKyoku_SetEndKyokuWithHora()
    {
        var logic  = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        method.Invoke(logic, new object[] { true, true });

        Assert.True(logic.KyokuInfo.EndKyokuWithHora);
        Assert.True(logic.KyokuInfo.Renchan);
    }

    // シナリオ5: ProcessEndKyoku(hora=false) → EndKyokuWithHora=false
    [Fact]
    public void ProcessEndKyoku_HoraFalse_SetsEndKyokuWithHoraFalse()
    {
        var logic  = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        method.Invoke(logic, new object[] { false, false });

        Assert.False(logic.KyokuInfo.EndKyokuWithHora);
        Assert.False(logic.KyokuInfo.Renchan);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// NLevel テーブル定数テスト
// 原典: HMajRatingCommon.cpp s_llMajNLevel[] テーブル値
// ═══════════════════════════════════════════════════════════════════════════
public class NLevelTableTests
{
    private readonly RatingService _svc = new();

    // 境界値: ちょうど閾値に達した場合の NLevel
    // 原典: s_llMajNLevel[] = {0,1,500,1500,3000,10000,30000,100000,500000,1000000,5000000}

    [Fact] public void NLevel_At0_Is0()       => Assert.Equal(0,  _svc.GetNLevel(0L));
    [Fact] public void NLevel_At1_Is1()       => Assert.Equal(1,  _svc.GetNLevel(1L));
    [Fact] public void NLevel_At500_Is2()     => Assert.Equal(2,  _svc.GetNLevel(500L));
    [Fact] public void NLevel_At1500_Is3()    => Assert.Equal(3,  _svc.GetNLevel(1500L));
    [Fact] public void NLevel_At3000_Is4()    => Assert.Equal(4,  _svc.GetNLevel(3000L));
    [Fact] public void NLevel_At10000_Is5()   => Assert.Equal(5,  _svc.GetNLevel(10000L));
    [Fact] public void NLevel_At30000_Is6()   => Assert.Equal(6,  _svc.GetNLevel(30000L));
    [Fact] public void NLevel_At100000_Is7()  => Assert.Equal(7,  _svc.GetNLevel(100000L));
    [Fact] public void NLevel_At500000_Is8()  => Assert.Equal(8,  _svc.GetNLevel(500000L));
    [Fact] public void NLevel_At1000000_Is9() => Assert.Equal(9,  _svc.GetNLevel(1000000L));
    [Fact] public void NLevel_At5000000_Is10()=> Assert.Equal(10, _svc.GetNLevel(5000000L));

    // 最大値以上は 10
    [Fact] public void NLevel_Huge_Is10()     => Assert.Equal(10, _svc.GetNLevel(999999999L));
}

// ═══════════════════════════════════════════════════════════════════════════
// GameConst 追加定数テスト
// 原典: HMajDef.h
// ═══════════════════════════════════════════════════════════════════════════
public class GameConstAdditionalTests
{
    // 原典: ALLINMONEY = 1000
    [Fact] public void AllinMoney_Is1000()         => Assert.Equal(1000L, GameConst.AllinMoney);
    // 原典: ALLINMONEY_OVER_2_DAN = 2000
    [Fact] public void AllinMoney2Dan_Is2000()     => Assert.Equal(2000L, GameConst.AllinMoney2Dan);
    // 原典: ALLINCOUNT_MAX = 1
    [Fact] public void AllinCountMax_Is1()         => Assert.Equal(1, GameConst.AllinCountMax);
    // 原典: ALLINCOUNT_MAX_NETCAFE = 2
    [Fact] public void AllinCountMaxNetCafe_Is2()  => Assert.Equal(2, GameConst.AllinCountMaxNetCafe);
    // 原典: YAKUMANBONUS_MONEY = 200
    [Fact] public void YakumanBonusMoney_Is200()   => Assert.Equal(200L, GameConst.YakumanBonusMoney);
    // 原典: DEFAULT_MONEY = 1000
    [Fact] public void DefaultMoney_Is1000()       => Assert.Equal(1000L, GameConst.DefaultMoney);
    // 原典: RATING_GRADE_MODE_INIT = 1500
    [Fact] public void RatingGradeModeInit_Is1500()=> Assert.Equal(1500, GameConst.RatingGradeModeInit);
    // 原典: MEMBER_ID_LEN = 24
    [Fact] public void MemberNoLen_Is24()          => Assert.Equal(24, GameConst.MemberNoLen);
    // 原典: PLAYER_MAX_COUNT = 4 (GameConst 版)
    [Fact] public void PlayerMaxCount_Is4()        => Assert.Equal(4, GameConst.PlayerMaxCount);
}
