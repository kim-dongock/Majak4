using MajakServer.Engine;
using System.Reflection;

namespace MajakServer.Tests;

/// <summary>
/// エンジン追加テスト — 原典: CPaiCode.cpp / HMajakYaku.h / MajakDef.h
/// </summary>

// ═══════════════════════════════════════════════════════════════════════════
// MajakConst 定数テスト
// 原典: MajakDef.h BIPAI_MAX_COUNT / TEHAI_COUNT / KAESHIPOINT 等
// ═══════════════════════════════════════════════════════════════════════════
public class MajakConstTests
{
    // 原典: #define BIPAI_MAX_COUNT 136
    [Fact] public void BipaiMaxCount_Is136()      => Assert.Equal(136, MajakConst.BipaiMaxCount);
    // 原典: #define WANPAI_COUNT 14
    [Fact] public void WanpaiCount_Is14()         => Assert.Equal(14, MajakConst.WanpaiCount);
    // 原典: #define TEHAI_COUNT 13
    [Fact] public void TehaiCount_Is13()          => Assert.Equal(13, MajakConst.TehaiCount);
    // 原典: #define PLAYER_MAX_COUNT 4
    [Fact] public void PlayerMaxCount_Is4()       => Assert.Equal(4, MajakConst.PlayerMaxCount);
    // 原典: #define DICE_COUNT 2
    [Fact] public void DiceCount_Is2()            => Assert.Equal(2, MajakConst.DiceCount);
    // 原典: #define DORA_MAX_COUNT 5
    [Fact] public void DoraMaxCount_Is5()         => Assert.Equal(5, MajakConst.DoraMaxCount);
    // 原典: #define KAESHIPOINT 30000
    [Fact] public void KaeshiPoint_Is30000()      => Assert.Equal(30000, MajakConst.KaeshiPoint);
    // 原典: #define DEFAULT_GAMEPOINT 25000
    [Fact] public void DefaultGamePoint_Is25000() => Assert.Equal(25000, MajakConst.DefaultGamePoint);
    // 原典: #define DEFAULT_TIP 20
    [Fact] public void DefaultTip_Is20()          => Assert.Equal(20, MajakConst.DefaultTip);
    // 原典: #define INVALID_ORDER -1
    [Fact] public void InvalidOrder_IsMinusOne()  => Assert.Equal(-1, MajakConst.InvalidOrder);
}

// ═══════════════════════════════════════════════════════════════════════════
// PaiCode 追加テスト
// 原典: CPaiCode.cpp — GetNextNumberPai / IsYaochupai / IsGreen etc.
// ═══════════════════════════════════════════════════════════════════════════
public class PaiCodeExtraTests
{
    private static PaiCode MakeRawPaiCode(int code)
    {
        return (PaiCode)typeof(PaiCode)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null)!
            .Invoke(new object[] { code });
    }

    // ─── GetNextNumberPai ─────────────────────────────────────────────────
    // 原典: CPaiCode::GetNextNumberPai
    //   North(0x34) → East(0x31)
    //   Chun(0x37) → Haku(0x35)
    //   9m → 1m (wrapped)

    // シナリオ1: 1m (serial 0) → 2m (serial 1)
    [Fact]
    public void GetNextNumberPai_1m_Returns2m()
    {
        var pai = PaiCode.MakeSerial(0); // 1m
        var next = pai.GetNextNumberPai();
        Assert.Equal(1, next.GetSerial()); // 2m
    }

    // シナリオ2: 9m (serial 8) → 1m (serial 0) — wrap around
    // 原典: GetNumber()==9 → m_nCode & 0x31
    [Fact]
    public void GetNextNumberPai_9m_Returns1m()
    {
        var pai = PaiCode.MakeSerial(8); // 9m
        var next = pai.GetNextNumberPai();
        Assert.Equal(0, next.GetSerial()); // 1m
    }

    // シナリオ3: North(serial 30) → East(serial 27)
    // 原典: m_nCode==0x34 → return 0x31 (East)
    [Fact]
    public void GetNextNumberPai_North_ReturnsEast()
    {
        var pai = PaiCode.MakeSerial(30); // North = serial 27+3 = 30
        var next = pai.GetNextNumberPai();
        Assert.Equal(27, next.GetSerial()); // East = serial 27
    }

    // シナリオ4: Chun(serial 33) → Haku(serial 31)
    // 原典: m_nCode==0x37 → return 0x35 (Haku)
    [Fact]
    public void GetNextNumberPai_Chun_ReturnsHaku()
    {
        var pai = PaiCode.MakeSerial(33); // Chun
        var next = pai.GetNextNumberPai();
        Assert.Equal(31, next.GetSerial()); // Haku
    }

    // シナリオ5: 9s (serial 17) → 1s (serial 9) — wrap
    [Fact]
    public void GetNextNumberPai_9s_Returns1s()
    {
        var pai = PaiCode.MakeSerial(17); // 9s
        var next = pai.GetNextNumberPai();
        Assert.Equal(9, next.GetSerial()); // 1s
    }

    // ─── IsYaochupai ──────────────────────────────────────────────────────
    // 原典: IsTsupai() || (m_nCode & 0x07) == 1

    // シナリオ6: 1m → Yaochupai (端牌)
    [Fact]
    public void IsYaochupai_1m_IsTrue() => Assert.True(PaiCode.MakeSerial(0).IsYaochupai);
    // シナリオ7: 9m → Yaochupai
    [Fact]
    public void IsYaochupai_9m_IsTrue() => Assert.True(PaiCode.MakeSerial(8).IsYaochupai);
    // シナリオ8: 2m → NOT Yaochupai
    [Fact]
    public void IsYaochupai_2m_IsFalse() => Assert.False(PaiCode.MakeSerial(1).IsYaochupai);
    // シナリオ9: East → Yaochupai (字牌)
    [Fact]
    public void IsYaochupai_East_IsTrue() => Assert.True(PaiCode.MakeSerial(27).IsYaochupai);

    // シナリオ9b: Huapai → Tsupai/Yaochupai in legacy predicate family
    // 原典: CPaiCode::IsTsupai() は m_nCode >= 0x30 のため 0x40 以上も true
    [Fact]
    public void IsTsupai_Huapai_IsTrueLikeLegacy()
    {
        var pai = MakeRawPaiCode(0x40);

        Assert.True(pai.IsHuapai);
        Assert.True(pai.IsTsupai);
        Assert.True(pai.IsYaochupai);
    }

    [Fact]
    public void IsSangenpai_Huapai_IsTrueLikeLegacy()
    {
        var pai = MakeRawPaiCode(0x40);

        Assert.True(pai.IsSangenpai);
    }

    // ─── IsRaotoupai ─────────────────────────────────────────────────────
    // 原典: IsShupai() && (m_nCode & 0x07) == 1 (1/9 of suited tiles)

    // シナリオ10: 1m → Raotoupai (老頭牌)
    [Fact]
    public void IsRaotoupai_1m_IsTrue() => Assert.True(PaiCode.MakeSerial(0).IsRaotoupai);
    // シナリオ11: East → NOT Raotoupai (字牌は老頭牌ではない)
    [Fact]
    public void IsRaotoupai_East_IsFalse() => Assert.False(PaiCode.MakeSerial(27).IsRaotoupai);
    // シナリオ12: 5m → NOT Raotoupai
    [Fact]
    public void IsRaotoupai_5m_IsFalse() => Assert.False(PaiCode.MakeSerial(4).IsRaotoupai);

    // ─── IsGreen ─────────────────────────────────────────────────────────
    // 原典: Hatsu || (Sou && [2,3,4,6,8])

    // シナリオ13: 発 (serial 32) → IsGreen
    [Fact]
    public void IsGreen_Hatsu_IsTrue() => Assert.True(PaiCode.MakeSerial(32).IsGreen);
    // シナリオ14: 2s (serial 10) → IsGreen
    [Fact]
    public void IsGreen_2s_IsTrue()    => Assert.True(PaiCode.MakeSerial(10).IsGreen);
    // シナリオ15: 1s (serial 9) → NOT IsGreen
    [Fact]
    public void IsGreen_1s_IsFalse()   => Assert.False(PaiCode.MakeSerial(9).IsGreen);
    // シナリオ16: 1m (serial 0) → NOT IsGreen
    [Fact]
    public void IsGreen_1m_IsFalse()   => Assert.False(PaiCode.MakeSerial(0).IsGreen);

    // ─── Kind / Number accessor ───────────────────────────────────────────

    // シナリオ17: 5m のKind=Man, Number=5
    [Fact]
    public void GetKindNumber_5m_ManFive()
    {
        var pai = PaiCode.MakeSerial(4); // 5m
        Assert.Equal(PaiCode.Kind.Man, pai.GetKind());
        Assert.Equal(5, pai.GetNumber());
    }

    // シナリオ18: 3s のKind=Sou, Number=3
    [Fact]
    public void GetKindNumber_3s_SouThree()
    {
        var pai = PaiCode.MakeSerial(11); // 3s (serial=9+2)
        Assert.Equal(PaiCode.Kind.Sou, pai.GetKind());
        Assert.Equal(3, pai.GetNumber());
    }

    // ─── IsRed / BipaiIndex ───────────────────────────────────────────────

    // シナリオ19: 赤牌フラグ設定/取得
    [Fact]
    public void IsRed_SetGet_Works()
    {
        var pai = PaiCode.MakeSerial(4); // 5m
        Assert.False(pai.IsRed);
        pai.IsRed = true;
        Assert.True(pai.IsRed);
    }

    // シナリオ20: GetSerialRed — 赤牌は 34+kind
    [Fact]
    public void GetSerialRed_RedTile_Returns34PlusKind()
    {
        var pai = PaiCode.MakeSerial(4); // 5m (kind=Man=0)
        pai.IsRed = true;
        Assert.Equal(34 + 0, pai.GetSerialRed()); // 34 + Man(0) = 34
    }

    // シナリオ21: GetSerialRed — 非赤牌はGetSerial()と同じ
    [Fact]
    public void GetSerialRed_Normal_SameAsGetSerial()
    {
        var pai = PaiCode.MakeSerial(5); // 6m
        Assert.Equal(pai.GetSerial(), pai.GetSerialRed());
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Yaku.CalcHoraTen テスト
// 原典: HMajakYaku.h CalcHoraTen()
//   mantbl[]={0,0,0,0,1,1,2,2,3,3,3,4,4}
//   tentbl[]={0,2000,3000,4000,6000,8000}
// ═══════════════════════════════════════════════════════════════════════════
public class YakuCalcHoraTenTests
{
    // ─── 役満 ─────────────────────────────────────────────────────────────
    // 原典: IsYakuman() → m_nMangan=6 / m_nTen=8000*m_nHanSum

    // シナリオ1: 役満(1倍) → Ten=8000, Mangan=6
    [Fact]
    public void CalcHoraTen_Yakuman_Ten8000()
    {
        var yaku = new Yaku();
        yaku.AddYakuman(HoraYaku.Suuankou, 1);
        yaku.CalcHoraTen();
        Assert.Equal(8000, yaku.Ten);
        Assert.Equal(6, yaku.Mangan);
    }

    // シナリオ2: ダブル役満(2倍) → Ten=16000
    [Fact]
    public void CalcHoraTen_DoubleYakuman_Ten16000()
    {
        var yaku = new Yaku();
        yaku.AddYakuman(HoraYaku.Suuankou2, 2);
        yaku.CalcHoraTen();
        Assert.Equal(16000, yaku.Ten);
    }

    // ─── 高翻 (5翻以上) ──────────────────────────────────────────────────
    // 原典: m_nHanSum>4 → m_nMangan=mantbl[m_nHanSum] / m_nTen=tentbl[...]

    // シナリオ3: 5翻 → 満貫 Ten=2000 Mangan=1
    [Fact]
    public void CalcHoraTen_5Han_Mangan()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 5); // dummy 5-han
        yaku.CalcHoraTen();
        Assert.Equal(2000, yaku.Ten);
        Assert.Equal(1, yaku.Mangan); // mantbl[5]=1
    }

    // シナリオ4: 6翻 → 跳満 Ten=3000 Mangan=2
    [Fact]
    public void CalcHoraTen_6Han_Haneman()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 6);
        yaku.CalcHoraTen();
        Assert.Equal(3000, yaku.Ten);
        Assert.Equal(2, yaku.Mangan); // mantbl[6]=2
    }

    // シナリオ5: 8翻 → 三倍満 Ten=4000 Mangan=3
    // 原典: mantbl[8]=3 → tentbl[3]=4000
    [Fact]
    public void CalcHoraTen_8Han_Sanbaiman()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 8);
        yaku.CalcHoraTen();
        Assert.Equal(4000, yaku.Ten);
        Assert.Equal(3, yaku.Mangan); // mantbl[8]=3 (三倍満)
    }

    // シナリオ6: 11翻 → 三倍満 Ten=6000 Mangan=4
    // mantbl[11]=4 → tentbl[4]=6000
    [Fact]
    public void CalcHoraTen_11Han_Sanbaiman()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 11);
        yaku.CalcHoraTen();
        Assert.Equal(6000, yaku.Ten);
        Assert.Equal(4, yaku.Mangan); // mantbl[11]=4
    }

    // シナリオ7: 13翻以下 → 数え役満 Mangan=5 Ten=8000
    [Fact]
    public void CalcHoraTen_13Han_KazoeYakuman()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 13);
        yaku.CalcHoraTen();
        Assert.Equal(8000, yaku.Ten);
        Assert.Equal(5, yaku.Mangan);
    }

    // シナリオ8: 13超 → 数え役満 Mangan=5
    [Fact]
    public void CalcHoraTen_Over13Han_KazoeYakuman()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 20);
        yaku.CalcHoraTen();
        Assert.Equal(5, yaku.Mangan);
    }

    // ─── 切り上げ満貫 ───────────────────────────────────────────────────
    // 原典: (m_nHanSum==4 && m_nFu>=30) || (m_nHanSum==3 && m_nFu>=60)

    // シナリオ9: 4翻30符 → 切り上げ満貫 Ten=2000 Mangan=0
    [Fact]
    public void CalcHoraTen_4Han30Fu_KiriageMangan()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 4);
        yaku.Fu = 30;
        yaku.CalcHoraTen();
        Assert.Equal(2000, yaku.Ten);
        Assert.Equal(0, yaku.Mangan);
    }

    // シナリオ10: 3翻60符 → 切り上げ満貫
    [Fact]
    public void CalcHoraTen_3Han60Fu_KiriageMangan()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 3);
        yaku.Fu = 60;
        yaku.CalcHoraTen();
        Assert.Equal(2000, yaku.Ten);
        Assert.Equal(0, yaku.Mangan);
    }

    // シナリオ11: 4翻20符 → 切り上げなし Ten=(20<<4)*4=1280 Mangan=-1
    [Fact]
    public void CalcHoraTen_4Han20Fu_NormalCalc()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 4);
        yaku.Fu = 20;
        yaku.CalcHoraTen();
        // Ten = (20 << 4) * 4 = 320 * 4 = 1280
        Assert.Equal(1280, yaku.Ten);
        Assert.Equal(-1, yaku.Mangan);
    }

    // シナリオ12: 3翻30符 → Ten=(30<<3)*4=960 Mangan=-1
    [Fact]
    public void CalcHoraTen_3Han30Fu_NormalCalc()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 3);
        yaku.Fu = 30;
        yaku.CalcHoraTen();
        // Ten = (30 << 3) * 4 = 240 * 4 = 960
        Assert.Equal(960, yaku.Ten);
        Assert.Equal(-1, yaku.Mangan);
    }

    // シナリオ13: Ten > 2000 → 切り上げ満貫 Mangan=0 Ten=2000
    // 例: 4翻40符 → (40<<4)*4 = 2560 → clamp to 2000
    [Fact]
    public void CalcHoraTen_4Han40Fu_ClampTo2000()
    {
        var yaku = new Yaku();
        yaku.AddYaku(HoraYaku.Pinfu, 4);
        yaku.Fu = 40;
        yaku.CalcHoraTen();
        Assert.Equal(2000, yaku.Ten);
        Assert.Equal(0, yaku.Mangan);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Yaku.CheckAndUpdate テスト
// 原典: YAKU::CheckAndUpdate — Ten が高い方を保持
// ═══════════════════════════════════════════════════════════════════════════
public class YakuCheckAndUpdateTests
{
    // シナリオ1: other の Ten が高い → other に更新
    [Fact]
    public void CheckAndUpdate_OtherHigher_UpdatesToOther()
    {
        var self = new Yaku();
        self.AddYaku(HoraYaku.Pinfu, 1);
        self.Fu  = 30;
        self.CalcHoraTen(); // Ten = (30<<1)*4 = 240

        var other = new Yaku();
        other.AddYaku(HoraYaku.Tanyao, 2);
        other.Fu  = 30;
        other.CalcHoraTen(); // Ten = (30<<2)*4 = 480

        self.CheckAndUpdate(other);
        Assert.Equal(480, self.Ten);
    }

    // シナリオ2: self の Ten が高い → 変化なし
    [Fact]
    public void CheckAndUpdate_SelfHigher_NoChange()
    {
        var self = new Yaku();
        self.AddYaku(HoraYaku.Tanyao, 3);
        self.Fu  = 40;
        self.CalcHoraTen(); // 3han40fu = (40<<3)*4 = 1280

        var other = new Yaku();
        other.AddYaku(HoraYaku.Pinfu, 1);
        other.Fu  = 20;
        other.CalcHoraTen(); // 1han20fu = (20<<1)*4 = 160

        int prevTen = self.Ten;
        self.CheckAndUpdate(other);
        Assert.Equal(prevTen, self.Ten);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// HoraYaku 役コードテスト
// 原典: MajakDef.h HORAYAKU enum — Y_HAITEI=0, Y_TANYAO=8, YAKUMAN_START=100
// ═══════════════════════════════════════════════════════════════════════════
public class HoraYakuTests
{
    // シナリオ1: 通常役の開始は0
    [Fact] public void HoraYaku_Haitei_Is0()      => Assert.Equal(0,   (int)HoraYaku.Haitei);
    // シナリオ2: タンヤオ=8
    [Fact] public void HoraYaku_Tanyao_Is8()      => Assert.Equal(8,   (int)HoraYaku.Tanyao);
    // シナリオ3: 役満は100から開始
    [Fact] public void HoraYaku_Daisangen_Is100() => Assert.Equal(100, (int)HoraYaku.Daisangen);
    // シナリオ4: 国士無双 = 108
    [Fact] public void HoraYaku_Kokushi_Is108()   => Assert.Equal(108, (int)HoraYaku.Kokushi);
    // シナリオ5: 天和 = 109
    [Fact] public void HoraYaku_Tenhou_Is109()    => Assert.Equal(109, (int)HoraYaku.Tenhou);

    // シナリオ6: 役満フラグは AddYakuman でのみ立つ
    [Fact]
    public void IsYakuman_AfterAddYakuman_IsTrue()
    {
        var y = new Yaku();
        Assert.False(y.IsYakuman);
        y.AddYakuman(HoraYaku.Tenhou, 1);
        Assert.True(y.IsYakuman);
    }

    // シナリオ7: AddYaku では役満フラグは立たない
    [Fact]
    public void IsYakuman_AfterAddYaku_IsFalse()
    {
        var y = new Yaku();
        y.AddYaku(HoraYaku.Tanyao, 2);
        Assert.False(y.IsYakuman);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Act enum テスト
// 原典: MajakDef.h ACT enum — INV=0, PAS=1, CHI=2 ... TAP=6 etc.
// ═══════════════════════════════════════════════════════════════════════════
public class ActEnumTests
{
    [Fact] public void Act_Inv_Is0() => Assert.Equal(0, (int)Act.Inv);
    [Fact] public void Act_Pas_Is1() => Assert.Equal(1, (int)Act.Pas);
    [Fact] public void Act_Chi_Is2() => Assert.Equal(2, (int)Act.Chi);
    [Fact] public void Act_Pon_Is3() => Assert.Equal(3, (int)Act.Pon);
    [Fact] public void Act_Kan_Is4() => Assert.Equal(4, (int)Act.Kan);
    [Fact] public void Act_Ron_Is5() => Assert.Equal(5, (int)Act.Ron);
    [Fact] public void Act_Tap_Is6() => Assert.Equal(6, (int)Act.Tap);
    [Fact] public void Act_Ank_Is7() => Assert.Equal(7, (int)Act.Ank);
    [Fact] public void Act_Ric_Is9() => Assert.Equal(9, (int)Act.Ric);
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic 追加テスト
// ═══════════════════════════════════════════════════════════════════════════
public class MajakGameLogicExtraTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    // シナリオ1: InitHanchan 後の TehaiCount — 親=14, 子=13
    [Fact]
    public void InitHanchan_TehaiCounts_Correct()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int parent = logic.KyokuInfo.OyaOrder;
        for (int i = 0; i < 4; i++)
            Assert.Equal(i == parent ? 14 : 13, logic.Player[i].Tehai.Count);
    }

    // シナリオ2: InitHanchan 後の KyokuInfo.Dice が初期化されている
    // 原典: KyokuInfo.Dice = new int[DiceCount] (修正済みバグ)
    [Fact]
    public void InitHanchan_Dice_IsInitialized()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        Assert.NotNull(logic.KyokuInfo.Dice);
        Assert.Equal(MajakConst.DiceCount, logic.KyokuInfo.Dice.Length);
        Assert.All(logic.KyokuInfo.Dice, die => Assert.InRange(die, 0, 5));
    }

    // シナリオ3: ProcessAction 無効アクション → ErrInvalidAction
    [Fact]
    public void ProcessAction_InvalidAction_ReturnsErrInvalidAction()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var result = logic.ProcessAction(0, (Act)255, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.ErrInvalidAction, result);
    }

    // シナリオ4: HanchanInfo.Player は 0-3 の順列
    [Fact]
    public void HanchanInfo_Players_ArePermutation()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var sorted = logic.HanchanInfo.Player.OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, sorted);
    }

    // シナリオ5: ドラ表示牌は有効な牌
    [Fact]
    public void KyokuInfo_DoraIndicator_IsValid()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        Assert.True(logic.KyokuInfo.Dora[0].IsValid);
    }

    // シナリオ6: 各プレイヤーの手牌に重複牌がない (BipaiIndex ユニーク)
    [Fact]
    public void InitHanchan_AllBipaiIndexUnique()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var all = logic.Player
            .SelectMany(p => p.Tehai)
            .Select(p => p.BipaiIndex)
            .ToList();
        Assert.Equal(all.Count, all.Distinct().Count()); // 全 BipaiIndex が一意
    }
}
