using MajakServer.Engine;
using MajakServer.Models.Player;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// Bipai (牌山) テスト
// 原典: CBipai.cpp — Init / Chipai / GetBipaiCount / GetDoraDisplay
// ═══════════════════════════════════════════════════════════════════════════
public class BipaiTests
{
    // ─── Init ────────────────────────────────────────────────────────────────

    // シナリオ1: Init(0,0) → 牌山に136枚の牌があること
    // 原典: BIPAI_MAX_COUNT=136, 全4色34種
    [Fact]
    public void Init_NoRed_Creates136Tiles()
    {
        var bipai = new Bipai();
        bipai.Init(nRed: 0, nHua: 0);

        var buf = new PaiCode[136];
        bipai.GetBipai(buf);

        Assert.Equal(136, buf.Length);
        Assert.All(buf, p => Assert.True(p.IsValid));
    }

    // シナリオ2: Init 後は全 BipaiIndex が 0-135 の一意な値
    [Fact]
    public void Init_AllBipaiIndexUnique()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);

        var buf = new PaiCode[136];
        bipai.GetBipai(buf);

        var indices = buf.Select(p => p.BipaiIndex).OrderBy(x => x).ToArray();
        Assert.Equal(Enumerable.Range(0, 136).ToArray(), indices);
    }

    // シナリオ3: Init(1,0) → 赤牌が設定されること
    // 原典: case1: Pin-5赤 + Man-5赤 + Sou-5赤 + Pin-5赤(2枚目)
    [Fact]
    public void Init_Red1_SetsRedTiles()
    {
        var bipai = new Bipai();
        bipai.Init(nRed: 1, nHua: 0);

        var buf = new PaiCode[136];
        bipai.GetBipai(buf);
        int redCount = buf.Count(p => p.IsRed);
        Assert.True(redCount > 0, "赤牌が存在すること");
    }

    // シナリオ4: nRed=1 と nRed=2 の赤牌数の違いを確認
    // 原典: switch で case1 が case2 にフォールスルー
    //   nRed=1 → Pin-5(コピー1) + Man-5 + Sou-5 + Pin-5(コピー0) = 4枚
    //   nRed=2 → Man-5 + Sou-5 + Pin-5(コピー0) = 3枚
    [Fact]
    public void Init_Red1_HasMoreRedThan_Red2()
    {
        var bipai1 = new Bipai(); bipai1.Init(nRed: 1, nHua: 0);
        var bipai2 = new Bipai(); bipai2.Init(nRed: 2, nHua: 0);

        var buf1 = new PaiCode[136]; bipai1.GetBipai(buf1);
        var buf2 = new PaiCode[136]; bipai2.GetBipai(buf2);

        int red1 = buf1.Count(p => p.IsRed);
        int red2 = buf2.Count(p => p.IsRed);
        // 原典: case1(fall-through) = 4枚、case2 = 3枚
        Assert.Equal(4, red1);
        Assert.Equal(3, red2);
    }

    // ─── Chipai (シャッフル) ─────────────────────────────────────────────────

    // シナリオ5: Chipai 後も136枚
    [Fact]
    public void Chipai_StillHas136Tiles()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.Chipai();

        var buf = new PaiCode[136];
        bipai.GetBipai(buf);
        Assert.Equal(136, buf.Length);
        Assert.All(buf, p => Assert.True(p.IsValid));
    }

    // シナリオ6: Chipai 後の BipaiIndex は 0-135 の一意
    [Fact]
    public void Chipai_AllBipaiIndexUnique()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.Chipai();

        var buf = new PaiCode[136];
        bipai.GetBipai(buf);
        var indices = buf.Select(p => p.BipaiIndex).OrderBy(x => x).ToArray();
        Assert.Equal(Enumerable.Range(0, 136).ToArray(), indices);
    }

    // シナリオ7: Chipai は各Serial が4枚ずつ存在
    // 原典: 34種 × 4枚 = 136枚
    [Fact]
    public void Chipai_EachSerialAppears4Times()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.Chipai();

        var buf = new PaiCode[136];
        bipai.GetBipai(buf);

        var counts = new int[34];
        foreach (var p in buf) counts[p.GetSerial()]++;
        Assert.All(counts, c => Assert.Equal(4, c));
    }

    // ─── GetBipaiCount ───────────────────────────────────────────────────────

    // シナリオ8: Init 直後のカウント
    // 原典: BIPAI_MAX_COUNT - WANPAI_COUNT - BipPtr - RinPtr = 136 - 14 - 0 - 0 = 122
    [Fact]
    public void GetBipaiCount_AfterInit_Is122()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.Chipai();
        Assert.Equal(136 - MajakConst.WanpaiCount, bipai.GetBipaiCount());
    }

    [Fact]
    public void Open_ClearsSentMask_ForPublicPaiInfoResend()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.Chipai();

        var tile = bipai.GetNextTsumo(memberOrder: 1);
        var firstInfo = BipaiInfo.Create();
        bipai.GetPaiInfo(ref firstInfo, openMask: 0b1_1111, skipMask: 1 << 0);
        Assert.Contains(firstInfo.Pai.Take(firstInfo.PaiCnt), pai => pai.BipaiIndex == tile.BipaiIndex);

        bipai.Open(tile.BipaiIndex);
        var secondInfo = BipaiInfo.Create();
        bipai.GetPaiInfo(ref secondInfo, openMask: 1 << 0, skipMask: 1 << 0);

        Assert.Contains(secondInfo.Pai.Take(secondInfo.PaiCnt), pai => pai.BipaiIndex == tile.BipaiIndex);
    }

    // ─── GetDoraDisplay ──────────────────────────────────────────────────────

    // シナリオ9: GetDoraDisplay(0, false) は有効な牌
    [Fact]
    public void GetDoraDisplay_ReturnsValidTile()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.Chipai();

        var dora = bipai.GetDoraDisplay(0, false);
        Assert.True(dora.IsValid);
    }

    // シナリオ10: ドラ表示牌と裏ドラ表示牌は異なる位置
    [Fact]
    public void GetDoraDisplay_Ura_DifferentFromOmote()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.Chipai();

        var dora  = bipai.GetDoraDisplay(0, false);
        var ura   = bipai.GetDoraDisplay(0, true);
        // BipaiIndex が異なることを確認 (同じ牌になる可能性は低い)
        // 位置が異なることを確認
        int idxDora = bipai.GetDoraIdx(0, false);
        int idxUra  = bipai.GetDoraIdx(0, true);
        Assert.NotEqual(idxDora, idxUra);
    }

    [Fact]
    public void GetDoraIdx_UsesLegacyOpenIdxAndRinReserveFormula()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.SetOpenIdx(20);

        Assert.Equal(14, bipai.GetDoraIdx(0, false));
        Assert.Equal(15, bipai.GetDoraIdx(0, true));
        Assert.Equal(12, bipai.GetDoraIdx(1, false));
    }

    [Fact]
    public void GetNextTsumoAndRinshan_UseLegacyPointersAndOpenForMember()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.SetOpenIdx(20);

        var tsumo = bipai.GetNextTsumo(2);
        var rinshan = bipai.GetNextRinshan(3);

        Assert.Equal(20, tsumo.BipaiIndex);
        Assert.Equal(18, rinshan.BipaiIndex);
        Assert.Equal(120, bipai.GetBipaiCount());
    }

    [Fact]
    public void GetPaiInfo_SendsVisibleTilesOncePerSkipMask()
    {
        var bipai = new Bipai();
        bipai.Init(0, 0);
        bipai.SetOpenIdx(0);
        var tsumo = bipai.GetNextTsumo(1);

        var first = BipaiInfo.Create();
        bipai.GetPaiInfo(ref first, openMask: 1 << 1, skipMask: 1 << 1);
        var second = BipaiInfo.Create();
        bipai.GetPaiInfo(ref second, openMask: 1 << 1, skipMask: 1 << 1);

        Assert.Equal(1, first.PaiCnt);
        Assert.Equal(tsumo.BipaiIndex, first.Pai[0].BipaiIndex);
        Assert.Equal(0, second.PaiCnt);
    }

    [Fact]
    public void SetBipai_RotatesIntoHaipaiPositionAndReindexes()
    {
        var src = new PaiCode[136];
        for (int i = 0; i < src.Length; i++) src[i] = PaiCode.MakeSerial(i % 34);
        var bipai = new Bipai();
        bipai.SetBipai(src, 20);

        var buf = new PaiCode[136];
        bipai.GetBipai(buf);

        Assert.Equal(src[0].Code, buf[20].Code);
        Assert.Equal(src[115].Code, buf[135].Code);
        Assert.Equal(src[116].Code, buf[0].Code);
        Assert.Equal(20, buf[20].BipaiIndex);
        Assert.Equal(0, buf[0].BipaiIndex);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// EnginePlayer テスト
// 原典: HMajakPlayer (InitHanchan / InitKyoku / 各操作)
// ═══════════════════════════════════════════════════════════════════════════
public class EnginePlayerTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0, Tip = false,
    };

    // ─── InitHanchan ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsLegacyInitialFlags()
    {
        var p = new EnginePlayer();

        Assert.True(p.IsMenzen);
        Assert.True(p.IsNagashiMangan);
        Assert.Equal(MajakConst.InvalidOrder, p.PaoOrder);
    }

    // シナリオ1: InitHanchan → GamePoint=25000 (原典: DEFAULT_GAMEPOINT)
    [Fact]
    public void InitHanchan_GamePointIsDefault()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        Assert.Equal(MajakConst.DefaultGamePoint, p.GamePoint);
    }

    // シナリオ2: InitHanchan → Yakitori rule enabled sets IsYakitori=true
    [Fact]
    public void InitHanchan_YakitoriRuleTrue_IsYakitoriTrue()
    {
        var p = new EnginePlayer();
        p.InitHanchan(1, DefaultRule() with { Yakitori = true });
        Assert.True(p.IsYakitori);
    }

    [Fact]
    public void InitHanchan_YakitoriRuleFalse_IsYakitoriFalse()
    {
        var p = new EnginePlayer();
        p.InitHanchan(1, DefaultRule() with { Yakitori = false });
        Assert.False(p.IsYakitori);
    }

    // シナリオ3: InitHanchan → Tip=DEFAULT_TIP
    // 原典: m_nTip = DEFAULT_TIP
    [Fact]
    public void InitHanchan_WithTip_SetsTip()
    {
        var rule = DefaultRule() with { Tip = true };
        var p = new EnginePlayer();
        p.InitHanchan(0, rule);
        Assert.Equal(MajakConst.DefaultTip, p.Tip);
    }

    // シナリオ4: InitHanchan + Tip=false still initializes legacy default Tip
    [Fact]
    public void InitHanchan_WithoutTip_StillSetsDefaultTipLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        Assert.Equal(MajakConst.DefaultTip, p.Tip);
    }

    // ─── InitKyoku ───────────────────────────────────────────────────────────

    // シナリオ5: InitKyoku → 手牌クリア
    [Fact]
    public void InitKyoku_ClearsTehaiAndFuro()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.Tehai.Add(PaiCode.MakeSerial(0)); // 手牌に追加
        p.InitKyoku();
        Assert.Empty(p.Tehai);
        Assert.Empty(p.Furo);
        Assert.Empty(p.Sutehai);
    }

    // シナリオ6: InitKyoku → IsMenzen=true, RichiType=None
    [Fact]
    public void InitKyoku_IsMenzenTrueAndRichiNone()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        Assert.True(p.IsMenzen);
        Assert.Equal(RichiType.None, p.RichiType);
    }

    // シナリオ7: InitKyoku → IsNagashiMangan=true (初期値)
    [Fact]
    public void InitKyoku_IsNagashiManganTrue()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        Assert.True(p.IsNagashiMangan);
    }

    // ─── Tsumo / Tapai ───────────────────────────────────────────────────────

    // シナリオ8: Tsumo → 手牌に追加
    [Fact]
    public void Tsumo_AddsTileToTehai()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var pai = PaiCode.MakeSerial(0);
        pai.BipaiIndex = 5;
        p.Tsumo(pai);
        Assert.Single(p.Tehai);
    }

    // シナリオ9: Tapai → 手牌から除去 + 捨て牌に追加
    [Fact]
    public void Tapai_RemovesFromTehaiAddsToSutehai()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var pai = PaiCode.MakeSerial(0);
        pai.BipaiIndex = 5;
        p.Tsumo(pai);
        var result = p.Tapai(pai);
        Assert.Equal(ActionResult.Ok, result);
        Assert.Empty(p.Tehai);
        Assert.Single(p.Sutehai);
    }

    // シナリオ10: Tapai 存在しない牌 → ErrPaiNotFoundInHand
    [Fact]
    public void Tapai_PaiNotInHand_ReturnsError()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var pai = PaiCode.MakeSerial(0);
        pai.BipaiIndex = 5;
        var result = p.Tapai(pai);
        Assert.Equal(ActionResult.ErrPaiNotFoundInHand, result);
    }

    [Fact]
    public void Tapai_ClearsIppatsuLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var pai = PaiCode.MakeSerial(0);
        pai.BipaiIndex = 5;
        p.Tsumo(pai);
        p.IsIppatsu = true;

        p.Tapai(pai);

        Assert.False(p.IsIppatsu);
    }

    [Fact]
    public void Tapai_PreservesTempFuritenAfterRichiLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var pai = PaiCode.MakeSerial(0);
        pai.BipaiIndex = 5;
        p.Tsumo(pai);
        p.SetTempFuriten();
        p.RichiType = RichiType.Richi;

        p.Tapai(pai);

        Assert.True(p.CheckFuriten());
    }

    [Fact]
    public void Tapai_SortsRemainingTehaiByCodeLikeLegacyRemoveTehai()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var nineMan = PaiCode.MakeSerial(8);
        nineMan.BipaiIndex = 1;
        var oneMan = PaiCode.MakeSerial(0);
        oneMan.BipaiIndex = 2;
        var fiveMan = PaiCode.MakeSerial(4);
        fiveMan.BipaiIndex = 3;
        p.Tehai.Add(nineMan);
        p.Tehai.Add(oneMan);
        p.Tehai.Add(fiveMan);

        p.Tapai(fiveMan);

        Assert.Equal(new[] { oneMan.Code, nineMan.Code }, p.Tehai.Select(tile => tile.Code).ToArray());
    }

    // ─── SetRichi ────────────────────────────────────────────────────────────

    // シナリオ11: SetRichi → GamePoint-=1000, RichiType=Richi
    // 原典: pPlayer->m_nGamePoint -= 1000; pPlayer->m_eRichiType = RICHI_RICHI
    [Fact]
    public void SetRichi_DeductsGamePointAndSetsType()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        int prevPoint = p.GamePoint;
        p.SetRichi(isFirstTurn: false);
        Assert.Equal(prevPoint - 1000, p.GamePoint);
        Assert.Equal(RichiType.Richi, p.RichiType);
    }

    // シナリオ12: SetRichi(firstTurn=true) → RichiType=Wrichi
    [Fact]
    public void SetRichi_FirstTurn_SetsWrichi()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        p.SetRichi(isFirstTurn: true);
        Assert.Equal(RichiType.Wrichi, p.RichiType);
    }

    // ─── ClearYakitori ───────────────────────────────────────────────────────

    // シナリオ13: ClearYakitori → IsYakitori=false
    [Fact]
    public void ClearYakitori_SetsToFalse()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule() with { Yakitori = true });
        Assert.True(p.IsYakitori);
        p.ClearYakitori();
        Assert.False(p.IsYakitori);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RatingService 追加テスト
// 原典: HMajRatingCommon.cpp
// ═══════════════════════════════════════════════════════════════════════════
public class RatingServiceAdvancedTests
{
    private readonly RatingService _svc = new();

    // ─── GetMoneyByRating ────────────────────────────────────────────────────
    // 原典: HMajRatingCommon::GetMoneyByRating
    //   nRating <= 0 → 0
    //   nRating < 1400 → 100000 * pow(2, (nRating-1400)/100)
    //   nRating >= 1400 → 100000 + 100000 * pow(2, (nRating-1500)/100)

    // シナリオ1: Rating=0 → 0
    [Fact]
    public void GetMoneyByRating_Zero_Returns0()
        => Assert.Equal(0L, _svc.GetMoneyByRating(0));

    // シナリオ2: Rating負値 → 0
    [Fact]
    public void GetMoneyByRating_Negative_Returns0()
        => Assert.Equal(0L, _svc.GetMoneyByRating(-100));

    // シナリオ3: Rating=1400 → 100000 (境界値)
    // 原典: >=1400 branch: 100000 + 100000 * pow(2, (1400-1500)/100) = 100000 + 100000*2^(-1) = 150000
    [Fact]
    public void GetMoneyByRating_1400_Returns150000()
    {
        long money = _svc.GetMoneyByRating(1400);
        Assert.True(money > 0);
        // 約150000
        Assert.InRange(money, 140000L, 160000L);
    }

    // シナリオ4: Rating=1500 → 200000 (基準値)
    // 原典: 100000 + 100000 * pow(2, 0) = 200000
    [Fact]
    public void GetMoneyByRating_1500_Returns200000()
    {
        long money = _svc.GetMoneyByRating(1500);
        Assert.Equal(200000L, money);
    }

    // シナリオ5: 高レーティングほど多くのコイン
    [Fact]
    public void GetMoneyByRating_Higher_ReturnsMore()
    {
        long m1 = _svc.GetMoneyByRating(1400);
        long m2 = _svc.GetMoneyByRating(1500);
        long m3 = _svc.GetMoneyByRating(1600);
        Assert.True(m1 < m2);
        Assert.True(m2 < m3);
    }

    // ─── GetExperience ───────────────────────────────────────────────────────
    // 原典: nGetExperience = (nHoraSoten * 3 + nHojuSoten) / 100
    //   【レガシーバグ修正済み】旧実装: (horaSoten/100) + (hojuSoten/100) ← 誤り

    // シナリオ6: 和了点1000 + 放銃点0 → (1000*3+0)/100 = 30
    [Fact]
    public void GetExperience_HoraOnly_Correct()
    {
        int add = _svc.GetExperience(0, horaSoten: 1000, hojuSoten: 0);
        Assert.Equal(30, add); // 原典: (1000*3+0)/100 = 30
    }

    // シナリオ7: 和了点0 + 放銃点300 → (0*3+300)/100 = 3
    [Fact]
    public void GetExperience_HojuOnly_Correct()
    {
        int add = _svc.GetExperience(0, horaSoten: 0, hojuSoten: 300);
        Assert.Equal(3, add); // 原典: (0+300)/100 = 3
    }

    // シナリオ8: 和了点2000 + 放銃点500 → (2000*3+500)/100 = 65
    [Fact]
    public void GetExperience_Combined_Correct()
    {
        int add = _svc.GetExperience(0, horaSoten: 2000, hojuSoten: 500);
        Assert.Equal(65, add); // (2000*3+500)/100 = 6500/100 = 65
    }

    // シナリオ9: 旧実装との違いを確認 (修正前は10+5=15, 修正後は65)
    // 原典の式が正しいことを確認するための回帰テスト
    [Fact]
    public void GetExperience_NotUsingOldFormula()
    {
        // horaSoten=2000, hojuSoten=500:
        // 旧 (誤): (2000/100) + (500/100) = 20 + 5 = 25
        // 新 (正): (2000*3+500)/100 = 65
        int add = _svc.GetExperience(0, horaSoten: 2000, hojuSoten: 500);
        Assert.NotEqual(25, add); // 旧実装ではない
        Assert.Equal(65, add);   // 正しい値
    }

    // シナリオ10: 経験値0 + 0点 → 0
    [Fact]
    public void GetExperience_ZeroSoten_Returns0()
    {
        int add = _svc.GetExperience(0, horaSoten: 0, hojuSoten: 0);
        Assert.Equal(0, add);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RatingRecord テスト
// 原典: HMajakPlayer m_pstRecord 関連
// ═══════════════════════════════════════════════════════════════════════════
public class RatingRecordTests
{
    // シナリオ1: CreateEmpty → 全フィールドが0
    [Fact]
    public void CreateEmpty_AllFieldsAreZero()
    {
        var rec = MajakServer.Engine.RatingRecord.CreateEmpty();
        Assert.Equal(0, rec.Rating);
        Assert.Equal(0, rec.MatchCnt);
        Assert.Equal(0, rec.WinCnt);
        Assert.Equal(0, rec.DefeatCnt);
        Assert.Equal(0, rec.DrawCnt);
        Assert.Equal(0, rec.HoraCnt);
        Assert.Equal(0, rec.RichiCnt);
        Assert.Equal(0, rec.PointSum);
    }

    // シナリオ2: CreateEmpty × 2 は別インスタンス
    [Fact]
    public void CreateEmpty_TwoInstances_AreIndependent()
    {
        var r1 = MajakServer.Engine.RatingRecord.CreateEmpty();
        var r2 = MajakServer.Engine.RatingRecord.CreateEmpty();
        r1.Rating = 999;
        Assert.Equal(0, r2.Rating);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// FuroBlock テスト
// 原典: HMajakPlayer 副露ブロック管理
// ═══════════════════════════════════════════════════════════════════════════
public class FuroBlockTests
{
    // シナリオ1: IsKan → Kan/Ank/Cha のみ true
    [Theory]
    [InlineData(Act.Kan, true)]
    [InlineData(Act.Ank, true)]
    [InlineData(Act.Cha, true)]
    [InlineData(Act.Pon, false)]
    [InlineData(Act.Chi, false)]
    [InlineData(Act.Shu, false)]
    public void FuroBlock_IsKan_ByAct(Act act, bool expected)
    {
        var furo = new FuroBlock { Act = act };
        Assert.Equal(expected, furo.IsKan());
    }

    // シナリオ2: IsKou → Shu/Chi 以外は true
    [Theory]
    [InlineData(Act.Pon, true)]
    [InlineData(Act.Kan, true)]
    [InlineData(Act.Ank, true)]
    [InlineData(Act.Shu, false)]
    [InlineData(Act.Chi, false)]
    public void FuroBlock_IsKou_ByAct(Act act, bool expected)
    {
        var furo = new FuroBlock { Act = act };
        Assert.Equal(expected, furo.IsKou());
    }

    // シナリオ3: IsShu → Shu/Chi のみ true
    [Theory]
    [InlineData(Act.Shu, true)]
    [InlineData(Act.Chi, true)]
    [InlineData(Act.Pon, false)]
    [InlineData(Act.Kan, false)]
    public void FuroBlock_IsShu_ByAct(Act act, bool expected)
    {
        var furo = new FuroBlock { Act = act };
        Assert.Equal(expected, furo.IsShu());
    }
}
