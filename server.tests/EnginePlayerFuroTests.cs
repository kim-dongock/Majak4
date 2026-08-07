using MajakServer.Engine;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// EnginePlayer 副露操作テスト
// 原典: HMajakPlayer.cpp — Chi / Pon / MinKan / AnKan / ChaKan
// ═══════════════════════════════════════════════════════════════════════════
public class EnginePlayerFuroTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0, Tip = false,
    };

    /// <summary>指定シリアルの牌を手牌に追加するヘルパー</summary>
    private static PaiCode AddToTehai(EnginePlayer p, int serial, int bipaiIndex)
    {
        var pai = PaiCode.MakeSerial(serial);
        pai.BipaiIndex = bipaiIndex;
        p.Tehai.Add(pai);
        return pai;
    }

    // ─── Chi ────────────────────────────────────────────────────────────────

    // シナリオ1: 有効なチー → Furo 追加 + 手牌2枚減少 + IsMenzen=false
    // 原典: stFuro.eAct = CHI; m_bIsMenzen = false
    [Fact]
    public void Chi_ValidTiles_AddsFuroAndSetsMenzenFalse()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        // 手牌に 2m(1) と 3m(2) を追加 (チーするのは 1m + 2m + 3m)
        var pai1 = AddToTehai(p, 1, 10); // 2m
        var pai2 = AddToTehai(p, 2, 11); // 3m
        var tapai = PaiCode.MakeSerial(0); // 1m (打牌牌)
        tapai.BipaiIndex = 0;

        var result = p.Chi(tapaiOrder: 3, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        Assert.Equal(ActionResult.Ok, result);
        Assert.Single(p.Furo);
        Assert.Equal(Act.Chi, p.Furo[0].Act);
        Assert.Empty(p.Tehai);
        Assert.False(p.IsMenzen);
    }

    // シナリオ2: チー後の面子牌がソート済み
    // 原典: stFuro.listFuroPai.sort() — 牌コード昇順
    [Fact]
    public void Chi_Tiles_AreSorted()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        AddToTehai(p, 2, 10); // 3m
        AddToTehai(p, 1, 11); // 2m (先に追加)
        var tapai = PaiCode.MakeSerial(0);
        tapai.BipaiIndex = 0;

        p.Chi(tapaiOrder: 3, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        var serials = p.Furo[0].Tiles.Select(t => t.GetSerial()).ToArray();
        Assert.Equal(new[] { 0, 1, 2 }, serials); // 1m,2m,3m の順
    }

    // シナリオ3: 手牌にない BipaiIndex → ErrPaiNotFoundInHand
    // 原典: RemoveTehai 失敗 → MLE_PAINOTFOUNDINHAND
    [Fact]
    public void Chi_PaiNotInHand_ReturnsError()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        var tapai = PaiCode.MakeSerial(0);
        tapai.BipaiIndex = 0;
        var result = p.Chi(tapaiOrder: 3, curTapai: tapai, bipaiIndex: new[] { 99, 100 });

        Assert.Equal(ActionResult.ErrPaiNotFoundInHand, result);
    }

    [Fact]
    public void Chi_Kuikae_BlocksCalledAndAlternativeTilesUntilFirstDiscard()
    {
        var player = new EnginePlayer();
        player.InitHanchan(0, DefaultRule());
        player.InitKyoku();

        AddToTehai(player, 3, 10); // 4m
        AddToTehai(player, 4, 11); // 5m
        var calledAgain = AddToTehai(player, 2, 12); // 3m
        var alternative = AddToTehai(player, 5, 13); // 6m
        var safeTile = AddToTehai(player, 8, 14); // 9m
        var calledTile = PaiCode.MakeSerial(2); // 3mをチーして345m

        Assert.Equal(ActionResult.Ok, player.Chi(3, calledTile, new[] { 10, 11 }));
        Assert.Equal(ActionResult.ErrKuikae, player.Tapai(calledAgain));
        Assert.Equal(ActionResult.ErrKuikae, player.Tapai(alternative));
        Assert.Equal(ActionResult.Ok, player.Tapai(safeTile));
        Assert.Equal(ActionResult.Ok, player.Tapai(calledAgain));
    }

    [Fact]
    public void Chi_Kuikae_MiddleCalledTileDoesNotBlockUnrelatedSuji()
    {
        var player = new EnginePlayer();
        player.InitHanchan(0, DefaultRule());
        player.InitKyoku();

        AddToTehai(player, 2, 10); // 3m
        AddToTehai(player, 4, 11); // 5m
        var unrelatedTile = AddToTehai(player, 1, 12); // 2m
        var calledTile = PaiCode.MakeSerial(3); // 4mをチーして345m

        Assert.Equal(ActionResult.Ok, player.Chi(3, calledTile, new[] { 10, 11 }));
        Assert.Equal(ActionResult.Ok, player.Tapai(unrelatedTile));
    }

    // ─── Pon ────────────────────────────────────────────────────────────────

    // シナリオ4: 有効なポン → Furo追加 + IsMenzen=false
    // 原典: stFuro.eAct = PON; m_bIsMenzen = false
    [Fact]
    public void Pon_ValidTiles_AddsFuroAndSetsMenzenFalse()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        AddToTehai(p, 27, 10); // 東
        AddToTehai(p, 27, 11); // 東
        var tapai = PaiCode.MakeSerial(27); // 東 (打牌牌)
        tapai.BipaiIndex = 0;

        var result = p.Pon(tapaiOrder: 1, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        Assert.Equal(ActionResult.Ok, result);
        Assert.Single(p.Furo);
        Assert.Equal(Act.Pon, p.Furo[0].Act);
        Assert.Equal(3, p.Furo[0].Tiles.Count);
        Assert.False(p.IsMenzen);
    }

    [Fact]
    public void Pon_Kuikae_BlocksCalledTileAndAdvertisesOnlyLegalDiscards()
    {
        var logic = new MajakGameLogic();
        var player = logic.Player[0];
        player.InitHanchan(0, DefaultRule());
        player.InitKyoku();

        AddToTehai(player, 27, 10);
        AddToTehai(player, 27, 11);
        var calledAgain = AddToTehai(player, 27, 12);
        var safeTile = AddToTehai(player, 28, 13);
        var calledTile = PaiCode.MakeSerial(27);

        Assert.Equal(ActionResult.Ok, player.Pon(1, calledTile, new[] { 10, 11 }));
        player.Mode = PlayerMode.Turn;

        var actions = logic.GetValidActions(0);
        Assert.DoesNotContain(calledAgain.BipaiIndex, actions.TapCandidates);
        Assert.Contains(safeTile.BipaiIndex, actions.TapCandidates);
        Assert.Equal(ActionResult.ErrKuikae, player.Tapai(calledAgain));
    }

    // シナリオ5: 副露した本人の流し満貫状態は HMajakPlayer::Pon では変更されない
    // 原典: ClearNagashiMangan は HMajakGameLogic::ProcessFuro の解決時に捨て牌側へだけ適用
    [Fact]
    public void Pon_PreservesCallerNagashiManganLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        Assert.True(p.IsNagashiMangan); // 初期=true

        AddToTehai(p, 27, 10);
        AddToTehai(p, 27, 11);
        var tapai = PaiCode.MakeSerial(27); tapai.BipaiIndex = 0;
        p.Pon(tapaiOrder: 1, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        Assert.True(p.IsNagashiMangan);
    }

    [Fact]
    public void Chi_PreservesTempFuritenLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        p.SetTempFuriten();
        AddToTehai(p, 1, 10);
        AddToTehai(p, 2, 11);
        var tapai = PaiCode.MakeSerial(0); tapai.BipaiIndex = 0;

        p.Chi(tapaiOrder: 3, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        Assert.True(p.CheckFuriten());
    }

    [Fact]
    public void Chi_InvalidSecondTile_DoesNotPartiallyRemoveHandLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        AddToTehai(p, 1, 10);
        var tapai = PaiCode.MakeSerial(0);
        tapai.BipaiIndex = 0;

        var result = p.Chi(tapaiOrder: 3, curTapai: tapai, bipaiIndex: new[] { 10, 99 });

        Assert.Equal(ActionResult.ErrPaiNotFoundInHand, result);
        Assert.Single(p.Tehai);
        Assert.Empty(p.Furo);
    }

    [Fact]
    public void Pon_PreservesTempFuritenLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        p.SetTempFuriten();
        AddToTehai(p, 27, 10);
        AddToTehai(p, 27, 11);
        var tapai = PaiCode.MakeSerial(27); tapai.BipaiIndex = 0;

        p.Pon(tapaiOrder: 1, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        Assert.True(p.CheckFuriten());
    }

    [Fact]
    public void Pon_ThirdSangenpai_SetsPaoOrderLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        p.Furo.Add(new FuroBlock { Act = Act.Pon, Tiles = { PaiCode.MakeSerial(31) } });
        p.Furo.Add(new FuroBlock { Act = Act.Pon, Tiles = { PaiCode.MakeSerial(32) } });
        AddToTehai(p, 33, 10);
        AddToTehai(p, 33, 11);
        var tapai = PaiCode.MakeSerial(33);
        tapai.BipaiIndex = 0;

        var result = p.Pon(tapaiOrder: 2, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(2, p.PaoOrder);
    }

    // ─── MinKan ─────────────────────────────────────────────────────────────

    // シナリオ6: 有効な明槓 → Furo追加 + KanCnt=1 + IsMenzen=false
    // 原典: stFuro.eAct = KAN; m_nKanCnt++; m_bIsMenzen = false
    [Fact]
    public void MinKan_Valid_AddsFuroAndIncrementsKanCnt()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        AddToTehai(p, 31, 10); // 白
        AddToTehai(p, 31, 11); // 白
        AddToTehai(p, 31, 12); // 白
        var tapai = PaiCode.MakeSerial(31); tapai.BipaiIndex = 0;

        var result = p.MinKan(tapaiOrder: 2, curTapai: tapai, bipaiIndex: new[] { 10, 11, 12 });

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(Act.Kan, p.Furo[0].Act);
        Assert.Equal(1, p.KanCnt);
        Assert.False(p.IsMenzen);
    }

    // ─── AnKan ───────────────────────────────────────────────────────────────

    // シナリオ7: 有効な暗槓 → Furo追加 + KanCnt=1 (IsMenzen は変わらない)
    // 原典: stFuro.eAct = ANK; m_nKanCnt++ (m_bIsMenzen は変更しない)
    [Fact]
    public void AnKan_Valid_AddsFuroAndIncrementsKanCnt()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        // 手牌に同じ牌4枚
        for (int i = 0; i < 4; i++) AddToTehai(p, 31, i + 10); // 白×4

        var result = p.AnKan(bipaiIndex: new[] { 10, 11, 12, 13 });

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(Act.Ank, p.Furo[0].Act);
        Assert.Equal(1, p.KanCnt);
        // AnKan は IsMenzen を変更しない
        Assert.True(p.IsMenzen);
    }

    // シナリオ8: 4枚目が足りない → ErrPaiNotFoundInHand
    [Fact]
    public void AnKan_NotEnoughTiles_ReturnsError()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        AddToTehai(p, 31, 10);
        AddToTehai(p, 31, 11);
        AddToTehai(p, 31, 12);

        var result = p.AnKan(bipaiIndex: new[] { 10, 11, 12, 99 }); // 99 は存在しない
        Assert.Equal(ActionResult.ErrPaiNotFoundInHand, result);
    }

    [Fact]
    public void AnKan_AfterRichi_WhenCheckAnkanFails_ReturnsAnkanAfterRichi()
    {
        var (player, indices) = FindRiichiAnkanBreakingHand();

        var result = player.AnKan(indices);

        Assert.Equal(ActionResult.ErrAnkanAfterRichi, result);
    }

    private static (EnginePlayer Player, int[] Indices) FindRiichiAnkanBreakingHand()
    {
        var random = new Random(1);
        for (int attempt = 0; attempt < 20000; attempt++)
        {
            int serial = random.Next(0, 27);
            var tiles = new List<int> { serial, serial, serial };
            var counts = new int[34];
            counts[serial] = 3;
            while (tiles.Count < 13)
            {
                int next = random.Next(0, 34);
                if (counts[next] >= 4) continue;
                counts[next]++;
                tiles.Add(next);
            }
            tiles.Add(serial);
            counts[serial]++;

            var player = new EnginePlayer();
            player.InitHanchan(0, DefaultRule());
            player.InitKyoku();
            int[] indices = new int[4];
            int quadIndex = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = PaiCode.MakeSerial(tiles[i]);
                tile.BipaiIndex = i;
                player.Tehai.Add(tile);
                if (tiles[i] == serial && quadIndex < 4)
                    indices[quadIndex++] = i;
            }
            player.RichiType = RichiType.Richi;

            if (!new Hand(player).CheckAnkan(serial))
                return (player, indices);
        }

        throw new InvalidOperationException("Could not find a riichi ankan fixture that fails CheckAnkan.");
    }

    // ─── ChaKan ──────────────────────────────────────────────────────────────

    // シナリオ9: 加槓 → Pon→Cha 変更 + KanCnt=1
    // 原典: iter->eAct = CHA; m_nKanCnt++
    [Fact]
    public void ChaKan_Valid_ChangesPonToAndIncrementsKanCnt()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        // 先にポン
        AddToTehai(p, 31, 10);
        AddToTehai(p, 31, 11);
        var tapai = PaiCode.MakeSerial(31); tapai.BipaiIndex = 0;
        p.Pon(tapaiOrder: 1, curTapai: tapai, bipaiIndex: new[] { 10, 11 });

        // 4枚目をツモ
        var fourth = PaiCode.MakeSerial(31); fourth.BipaiIndex = 20;
        p.Tsumo(fourth);

        var result = p.ChaKan(fourth);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(Act.Cha, p.Furo[0].Act);
        Assert.Equal(1, p.KanCnt);
    }

    [Fact]
    public void Hua_NonHuapai_ReturnsErrHuapaiLikeLegacy()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var tile = AddToTehai(p, 0, 10);

        var result = p.Hua(tile);

        Assert.Equal(ActionResult.ErrHuapai, result);
        Assert.Single(p.Tehai);
        Assert.Empty(p.NukiDora);
    }

    [Fact]
    public void Hua_Huapai_MovesTileToNukiDora()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();
        var tile = new PaiCode(4, 1);
        tile.BipaiIndex = 10;
        p.Tehai.Add(tile);

        var result = p.Hua(tile);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Empty(p.Tehai);
        Assert.Single(p.NukiDora);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// EnginePlayer Taopai / CheckFuriten テスト
// 原典: HMajakPlayer::Taopai / CheckFuriten
// ═══════════════════════════════════════════════════════════════════════════
public class EnginePlayerQueryTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0, Tip = false,
    };

    // シナリオ1: 9種以上の么九牌でタオパイ → Ok
    // 原典: HMajakPlayer::Taopai は手牌内の 1/9/字牌の種類数 >= 9 を判定する
    [Fact]
    public void Taopai_NineKindsYaochu_ReturnsOkWithoutSettingTempai()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        foreach (var s in new[] { 0,8, 9,17, 18,26, 27,28,29, 1,2,3,4 })
        {
            var pai = PaiCode.MakeSerial(s);
            pai.BipaiIndex = s + 100;
            p.Tehai.Add(pai);
        }

        var result = p.Taopai();

        Assert.Equal(ActionResult.Ok, result);
        Assert.False(p.IsTempai);
    }

    // シナリオ2: 么九牌の種類数が9未満 → ErrCannotHora
    [Fact]
    public void Taopai_LessThanNineKindsYaochu_ReturnsCannotHora()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        // バラバラ手 (13枚)
        foreach (var s in new[] { 0,2,4,6,8,9,11,13,15,17,18,20,22 })
        {
            var pai = PaiCode.MakeSerial(s);
            pai.BipaiIndex = s + 100;
            p.Tehai.Add(pai);
        }

        var result = p.Taopai();
        Assert.Equal(ActionResult.ErrCannotHora, result);
    }

    // シナリオ3: CheckFuriten — 捨て牌でフリテン
    [Fact]
    public void CheckFuriten_SutehaInWait_ReturnsTrue()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.InitKyoku();

        // 13枚テンパイ手: 123m 456m 789m 東東東 南(単騎) → 南待ち
        foreach (var s in new[] { 0,1,2, 3,4,5, 6,7,8, 27,27,27, 28 })
        {
            var pai = PaiCode.MakeSerial(s);
            pai.BipaiIndex = s + 100;
            p.Tehai.Add(pai);
        }
        // 和了牌 南(28) を捨て牌リストに追加 (別の BipaiIndex)
        var horaDiscarded = PaiCode.MakeSerial(28);
        horaDiscarded.BipaiIndex = 200; // 別の BipaiIndex
        p.Sutehai.Add(horaDiscarded);

        Assert.True(p.CheckFuriten());
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RatingService.CalcGradeRating テスト
// 原典: HMajRoomServer::CalcRating_MajakTypeGradeMode (HMajRoomServer.cpp)
//   【修正済み】旧実装 (Elo式) → 新実装 (pointSum + selfCorrect + scale)
// ═══════════════════════════════════════════════════════════════════════════
public class CalcGradeRatingTests
{
    private readonly RatingService _svc = new();

    // 原典定数:
    //   RATING_CARC_MATCH_COUNT = 400
    //   RATING_CARC_SCALE = 0.5
    //   RATING_CARC_CORRECT_BASE = 20.0
    //   RATING_CARC_PLAYNUM_CORRECT_HIGH = 0.2
    //   RATING_CARC_PLAYNUM_CORRECT_LOW = 0.002

    // シナリオ1: 試合数400以上 → matchCorrect = HIGH = 0.2
    // fGetRate = 0.2 * (pointSum + selfCorrect) * 0.5
    [Fact]
    public void CalcGradeRating_HighMatchCnt_UsesHighCorrect()
    {
        // matchCnt=400, currRating=1500, ratingAvg=1500, pointSum=1000
        // selfCorrect = (1500-1500)/20.0 = 0.0
        // matchCorrect = 0.2 (HIGH)
        // getRate = 0.2 * (1000 + 0.0) * 0.5 = 100.0 → 100
        int result = _svc.CalcGradeRating(
            currRating: 1500,
            pointSum:   1000,
            matchCnt:   400,
            ratingAvg:  1500);
        Assert.Equal(1500 + 100, result); // 1600
    }

    // シナリオ2: 試合数0 → matchCorrect = 1.0 - 0 * LOW = 1.0
    [Fact]
    public void CalcGradeRating_ZeroMatchCnt_MatchCorrectIs1()
    {
        // matchCnt=0, pointSum=1000, selfCorrect=0, matchCorrect=1.0
        // getRate = 1.0 * 1000 * 0.5 = 500
        int result = _svc.CalcGradeRating(
            currRating: 1500,
            pointSum:   1000,
            matchCnt:   0,
            ratingAvg:  1500);
        Assert.Equal(1500 + 500, result); // 2000
    }

    // シナリオ3: 平均より低いレーティング → selfCorrect 正 → 加算
    // selfCorrect = (1600-1400)/20 = 10
    [Fact]
    public void CalcGradeRating_BelowAverage_SelfCorrectPositive()
    {
        // currRating=1400, ratingAvg=1600, pointSum=0, matchCnt=400
        // selfCorrect = (1600-1400)/20 = 10
        // matchCorrect = 0.2
        // getRate = 0.2 * (0 + 10) * 0.5 = 1.0 → 1
        int result = _svc.CalcGradeRating(
            currRating: 1400,
            pointSum:   0,
            matchCnt:   400,
            ratingAvg:  1600);
        Assert.Equal(1400 + 1, result); // 1401
    }

    // シナリオ4: 平均より高いレーティング → selfCorrect 負
    [Fact]
    public void CalcGradeRating_AboveAverage_SelfCorrectNegative()
    {
        // currRating=1600, ratingAvg=1400, pointSum=100, matchCnt=400
        // selfCorrect = (1400-1600)/20 = -10
        // matchCorrect = 0.2
        // getRate = 0.2 * (100 - 10) * 0.5 = 9.0 → 9
        int result = _svc.CalcGradeRating(
            currRating: 1600,
            pointSum:   100,
            matchCnt:   400,
            ratingAvg:  1400);
        Assert.Equal(1600 + 9, result); // 1609
    }

    // シナリオ5: 試合数200 → matchCorrect = 1.0 - 200 * 0.002 = 0.6
    [Fact]
    public void CalcGradeRating_MatchCnt200_CorrectIs0_6()
    {
        // matchCnt=200, matchCorrect = 1.0 - 200*0.002 = 0.6
        // pointSum=1000, selfCorrect=0, getRate = 0.6 * 1000 * 0.5 = 300
        int result = _svc.CalcGradeRating(
            currRating: 1500,
            pointSum:   1000,
            matchCnt:   200,
            ratingAvg:  1500);
        Assert.Equal(1500 + 300, result); // 1800
    }

    // シナリオ6: pointSum=0 + selfCorrect=0 → 変動なし
    [Fact]
    public void CalcGradeRating_NoScore_NoChange()
    {
        int result = _svc.CalcGradeRating(
            currRating: 1500,
            pointSum:   0,
            matchCnt:   400,
            ratingAvg:  1500);
        Assert.Equal(1500, result); // 変動なし
    }

    // シナリオ7: 旧実装との違いを確認 (回帰テスト)
    // 旧 Elo 式: rank=1 + matchCnt=10 + currRating=1500 → 異なる値
    [Fact]
    public void CalcGradeRating_NotUsingOldEloFormula()
    {
        // 新公式: matchCnt=400, pointSum=1000, same rating
        // 旧Elo: rank=1, matchCnt=400 → delta = (20.2 * (1.0-0.25)) / 0.5 = ~30.3
        // 新式: 0.2 * 1000 * 0.5 = 100
        int result = _svc.CalcGradeRating(
            currRating: 1500,
            pointSum:   1000,
            matchCnt:   400,
            ratingAvg:  1500);
        // 旧式では 1500 + ~30 = ~1530 付近
        // 新式では 1600 (100加算)
        Assert.Equal(1600, result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameConst グレードモード定数テスト
// 原典: HMajDef.h RATING_CARC_* defines
// ═══════════════════════════════════════════════════════════════════════════
public class GameConstGradeModeTests
{
    // 原典: RATING_CARC_MATCH_COUNT = 400
    [Fact]
    public void RatingCarcMatchCount_Is400()
        => Assert.Equal(400, GameConst.RatingCarcMatchCount);

    // 原典: RATING_CARC_SCALE = 0.5
    [Fact]
    public void RatingCarcScale_Is0_5()
        => Assert.Equal(0.5, GameConst.RatingCarcScale);

    // 原典: RATING_CARC_CORRECT_BASE = 20.0
    [Fact]
    public void RatingCarcCorrectBase_Is20()
        => Assert.Equal(20.0, GameConst.RatingCarcCorrectBase);

    // 原典: RATING_CARC_PLAYNUM_CORRECT_HIGH = 0.2
    [Fact]
    public void RatingCarcPlayNumCorrectHigh_Is0_2()
        => Assert.Equal(0.2, GameConst.RatingCarcPlayNumCorrectHigh);

    // 原典: RATING_CARC_PLAYNUM_CORRECT_LOW = 0.002
    [Fact]
    public void RatingCarcPlayNumCorrectLow_Is0_002()
        => Assert.Equal(0.002, GameConst.RatingCarcPlayNumCorrectLow);
}
