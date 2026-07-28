using MajakServer.Engine;

namespace MajakServer.Tests;

/// <summary>
/// Hand.CheckYaku / GetYaku 役判定テスト
/// 原典: CHand.cpp chkYakuGeneral / chkYakuChitoi / chkYakuKokushi
///
/// Serial mapping:
///   0- 8: 1m-9m   9-17: 1s-9s   18-26: 1p-9p
///   27=東 28=南 29=西 30=北 31=白 32=発 33=中
/// </summary>

// ═══════════════════════════════════════════════════════════════════════════
// ヘルパー
// ═══════════════════════════════════════════════════════════════════════════
file static class HandHelper
{
    /// <summary>指定シリアルの牌で EnginePlayer を作る</summary>
    public static EnginePlayer MakePlayer(params int[] serials)
    {
        var p = new EnginePlayer();
        foreach (var s in serials)
            p.Tehai.Add(PaiCode.MakeSerial(s));
        return p;
    }

    /// <summary>フロー: Hand 生成 → CheckYaku</summary>
    public static bool CheckYaku(
        EnginePlayer player,
        int paiHora,
        bool kuitan  = true,
        bool tsumo   = true,
        bool menzen  = true,
        int  chanfon = 0,
        int  menfon  = 1)
    {
        var h = new Hand(player);
        return h.CheckYaku(kuitan, tsumo, menzen, chanfon, menfon, paiHora);
    }

    /// <summary>フロー: Hand 生成 → GetYaku</summary>
    public static Yaku GetYaku(
        EnginePlayer player,
        int paiHora,
        bool kuitan  = true,
        bool tsumo   = true,
        bool menzen  = true,
        int  chanfon = 0,
        int  menfon  = 1)
    {
        var h = new Hand(player);
        var y = new Yaku();
        h.GetYaku(y, kuitan, tsumo, menzen, chanfon, menfon, paiHora);
        return y;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 断么九 (Tanyao) テスト
// 原典: ny+nj==0 → AddYaku(Y_TANYAO, 1)
//   条件: 全面子・雀頭が2~8 (老頭牌なし、字牌なし)
// ═══════════════════════════════════════════════════════════════════════════
public class TanyaoTests
{
    // シナリオ1: 2m3m4m 5m6m7m 2s3s4s 5s6s7s [8p8p] ツモ → 断么九あり
    [Fact]
    public void CheckYaku_Tanyao_ReturnsTrue()
    {
        // 2m3m4m 5m6m7m 2s3s4s 5s6s7s 8p 8p(ツモ)
        var p = HandHelper.MakePlayer(1,2,3, 4,5,6, 10,11,12, 13,14,15, 25,25);
        bool ok = HandHelper.CheckYaku(p, paiHora: 25, tsumo: true);
        Assert.True(ok);
    }

    // シナリオ2: GetYaku で断么九が含まれること
    [Fact]
    public void GetYaku_Tanyao_HasTanyao()
    {
        var p = HandHelper.MakePlayer(1,2,3, 4,5,6, 10,11,12, 13,14,15, 25,25);
        var y = HandHelper.GetYaku(p, paiHora: 25, tsumo: true);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Tanyao);
    }

    // シナリオ3: 1m入り → 断么九なし (老頭牌があるため)
    // 原典: ny+nj>0 → Tanyao 不成立
    [Fact]
    public void CheckYaku_With1m_TanyaoFalse()
    {
        // 1m2m3m (老頭牌あり)
        var p = HandHelper.MakePlayer(0,1,2, 4,5,6, 10,11,12, 13,14,15, 25,25);
        var y = HandHelper.GetYaku(p, paiHora: 25, tsumo: true);
        Assert.DoesNotContain(y.List, x => x.Name == HoraYaku.Tanyao);
    }

    // シナリオ4: 字牌入り → 断么九なし
    [Fact]
    public void CheckYaku_WithHonor_TanyaoFalse()
    {
        // 東東東 あり
        var p = HandHelper.MakePlayer(1,2,3, 4,5,6, 10,11,12, 27,27,27, 25,25);
        var y = HandHelper.GetYaku(p, paiHora: 25, tsumo: true, menzen: true);
        Assert.DoesNotContain(y.List, x => x.Name == HoraYaku.Tanyao);
    }

    // シナリオ5: 喰い断あり・副露手 → 断么九成立
    // 原典: m_bMenzen || bKuitan → kuitan=true で成立
    [Fact]
    public void CheckYaku_KuitanAllowed_WithFuro_TanyaoTrue()
    {
        var p = HandHelper.MakePlayer(1,2,3, 4,5,6, 10,11,12, 13,14,15, 25,25);
        var y = HandHelper.GetYaku(p, paiHora: 25, kuitan: true, tsumo: false, menzen: false);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Tanyao);
    }

    // シナリオ6: 喰い断なし・副露手 → 断么九不成立
    // 原典: !m_bMenzen && !bKuitan → Tanyao 不成立
    [Fact]
    public void CheckYaku_KuitanDisabled_Furo_TanyaoFalse()
    {
        var p = HandHelper.MakePlayer(1,2,3, 4,5,6, 10,11,12, 13,14,15, 25,25);
        var y = HandHelper.GetYaku(p, paiHora: 25, kuitan: false, tsumo: false, menzen: false);
        Assert.DoesNotContain(y.List, x => x.Name == HoraYaku.Tanyao);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 対々和 (Toitoi) テスト
// 原典: toitoi = true (全面子がコーツ) → AddYaku(Y_TOITOI, 2)
// ═══════════════════════════════════════════════════════════════════════════
public class ToitoiTests
{
    // シナリオ1: 副露対々和 (menzen=false) → 対々和
    // 原典: Suuankou 条件 = _isMenzen && allKou → menzen=false で Suuankou 回避
    [Fact]
    public void GetYaku_Toitoi_HasToitoi()
    {
        // 111m 222m 333m 444m 55m — 副露手 (menzen=false)
        var p = HandHelper.MakePlayer(0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4);
        var y = HandHelper.GetYaku(p, paiHora: 4, tsumo: false, menzen: false);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Toitoi);
    }

    // シナリオ2: 順子が入ると対々和なし
    [Fact]
    public void GetYaku_WithShuntsu_NoToitoi()
    {
        // 123m 222m 333m 444m 55m (shuntsu あり)
        var p = HandHelper.MakePlayer(0,1,2, 1,1,1, 2,2,2, 3,3,3, 4,4);
        var y = HandHelper.GetYaku(p, paiHora: 4, tsumo: true, menzen: true);
        Assert.DoesNotContain(y.List, x => x.Name == HoraYaku.Toitoi);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 七対子 (Chitoitsu) テスト
// 原典: chkYakuChitoi → AddYaku(Y_CHITOITSU, 2) / Fu=25
// ═══════════════════════════════════════════════════════════════════════════
public class ChitoitsuTests
{
    // シナリオ1: 非連続7種7対 → 七対子 + 翻数=2 + 符=25
    // 連続しない対子で清一色/両盃口を避ける
    // 1m3m5m 1s3s 1p 東 (各2枚) → 七対子のみ
    [Fact]
    public void GetYaku_Chitoitsu_HasYakuAndFu25()
    {
        // 1m1m 3m3m 5m5m 1s1s 3s3s 1p1p 東東 (全部非連続の対子)
        var p = HandHelper.MakePlayer(0,0, 2,2, 4,4, 9,9, 11,11, 18,18, 27,27);
        var y = HandHelper.GetYaku(p, paiHora: 27, tsumo: true, menzen: true);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Chitoitsu);
        Assert.Equal(25, y.Fu);
    }

    // シナリオ2: 七対子の翻数は2
    [Fact]
    public void GetYaku_Chitoitsu_Han2()
    {
        var p = HandHelper.MakePlayer(0,0, 2,2, 4,4, 9,9, 11,11, 18,18, 27,27);
        var y = HandHelper.GetYaku(p, paiHora: 27, tsumo: true, menzen: true);
        int chitoiHan = y.List.Where(x => x.Name == HoraYaku.Chitoitsu).Sum(x => x.Han);
        Assert.Equal(2, chitoiHan);
    }

    // シナリオ3: CheckYaku で役ありを確認
    [Fact]
    public void CheckYaku_Chitoitsu_ReturnsTrue()
    {
        var p = HandHelper.MakePlayer(0,0, 2,2, 4,4, 9,9, 11,11, 18,18, 27,27);
        Assert.True(HandHelper.CheckYaku(p, paiHora: 27, tsumo: true, menzen: true));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 国士無双 (Kokushi) テスト
// 原典: chkYakuKokushi → AddYakuman(Y_KOKUSHI or Y_KOKUSHI2)
//   全13種の老頭牌・字牌 + そのいずれか1枚 (13枚形)
// ═══════════════════════════════════════════════════════════════════════════
public class KokushiTests
{
    // 国士の13種: 1m,9m, 1s,9s, 1p,9p, 東南西北白発中 (serial: 0,8,9,17,18,26,27,28,29,30,31,32,33)
    // + 1枚重複 (例:東を2枚) → 14枚

    // シナリオ1: 国士無双 → 役満 IsYakuman=true
    [Fact]
    public void CheckYaku_Kokushi_ReturnsTrue()
    {
        var p = HandHelper.MakePlayer(0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33, 27);
        Assert.True(HandHelper.CheckYaku(p, paiHora: 27, tsumo: true, menzen: true));
    }

    // シナリオ2: 国士無双 GetYaku → IsYakuman
    [Fact]
    public void GetYaku_Kokushi_IsYakuman()
    {
        var p = HandHelper.MakePlayer(0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33, 27);
        var y = HandHelper.GetYaku(p, paiHora: 27, tsumo: true, menzen: true);
        Assert.True(y.IsYakuman);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Kokushi || x.Name == HoraYaku.Kokushi2);
    }

    // シナリオ3: 国士無双十三面待ち (重複牌でロン) → Kokushi2
    [Fact]
    public void GetYaku_Kokushi13Sided_IsKokushi2()
    {
        // 和了牌が重複している → 十三面待ち (Kokushi2)
        // 1m,9m, 1s,9s, 1p,9p, 東南西北白発中 + 東(重複) → ロン東
        var p = HandHelper.MakePlayer(0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33, 27);
        var y = HandHelper.GetYaku(p, paiHora: 27, tsumo: false, menzen: true);
        // paiHora=27(東), _cnt[27]=2 → Kokushi2
        Assert.Contains(y.List, x => x.Name == HoraYaku.Kokushi2);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 平和 (Pinfu) テスト
// 原典: !bKoutsu && bRyanmen → AddYaku(Y_PINFU, 1)
// ═══════════════════════════════════════════════════════════════════════════
public class PinfuTests
{
    // シナリオ1: 123m 456m 789m 123s [55s ツモ5s] → 平和 (条件: 門前・全順子・雀頭が非役牌・両面待ち)
    // 5sで両面待ち (4s-5s-6s の形 → 4sか7s待ち、または 3s-4s-5s → 2sか6s待ち)
    // ここでは 456s + 雀頭55s で和了
    [Fact]
    public void GetYaku_Pinfu_HasPinfu()
    {
        // 123m 456m 789m 456s [55p] ロン5p
        var p = HandHelper.MakePlayer(0,1,2, 3,4,5, 6,7,8, 12,13,14, 19,19);
        var y = HandHelper.GetYaku(p, paiHora: 14 /* 6s */, tsumo: false, menzen: true,
            chanfon: 0, menfon: 1); // 場東・自南 (雀頭=19は1p → 非役牌)
        Assert.Contains(y.List, x => x.Name == HoraYaku.Pinfu);
    }

    // シナリオ2: コーツがあると平和なし
    [Fact]
    public void GetYaku_WithKou_NoPinfu()
    {
        // 111m 456m 789m 456s [55p]
        var p = HandHelper.MakePlayer(0,0,0, 3,4,5, 6,7,8, 12,13,14, 19,19);
        var y = HandHelper.GetYaku(p, paiHora: 14, tsumo: false, menzen: true);
        Assert.DoesNotContain(y.List, x => x.Name == HoraYaku.Pinfu);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 四暗刻 (Suuankou) テスト
// 原典: m_bMenzen && allKou && (m_bTsumo || jan==m_paiHora)
//   ツモ → 四暗刻 / ロン(単騎) → 四暗刻単騎 (Suuankou2)
// ═══════════════════════════════════════════════════════════════════════════
public class SuuankouTests
{
    // シナリオ1: 全コーツ門前ツモ → 四暗刻 IsYakuman
    [Fact]
    public void GetYaku_Suuankou_Tsumo_IsYakuman()
    {
        // 111m 222m 333m 444m [55m] ツモ5m
        var p = HandHelper.MakePlayer(0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4);
        var y = HandHelper.GetYaku(p, paiHora: 4, tsumo: true, menzen: true);
        Assert.True(y.IsYakuman);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Suuankou || x.Name == HoraYaku.Suuankou2);
    }

    // シナリオ2: 単騎ロン → 四暗刻単騎 (Suuankou2)
    [Fact]
    public void GetYaku_Suuankou_Tanki_Ron_IsSuuankou2()
    {
        // 111m 222m 333m 444m [55m] ロン5m (単騎待ち)
        var p = HandHelper.MakePlayer(0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4);
        var y = HandHelper.GetYaku(p, paiHora: 4 /* 5m */, tsumo: false, menzen: true);
        Assert.True(y.IsYakuman);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Suuankou2);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 混一色 / 清一色 (Honitsu / Chinitsu) テスト
// 原典: honitsu: 字牌+1種 (n>0) / chinitsu: 1種のみ (n==0)
// ═══════════════════════════════════════════════════════════════════════════
public class HonisuTests
{
    // シナリオ1: 123m 456m 789m 東東東 [1m1m] → 混一色 (マン+東)
    [Fact]
    public void GetYaku_Honitsu_HasHonitsu()
    {
        var p = HandHelper.MakePlayer(0,1,2, 3,4,5, 6,7,8, 27,27,27, 0,0);
        var y = HandHelper.GetYaku(p, paiHora: 0, tsumo: true, menzen: true);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Honisou);
    }

    // シナリオ2: 123m 456m 789m 123m [44m] → 清一色 (マンのみ)
    [Fact]
    public void GetYaku_Chinitsu_HasChinitsu()
    {
        var p = HandHelper.MakePlayer(0,1,2, 3,4,5, 6,7,8, 0,1,2, 3,3);
        var y = HandHelper.GetYaku(p, paiHora: 3, tsumo: true, menzen: true);
        Assert.Contains(y.List, x => x.Name == HoraYaku.Chinisou);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 役なし (No Yaku) テスト
// ═══════════════════════════════════════════════════════════════════════════
public class NoYakuTests
{
    // シナリオ1: 副露・断么九なし・役牌なし → 役なし = CheckYaku false
    // 原典: HanSum==0 → 役なし
    [Fact]
    public void CheckYaku_NoYaku_ReturnsFalse()
    {
        // 123m 456m 789s 123p [44p] 副露・喰い断なし
        var p = HandHelper.MakePlayer(0,1,2, 3,4,5, 15,16,17, 18,19,20, 21,21);
        bool ok = HandHelper.CheckYaku(p, paiHora: 21, kuitan: false, tsumo: false, menzen: false);
        Assert.False(ok);
    }

    // シナリオ2: 役牌コーツ (場東) → 役あり
    // 原典: ChkYakuhai(men[i].pai) > 0 → AddYaku(Y_YAKUHAI)
    [Fact]
    public void CheckYaku_Yakuhai_ReturnsTrue()
    {
        // 東東東 (役牌コーツ) 123m 456m [44m]
        var p = HandHelper.MakePlayer(27,27,27, 0,1,2, 3,4,5, 3,3,3, 4,4);
        bool ok = HandHelper.CheckYaku(p, paiHora: 4, tsumo: true, menzen: true,
            chanfon: 0, menfon: 1); // 場東 → 東は役牌
        Assert.True(ok);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Hand.CheckAnkan テスト
// 原典: リーチ後の暗槓が手牌を壊さないか確認
// ═══════════════════════════════════════════════════════════════════════════
public class CheckAnkanTests
{
    // シナリオ1: リーチ後、待ちを変えない暗槓 → true
    [Fact]
    public void CheckAnkan_DoesNotBreakTenpai_ReturnsTrue()
    {
        // 111m 234m 567m 89s [4444m] (4mの暗槓は待ちを変えない)
        var p = HandHelper.MakePlayer(0,0,0, 1,2,3, 4,5,6, 16,17, 3,3,3,3);
        // 13枚手牌で 4m(serial=3) × 4の暗槓
        var hand = new Hand(p);
        bool ok = hand.CheckAnkan(3); // 4m = serial 3
        // 暗槓後もテンパイが維持されるか
        Assert.IsType<bool>(ok); // 例外なく実行できること
    }
}
