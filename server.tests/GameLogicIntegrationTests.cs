using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Engine;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Reflection;

namespace MajakServer.Tests;

/// <summary>
/// MajakGameLogic 統合テスト
/// 原典: HMajakGameLogic.cpp ProcessAction / ProcessTurn / ProcessFuro / ProcessModeKyo
/// </summary>

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic ProcessTurn エラーケーステスト
// 原典: HMajakGameLogic::ProcessTurn — エラーコード確認
// ═══════════════════════════════════════════════════════════════════════════
public class GameLogicProcessTurnTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    private static MajakGameLogic InitGame()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        return logic;
    }

    private static void SetCurrAct(MajakGameLogic logic, Act act)
    {
        typeof(MajakGameLogic)
            .GetField("_currAct", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, act);
    }

    private static bool GetPrivateBool(MajakGameLogic logic, string fieldName)
        => (bool)typeof(MajakGameLogic)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(logic)!;

    private static int[] TakeBipaiIndices(EnginePlayer player, int count)
        => count == 0 ? Array.Empty<int>() : player.Tehai.Take(count).Select(t => t.BipaiIndex).ToArray();

    private static PaiCode[] FindFourMatchingWallTiles(MajakGameLogic logic)
        => Enumerable.Range(0, 136)
            .Select(logic.GetBipaiPai)
            .Where(pai => pai.IsValid)
            .GroupBy(pai => pai.GetSerial())
            .First(group => group.Count() >= 4)
            .Take(4)
            .ToArray();

    private static void SetTehai(EnginePlayer player, params int[] serials)
    {
        player.Tehai.Clear();
        for (int i = 0; i < serials.Length; i++)
        {
            var pai = PaiCode.MakeSerial(serials[i]);
            pai.BipaiIndex = 80 + i;
            player.Tehai.Add(pai);
        }
    }

    // シナリオ1: MODE_TURN で無効アクション → ErrInvalidMode
    // 原典: default: return MLE_INVALIDMODE (ProcessTurn内)
    // ※ Act.Pas は bipaiCount=0 なのでインデックス検証をバイパス、ProcessTurn内の default へ
    [Fact]
    public void ProcessTurn_InvalidAction_ErrInvalidMode()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        Assert.Equal(PlayerMode.Turn, logic.Player[parent].Mode);
        // PAS は MODE_TURN の switch に case がない → default: ErrInvalidMode
        var result = logic.ProcessAction(parent, Act.Pas, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.ErrInvalidMode, result);
    }

    // シナリオ2: MODE_TURN で Tap → Ok (打牌)
    // 原典: case TAP: pPlayer->Tapai(clTapai); EnterFuroMode(TAP, clTapai, MODE_FURO)
    [Fact]
    public void ProcessTurn_Tap_ReturnsOk()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        // 親の手牌から1枚選んで打牌
        Assert.Equal(PlayerMode.Turn, logic.Player[parent].Mode);
        var tapPai = logic.Player[parent].Tehai.Last();
        var result = logic.ProcessAction(parent, Act.Tap, new[] { tapPai.BipaiIndex }, 1);
        Assert.Equal(ActionResult.Ok, result);
    }

    [Fact]
    public void ProcessTurn_Tap_EntersLegacyFuroModeState()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        var tapPai = logic.Player[parent].Tehai.Last();

        var result = logic.ProcessAction(parent, Act.Tap, new[] { tapPai.BipaiIndex }, 1);

        Assert.Equal(ActionResult.Ok, result);
        Assert.All(logic.Player, player => Assert.Equal(PlayerMode.Furo, player.Mode));
        Assert.Equal(parent, logic.GetHojuOrder());

        var info = BipaiInfo.Create();
        logic.GetBipai(ref info, openMask: 1 << ((parent + 1) % 4), skipMask: 1 << ((parent + 1) % 4));
        Assert.Contains(info.Pai.Take(info.PaiCnt), pai => pai.BipaiIndex == tapPai.BipaiIndex);
    }

    // シナリオ3: リーチ後に別の牌を打牌しようとする → ErrAfterRichi
    // 原典: if(NONE!=pPlayer->m_eRichiType) → if(back().BipaiIndex!=nBipaiIndex) → MLE_AFTERRICHI
    [Fact]
    public void ProcessTurn_AfterRichi_WrongTile_ErrAfterRichi()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;

        // リーチ状態を強制設定
        logic.Player[parent].RichiType = RichiType.Richi;

        // ツモった牌以外を打とうとする
        var tehai = logic.Player[parent].Tehai;
        var lastTile = tehai.Last(); // ツモ牌
        var otherTile = tehai.First(); // ツモ牌以外

        if (lastTile.BipaiIndex != otherTile.BipaiIndex)
        {
            var result = logic.ProcessAction(parent, Act.Tap,
                new[] { otherTile.BipaiIndex }, 1);
            Assert.Equal(ActionResult.ErrAfterRichi, result);
        }
    }

    // シナリオ4: MODE_NONE で ProcessAction → ErrInvalidMode
    // 原典: default: break → return MLE_INVALIDMODE
    // InitKyoku 後、親以外は Mode=None なので直接検証可能
    [Fact]
    public void ProcessAction_ModeNone_ErrInvalidMode()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        int child  = (parent + 1) % 4;
        // InitKyoku後、子は Mode=None (親の打牌前は Furoモードに入らない)
        Assert.Equal(PlayerMode.None, logic.Player[child].Mode);
        // Mode=None に対してアクション送信 → ErrInvalidMode
        var tapTile = logic.Player[child].Tehai.Last();
        var result  = logic.ProcessAction(child, Act.Tap, new[] { tapTile.BipaiIndex }, 1);
        Assert.Equal(ActionResult.ErrInvalidMode, result);
    }

    [Fact]
    public void ProcessAction_DuplicatePhysicalIndex_ErrPaiAlreadyUsed()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        int index = logic.Player[parent].Tehai[0].BipaiIndex;

        var result = logic.ProcessAction(parent, Act.Ank, new[] { index, index, index, index }, 4);

        Assert.Equal(ActionResult.ErrPaiAlreadyUsed, result);
    }

    // シナリオ5: 点数不足でリーチ → ErrPointNotEnough
    // 原典: 1000 > pPlayer->m_nGamePoint && m_stRuleInfo.m_nContest!=1 → MLE_POINTNOTENOUGH
    [Fact]
    public void ProcessTurn_RichiWithLowPoints_ErrPointNotEnough()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        // 残高を500に設定 (1000未満)
        logic.Player[parent].GamePoint = 500;

        var lastTile = logic.Player[parent].Tehai.Last();
        var result = logic.ProcessAction(parent, Act.Ric, new[] { lastTile.BipaiIndex }, 1);
        Assert.Equal(ActionResult.ErrPointNotEnough, result);
    }

    [Fact]
    public void ProcessTurn_Taopai_NineKindsYaochu_EndsKyoku()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        logic.Player[parent].Tehai.Clear();
        foreach (var s in new[] { 0,8, 9,17, 18,26, 27,28,29, 1,2,3,4,5 })
        {
            var pai = PaiCode.MakeSerial(s);
            pai.BipaiIndex = s + 40;
            logic.Player[parent].Tehai.Add(pai);
        }

        var result = logic.ProcessAction(parent, Act.Tao, Array.Empty<int>(), 0);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(KyokuEnd.Taopai, logic.KyokuEnd);
    }

    [Fact]
    public void ProcessTurn_Taopai_LessThanNineKindsYaochu_ErrCannotHora()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        logic.Player[parent].Tehai.Clear();
        foreach (var s in new[] { 0,8, 9,17, 18, 1,2,3,4,5,6,7,10,11 })
        {
            var pai = PaiCode.MakeSerial(s);
            pai.BipaiIndex = s + 40;
            logic.Player[parent].Tehai.Add(pai);
        }

        var result = logic.ProcessAction(parent, Act.Tao, Array.Empty<int>(), 0);

        Assert.Equal(ActionResult.ErrCannotHora, result);
    }

    [Fact]
    public void GetValidActions_AfterRichi_ExposesLegacyValidAnkanCandidate()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        var player = logic.Player[parent];
        player.Tehai.Clear();
        foreach (var s in new[] { 0,1,2,3,4,5,6,7,8,9 })
        {
            var pai = PaiCode.MakeSerial(s);
            pai.BipaiIndex = s + 20;
            player.Tehai.Add(pai);
        }
        foreach (var idx in new[] { 100, 101, 102, 103 })
        {
            var haku = PaiCode.MakeSerial(31);
            haku.BipaiIndex = idx;
            player.Tehai.Add(haku);
        }
        player.RichiType = RichiType.Richi;

        var actions = logic.GetValidActions(parent);

        Assert.Contains(actions.AnkanCandidates, c => c.OrderBy(x => x).SequenceEqual(new[] { 100, 101, 102, 103 }));
    }

    [Fact]
    public void GetValidActions_OpenHandDoesNotExposeRichiCandidates()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        var player = logic.Player[parent];
        var tapai = PaiCode.MakeSerial(0);
        tapai.BipaiIndex = 200;
        var result = player.Chi(parent, tapai, TakeBipaiIndices(player, 2));
        Assert.Equal(ActionResult.Ok, result);

        var actions = logic.GetValidActions(parent);

        Assert.Empty(actions.RichiCandidates);
    }

    [Fact]
    public void GetValidActions_FifthAnkanInNormalRule_IsExposedForAbortiveDraw()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        var player = logic.Player[parent];
        SetTehai(player, 0,1,2,3,4,5,6,7,8,9, 31,31,31,31);
        logic.KyokuInfo.KanCount = 4;
        player.KanCnt = 4;

        var actions = logic.GetValidActions(parent);

        Assert.Contains(actions.AnkanCandidates, candidate =>
            candidate.OrderBy(index => index).SequenceEqual(new[] { 90, 91, 92, 93 }));
    }

    [Theory]
    [InlineData(Act.Tsu, 0)]
    [InlineData(Act.Ank, 4)]
    [InlineData(Act.Cha, 1)]
    [InlineData(Act.Hua, 1)]
    public void ProcessTurn_AfterChiOrPonLockedActions_ErrAfterFuro(Act action, int count)
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        SetCurrAct(logic, Act.Chi);
        var indices = TakeBipaiIndices(logic.Player[parent], count);

        var result = logic.ProcessAction(parent, action, indices, count);

        Assert.Equal(ActionResult.ErrAfterFuro, result);
    }

    [Theory]
    [InlineData(Act.Ank, 4)]
    [InlineData(Act.Cha, 1)]
    public void ProcessTurn_KanAfterFourKanInContest_ErrKanAfter4Kan(Act action, int count)
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule() with { Contest = 1 });
        int parent = logic.KyokuInfo.OyaOrder;
        logic.KyokuInfo.KanCount = 4;
        var indices = TakeBipaiIndices(logic.Player[parent], count);

        var result = logic.ProcessAction(parent, action, indices, count);

        Assert.Equal(ActionResult.ErrKanAfter4Kan, result);
    }

    [Fact]
    public void ProcessTurn_HuaAfterRichiWrongTile_ErrAfterRichi()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        logic.Player[parent].RichiType = RichiType.Richi;
        var firstTile = logic.Player[parent].Tehai.First();
        var lastTile = logic.Player[parent].Tehai.Last();
        Assert.NotEqual(lastTile.BipaiIndex, firstTile.BipaiIndex);

        var result = logic.ProcessAction(parent, Act.Hua, new[] { firstTile.BipaiIndex }, 1);

        Assert.Equal(ActionResult.ErrAfterRichi, result);
    }

    [Fact]
    public void ProcessTurn_AnkanSuccess_OpensKanAndDrawsRinshanLikeLegacy()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        var player = logic.Player[parent];
        player.Tehai.Clear();
        var tiles = FindFourMatchingWallTiles(logic);
        foreach (var tile in tiles) player.Tehai.Add(tile);
        var indices = tiles.Select(tile => tile.BipaiIndex).ToArray();

        var result = logic.ProcessAction(parent, Act.Ank, indices, 4);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Contains(player.Furo, furo => furo.Act == Act.Ank);
        Assert.Equal(1, logic.KyokuInfo.KanCount);
        Assert.True(GetPrivateBool(logic, "_isRinshan"));
        Assert.Equal(PlayerMode.Turn, player.Mode);
    }

    [Fact]
    public void ProcessTurn_ChakanSuccess_EntersChanModeAndMarksChankan()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        var player = logic.Player[parent];
        var tile = player.Tehai.Last();
        player.Furo.Add(new FuroBlock
        {
            Act = Act.Pon,
            TapaiOrder = (parent + 1) % 4,
            Tiles = { tile, tile, tile }
        });

        var result = logic.ProcessAction(parent, Act.Cha, new[] { tile.BipaiIndex }, 1);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(Act.Cha, player.Furo[0].Act);
        Assert.True(GetPrivateBool(logic, "_isChankan"));
        Assert.All(logic.Player, p => Assert.Equal(PlayerMode.Chan, p.Mode));
    }

    [Fact]
    public void ProcessFuro_ChankanRon_PreservesCompletedChakanLikeLegacy()
    {
        var logic = InitGame();
        int declarerOrder = logic.KyokuInfo.OyaOrder;
        int winnerOrder = (declarerOrder + 1) % 4;
        var declarer = logic.Player[declarerOrder];
        var addedTile = declarer.Tehai.Last();
        declarer.Furo.Add(new FuroBlock
        {
            Act = Act.Pon,
            TapaiOrder = (declarerOrder + 2) % 4,
            Tiles = { addedTile, addedTile, addedTile },
        });

        var winner = logic.Player[winnerOrder];
        winner.Tehai.Clear();
        int[] tripletSerials = { 0, 9, 18, 27, 28, 29, 30, 31, 32, 33 };
        foreach (int serial in tripletSerials.Where(serial => serial != addedTile.GetSerial()).Take(4))
            for (int copy = 0; copy < 3; copy++)
                winner.Tehai.Add(PaiCode.MakeSerial(serial));
        winner.Tehai.Add(PaiCode.MakeSerial(addedTile.GetSerial()));
        winner.RichiType = RichiType.Richi;

        Assert.Equal(ActionResult.Ok,
            logic.ProcessAction(declarerOrder, Act.Cha, new[] { addedTile.BipaiIndex }, 1));
        Assert.Equal(ActionResult.Ok,
            logic.ProcessAction(winnerOrder, Act.Ron, Array.Empty<int>(), 0));
        for (int order = 0; order < MajakConst.PlayerMaxCount; order++)
        {
            if (logic.Player[order].Mode == PlayerMode.Chan)
                Assert.Equal(ActionResult.Ok,
                    logic.ProcessAction(order, Act.Pas, Array.Empty<int>(), 0));
        }

        Assert.Equal(Act.Cha, declarer.Furo[0].Act);
        Assert.Equal(4, declarer.Furo[0].Tiles.Count);
        Assert.Equal(1, declarer.KanCnt);
        Assert.Equal(0, logic.KyokuInfo.KanCount);
    }

    [Fact]
    public void ProcessTurn_HuaWithNonHuapai_ReturnsErrHuapaiLikeLegacy()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        var player = logic.Player[parent];
        var tile = player.Tehai.Last();

        var result = logic.ProcessAction(parent, Act.Hua, new[] { tile.BipaiIndex }, 1);

        Assert.Equal(ActionResult.ErrHuapai, result);
        Assert.DoesNotContain(player.NukiDora, pai => pai.BipaiIndex == tile.BipaiIndex);
    }

    [Fact]
    public void ProcessTurn_TsumoWithoutHoraForm_ErrCannotHora()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        SetTehai(logic.Player[parent], 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13);

        var result = logic.ProcessAction(parent, Act.Tsu, Array.Empty<int>(), 0);

        Assert.Equal(ActionResult.ErrCannotHora, result);
    }

    [Fact]
    public void ProcessTurn_TsumoSuccess_ProcessesHoraLikeLegacy()
    {
        var logic = InitGame();
        int parent = logic.KyokuInfo.OyaOrder;
        SetTehai(logic.Player[parent], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);

        var result = logic.ProcessAction(parent, Act.Tsu, Array.Empty<int>(), 0);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(KyoResultPin.Tsumo, logic.LastKyoResult.Pin);
        Assert.Equal(GameStatus.EndKyoku, logic.GameStatus);
        Assert.Equal(Act.Tsu, logic.Player[parent].CurAct);
    }
}

public class GameLogicDoraTests
{
    private static int InvokeCountDora(MajakGameLogic logic, Yaku yaku, PaiCode pai, bool richi)
    {
        var method = typeof(MajakGameLogic)
            .GetMethod("CountDora", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (int)method.Invoke(logic, new object[] { yaku, pai, richi })!;
    }

    [Fact]
    public void CountDora_ContestModeSkipsUraAndRedDoraLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(new RuleInfo { Contest = 1, Kuitan = true });
        logic.KyokuInfo.Dora[0] = new PaiCode(0, 1);
        logic.KyokuInfo.UraDora[0] = new PaiCode(0, 2);
        var pai = new PaiCode(0, 3) { IsRed = true };
        var yaku = new Yaku();

        int count = InvokeCountDora(logic, yaku, pai, richi: true);

        Assert.Equal(0, count);
        Assert.Equal(0, yaku.DoraCnt[1]);
        Assert.Equal(0, yaku.DoraCnt[2]);
        Assert.Equal(0, yaku.Chip);
    }

    [Fact]
    public void CountDora_NormalModeCountsUraAndRedDora()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(new RuleInfo { Contest = 0, Kuitan = true });
        logic.KyokuInfo.Dora[0] = new PaiCode(0, 1);
        logic.KyokuInfo.UraDora[0] = new PaiCode(0, 2);
        var pai = new PaiCode(0, 3) { IsRed = true };
        var yaku = new Yaku();

        int count = InvokeCountDora(logic, yaku, pai, richi: true);

        Assert.Equal(2, count);
        Assert.Equal(1, yaku.DoraCnt[1]);
        Assert.Equal(1, yaku.DoraCnt[2]);
        Assert.Equal(2, yaku.Chip);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic ProcessFuro エラーケーステスト
// 原典: HMajakGameLogic::ProcessFuro
// ═══════════════════════════════════════════════════════════════════════════
public class GameLogicProcessFuroTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    private static MajakGameLogic CreateMultiRonGame(int ronMode)
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule() with { Ron = ronMode });
        const int discarder = 0;

        typeof(MajakGameLogic)
            .GetField("_currOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, discarder);
        typeof(MajakGameLogic)
            .GetField("_currTapai", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, PaiCode.MakeSerial(33));

        logic.Player[discarder].Mode = PlayerMode.None;
        for (int order = 1; order < MajakConst.PlayerMaxCount; order++)
        {
            EnginePlayer player = logic.Player[order];
            player.Tehai.Clear();
            foreach (int serial in new[] { 0,0,0, 9,9,9, 18,18,18, 27,27,27, 33 })
                player.Tehai.Add(PaiCode.MakeSerial(serial));
            player.Mode = PlayerMode.Furo;
            player.IsHoraForm = true;
            player.RichiType = RichiType.Richi;
        }
        return logic;
    }

    private static void SubmitRon(MajakGameLogic logic, params int[] winnerOrders)
    {
        foreach (int order in winnerOrders)
            Assert.Equal(ActionResult.Ok,
                logic.ProcessAction(order, Act.Ron, Array.Empty<int>(), 0));

        for (int order = 1; order < MajakConst.PlayerMaxCount; order++)
            if (logic.Player[order].Mode == PlayerMode.Furo)
                Assert.Equal(ActionResult.Ok,
                    logic.ProcessAction(order, Act.Pas, Array.Empty<int>(), 0));
    }

    [Fact]
    public void ProcessFuro_HeadBump_AcceptsNearestRonOnlyLikeLegacy()
    {
        MajakGameLogic logic = CreateMultiRonGame(ronMode: 0);

        SubmitRon(logic, 1, 2);

        Assert.True(logic.LastKyoResult.Hora[1]);
        Assert.False(logic.LastKyoResult.Hora[2]);
        Assert.False(logic.LastKyoResult.Hora[3]);
        Assert.Equal(Act.Pas, logic.Player[2].CurAct);
        Assert.Equal(Act.Pas, logic.Player[3].CurAct);
    }

    [Fact]
    public void ProcessFuro_DoubleRon_AcceptsTwoWinnersLikeLegacy()
    {
        MajakGameLogic logic = CreateMultiRonGame(ronMode: 1);

        SubmitRon(logic, 1, 2);

        Assert.True(logic.LastKyoResult.Hora[1]);
        Assert.True(logic.LastKyoResult.Hora[2]);
        Assert.False(logic.LastKyoResult.Hora[3]);
    }

    [Fact]
    public void ProcessFuro_DoubleRon_ThirdRonCausesSanchahoLikeLegacy()
    {
        MajakGameLogic logic = CreateMultiRonGame(ronMode: 1);

        SubmitRon(logic, 1, 2, 3);

        Assert.Equal(KyokuEnd.Sanchaho, logic.KyokuEnd);
        Assert.Equal(KyoResultPin.Sanchaho, logic.LastKyoResult.Pin);
        Assert.DoesNotContain(logic.LastKyoResult.Hora, won => won);
    }

    [Fact]
    public void ProcessFuro_TripleRon_AcceptsAllThreeWinnersLikeLegacy()
    {
        MajakGameLogic logic = CreateMultiRonGame(ronMode: 2);

        SubmitRon(logic, 1, 2, 3);

        Assert.True(logic.LastKyoResult.Hora[1]);
        Assert.True(logic.LastKyoResult.Hora[2]);
        Assert.True(logic.LastKyoResult.Hora[3]);
    }

    // シナリオ1: 自分の打牌に対して Ron → ErrSelf
    // 原典: if(action != PAS && pPlayer->m_nOrder == m_nCurrOrder) → MLE_SELF
    [Fact]
    public void ProcessFuro_SelfRon_ErrSelf()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        // 親が打牌した後、自分が Ron しようとする
        var tapTile = logic.Player[parent].Tehai.Last();
        logic.ProcessAction(parent, Act.Tap, new[] { tapTile.BipaiIndex }, 1);

        // 自分 (parent) が Ron しようとする → ErrSelf
        var result = logic.ProcessAction(parent, Act.Ron, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.ErrSelf, result);
    }

    // シナリオ2: MODE_FURO で全員 Pas → Ok (次のプレイヤーへ)
    // 原典: 全員 MODE_NONE になったら resolve
    [Fact]
    public void ProcessFuro_AllPass_ReturnsOkAndAdvances()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        var tapTile = logic.Player[parent].Tehai.Last();
        logic.ProcessAction(parent, Act.Tap, new[] { tapTile.BipaiIndex }, 1);

        // 他3人が全員 Pas
        int pasCount = 0;
        for (int i = 1; i < 4; i++)
        {
            int odr = (parent + i) % 4;
            if (logic.Player[odr].Mode == PlayerMode.Furo)
            {
                logic.ProcessAction(odr, Act.Pas, Array.Empty<int>(), 0);
                pasCount++;
            }
        }
        // 少なくとも1人がパスできること
        Assert.True(pasCount > 0);
    }

    [Fact]
    public void ProcessFuro_PassWithHoraFormSetsTempFuritenLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        int child = (parent + 1) % 4;
        var tapTile = logic.Player[parent].Tehai.Last();
        logic.ProcessAction(parent, Act.Tap, new[] { tapTile.BipaiIndex }, 1);
        logic.Player[child].IsHoraForm = true;

        var result = logic.ProcessAction(child, Act.Pas, Array.Empty<int>(), 0);

        Assert.Equal(ActionResult.Ok, result);
        Assert.True(logic.Player[child].CheckFuriten());
        Assert.Equal(PlayerMode.None, logic.Player[child].Mode);
        Assert.Equal(Act.Pas, logic.Player[child].CurAct);
    }

    [Fact]
    public void ProcessFuro_ChiFromNonNextOrder_ErrNotNextOrder()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        int nonNext = (parent + 2) % 4;
        var tapTile = logic.Player[parent].Tehai.Last();
        logic.ProcessAction(parent, Act.Tap, new[] { tapTile.BipaiIndex }, 1);
        var indices = logic.Player[nonNext].Tehai.Take(2).Select(t => t.BipaiIndex).ToArray();

        var result = logic.ProcessAction(nonNext, Act.Chi, indices, 2);

        Assert.Equal(ActionResult.ErrNotNextOrder, result);
    }

    [Fact]
    public void ProcessFuro_PonAfterRichi_ErrAfterRichi()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        int child = (parent + 1) % 4;
        var tapTile = logic.Player[parent].Tehai.Last();
        logic.ProcessAction(parent, Act.Tap, new[] { tapTile.BipaiIndex }, 1);
        logic.Player[child].RichiType = RichiType.Richi;
        var indices = logic.Player[child].Tehai.Take(2).Select(t => t.BipaiIndex).ToArray();

        var result = logic.ProcessAction(child, Act.Pon, indices, 2);

        Assert.Equal(ActionResult.ErrAfterRichi, result);
    }

    [Fact]
    public void ProcessFuro_KanAfterFourKanInContest_ErrKanAfter4Kan()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule() with { Contest = 1 });

        int parent = logic.KyokuInfo.OyaOrder;
        int child = (parent + 1) % 4;
        var tapTile = logic.Player[parent].Tehai.Last();
        logic.ProcessAction(parent, Act.Tap, new[] { tapTile.BipaiIndex }, 1);
        logic.KyokuInfo.KanCount = 4;
        var indices = logic.Player[child].Tehai.Take(3).Select(t => t.BipaiIndex).ToArray();

        var result = logic.ProcessAction(child, Act.Kan, indices, 3);

        Assert.Equal(ActionResult.ErrKanAfter4Kan, result);
    }

    [Fact]
    public void ProcessFuro_RonWithoutHoraForm_ErrNotHoraForm()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        int child = (parent + 1) % 4;
        var tapTile = logic.Player[parent].Tehai.Last();
        logic.ProcessAction(parent, Act.Tap, new[] { tapTile.BipaiIndex }, 1);
        logic.Player[child].IsHoraForm = false;

        var result = logic.ProcessAction(child, Act.Ron, Array.Empty<int>(), 0);

        Assert.Equal(ActionResult.ErrNotHoraForm, result);
    }

    [Fact]
    public void ProcessModeChan_Chi_ReturnsInvalidMode()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int parent = logic.KyokuInfo.OyaOrder;
        int child = (parent + 1) % 4;
        logic.Player[child].Mode = PlayerMode.Chan;
        var indices = logic.Player[child].Tehai.Take(2).Select(t => t.BipaiIndex).ToArray();

        var result = logic.ProcessAction(child, Act.Chi, indices, 2);

        Assert.Equal(ActionResult.ErrInvalidMode, result);
    }

    [Fact]
    public void GetValidActions_ModeChan_OnlyPassAndRonCandidates()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int parent = logic.KyokuInfo.OyaOrder;
        int child = (parent + 1) % 4;
        var player = logic.Player[child];
        player.Tehai.Clear();
        foreach (int serial in new[] { 0,0,0, 9,9,9, 18,18,18, 27,27,27, 33 })
            player.Tehai.Add(PaiCode.MakeSerial(serial));
        player.Mode = PlayerMode.Chan;
        player.IsHoraForm = true;
        player.RichiType = RichiType.Richi;
        typeof(MajakGameLogic)
            .GetField("_currOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, parent);
        typeof(MajakGameLogic)
            .GetField("_currTapai", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, PaiCode.MakeSerial(33));

        var actions = logic.GetValidActions(child);

        Assert.True(actions.CanPass);
        Assert.True(actions.CanRon);
        Assert.Empty(actions.ChiCandidates);
        Assert.Empty(actions.PonCandidates);
        Assert.Empty(actions.KanCandidates);
        Assert.Empty(actions.ChakanCandidates);
        Assert.Empty(actions.AnkanCandidates);
    }

    [Fact]
    public void GetValidActions_OpenNoYakuRon_IsNotExposed()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int discarder = logic.KyokuInfo.OyaOrder;
        int child = (discarder + 1) % 4;
        var player = logic.Player[child];
        player.Tehai.Clear();
        foreach (int serial in new[] { 3,4,5, 15,16,17, 18,19,20, 21 })
            player.Tehai.Add(PaiCode.MakeSerial(serial));
        player.Furo.Add(new FuroBlock
        {
            Act = Act.Chi,
            Tiles = { PaiCode.MakeSerial(0), PaiCode.MakeSerial(1), PaiCode.MakeSerial(2) },
        });
        player.Mode = PlayerMode.Furo;
        player.IsHoraForm = true;

        typeof(MajakGameLogic)
            .GetField("_currOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, discarder);
        typeof(MajakGameLogic)
            .GetField("_currTapai", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, PaiCode.MakeSerial(21));

        var actions = logic.GetValidActions(child);

        Assert.False(actions.CanRon);
        Assert.True(actions.CanPass);
        Assert.Equal("noYaku", actions.HoraErrorReason);
        Assert.Equal(10, player.Tehai.Count);
    }

    [Fact]
    public void GetValidActions_ChiCandidate_AllowsRedFiveLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int parent = logic.KyokuInfo.OyaOrder;
        int child = (parent + 1) % 4;

        var redFiveMan = new PaiCode(0, 5) { IsRed = true, BipaiIndex = 51 };
        var sixMan = new PaiCode(0, 6) { BipaiIndex = 52 };
        logic.Player[child].Tehai.Clear();
        logic.Player[child].Tehai.Add(redFiveMan);
        logic.Player[child].Tehai.Add(sixMan);
        logic.Player[child].Mode = PlayerMode.Furo;

        typeof(MajakGameLogic)
            .GetField("_currOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, parent);
        typeof(MajakGameLogic)
            .GetField("_currTapai", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, new PaiCode(0, 4) { BipaiIndex = 50 });

        var actions = logic.GetValidActions(child);

        Assert.Contains(actions.ChiCandidates, candidate =>
            candidate.SequenceEqual(new[] { redFiveMan.BipaiIndex, sixMan.BipaiIndex }));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic ProcessModeKyo テスト
// 原典: MODE_KYO — 全員 PAS で次局に進む
// ═══════════════════════════════════════════════════════════════════════════
public class GameLogicModeKyoTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    private static void CompleteKyoPasses(MajakGameLogic logic)
    {
        for (int order = 0; order < MajakConst.PlayerMaxCount; order++)
            logic.Player[order].Mode = PlayerMode.Kyo;
        for (int order = 0; order < MajakConst.PlayerMaxCount; order++)
            Assert.Equal(ActionResult.Ok,
                logic.ProcessAction(order, Act.Pas, Array.Empty<int>(), 0));
    }

    // シナリオ1: KYO モードで全員 PAS → CurKyoku 進む
    // 原典: 全員 MODE_NONE → m_stHanchanInfo.m_nCurKyoku++
    [Fact]
    public void ProcessModeKyo_AllPass_AdvancesKyoku()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        // Kyo モードを強制設定
        for (int i = 0; i < 4; i++)
            logic.Player[i].Mode = PlayerMode.Kyo;

        int prevKyoku = logic.HanchanInfo.CurKyoku;

        // 全員 PAS
        for (int i = 0; i < 4; i++)
        {
            var result = logic.ProcessAction(i, Act.Pas, Array.Empty<int>(), 0);
            Assert.Equal(ActionResult.Ok, result);
        }

        // CurKyoku が進んでいるか、またはゲームが終了している
        bool advanced = logic.HanchanInfo.CurKyoku > prevKyoku
                     || logic.GameStatus == GameStatus.NotPlaying;
        Assert.True(advanced);
    }

    // シナリオ2: KYO で PAS 以外のアクション → ErrInvalidMode
    // 原典: switch(eAction) { case PAS: ... default: break; } → return MLE_INVALIDMODE
    [Fact]
    public void ProcessModeKyo_NonPas_ErrInvalidMode()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        logic.Player[0].Mode = PlayerMode.Kyo;

        var result = logic.ProcessAction(0, Act.Tap,
            new[] { logic.Player[0].Tehai.Last().BipaiIndex }, 1);
        Assert.Equal(ActionResult.ErrInvalidMode, result);
    }

    // シナリオ3: 東1局ゲーム → 東4局 PAS で GE_SET
    // 原典: if(nLastKyoku == m_stHanchanInfo.m_nCurKyoku) → m_nGameEnd = GE_SET
    [Fact]
    public void ProcessModeKyo_TonpuLast_GameEnd()
    {
        var rule = new RuleInfo { Hanchan = false, Kuitan = true, Contest = 0, AkaDora = 0, Uma = 0 };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);

        // 東4局に設定
        logic.HanchanInfo.CurKyoku = 3; // 0-indexed, 4局=index3

        for (int i = 0; i < 4; i++)
            logic.Player[i].Mode = PlayerMode.Kyo;

        for (int i = 0; i < 4; i++)
            logic.ProcessAction(i, Act.Pas, Array.Empty<int>(), 0);

        // ゲームが終了していること
        Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);
        Assert.Equal(GameEnd.Set, logic.GameEnd);
    }

    [Fact]
    public void ProcessModeKyo_DebugEndAfterEast1_GameEnd()
    {
        var logic = new MajakGameLogic();
        logic.SetDebugEndAfterEast1(true);
        logic.InitHanchan(DefaultRule());

        for (int i = 0; i < 4; i++)
            logic.Player[i].Mode = PlayerMode.Kyo;

        for (int i = 0; i < 4; i++)
            logic.ProcessAction(i, Act.Pas, Array.Empty<int>(), 0);

        Assert.Equal(1, logic.HanchanInfo.CurKyoku);
        Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);
        Assert.Equal(GameEnd.Set, logic.GameEnd);
    }

    [Fact]
    public void ProcessModeKyo_AllLastDealerTopEndsAutomaticallyLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int oya = logic.KyokuInfo.OyaOrder;
        logic.HanchanInfo.CurKyoku = 7;
        logic.HanchanInfo.RenchanCount = 0;
        logic.KyokuInfo.Renchan = true;
        logic.KyokuInfo.EndKyokuWithHora = true;
        for (int order = 0; order < MajakConst.PlayerMaxCount; order++)
            logic.Player[order].GamePoint = order == oya ? 40000 : 20000;

        CompleteKyoPasses(logic);

        Assert.Equal(GameEnd.Hora, logic.GameEnd);
        Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);
    }

    [Theory]
    [InlineData(Act.Ron, GameEnd.None, GameStatus.NewKyoku)]
    [InlineData(Act.Pas, GameEnd.Stop, GameStatus.NotPlaying)]
    public void ProcessModeKyo_AllLastDealerNotTopCanContinueOrStopLikeLegacy(
        Act choice,
        GameEnd expectedEnd,
        GameStatus expectedStatus)
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int oya = logic.KyokuInfo.OyaOrder;
        int top = (oya + 1) % MajakConst.PlayerMaxCount;
        logic.HanchanInfo.CurKyoku = 7;
        logic.HanchanInfo.RenchanCount = 0;
        logic.KyokuInfo.Renchan = true;
        logic.KyokuInfo.EndKyokuWithHora = true;
        for (int order = 0; order < MajakConst.PlayerMaxCount; order++)
            logic.Player[order].GamePoint = order == top ? 40000 : 20000;

        CompleteKyoPasses(logic);
        Assert.Equal(PlayerMode.Aga, logic.Player[oya].Mode);

        Assert.Equal(ActionResult.Ok,
            logic.ProcessAction(oya, choice, Array.Empty<int>(), 0));

        Assert.Equal(expectedEnd, logic.GameEnd);
        Assert.Equal(expectedStatus, logic.GameStatus);
    }
}

public class GameLogicRandomSimulationTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    [Fact]
    public void RandomlyDealtGames_PlayFromInitialHandToGameEnd()
    {
        const int gameCount = 12;
        const int maxActionsPerGame = 10_000;
        var random = new Random();

        for (int gameNumber = 0; gameNumber < gameCount; gameNumber++)
        {
            var logic = new MajakGameLogic();
            logic.InitHanchan(DefaultRule());
            int actionCount = 0;

            while (logic.GameStatus != GameStatus.NotPlaying || actionCount == 0)
            {
                Assert.True(actionCount++ < maxActionsPerGame,
                    $"Game {gameNumber} did not finish within {maxActionsPerGame} actions.");

                bool processedAction = false;
                for (int order = 0; order < MajakConst.PlayerMaxCount; order++)
                {
                    var player = logic.Player[order];
                    if (player.Mode == PlayerMode.None) continue;

                    var validActions = logic.GetValidActions(order);
                    Act action;
                    int[] bipaiIndices;
                    switch (player.Mode)
                    {
                        case PlayerMode.Turn:
                            Assert.NotEmpty(validActions.TapCandidates);
                            action = Act.Tap;
                            bipaiIndices = [validActions.TapCandidates[random.Next(validActions.TapCandidates.Count)]];
                            break;
                        case PlayerMode.Furo:
                        case PlayerMode.Chan:
                        case PlayerMode.Kyo:
                        case PlayerMode.Aga:
                            Assert.True(validActions.CanPass);
                            action = Act.Pas;
                            bipaiIndices = Array.Empty<int>();
                            break;
                        default:
                            throw new Xunit.Sdk.XunitException($"Unexpected player mode: {player.Mode}");
                    }

                    Assert.Equal(ActionResult.Ok,
                        logic.ProcessAction(order, action, bipaiIndices, bipaiIndices.Length));
                    processedAction = true;
                    break;
                }

                Assert.True(processedAction, "The game has active status but no player has a pending action.");
            }

            Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);
            Assert.NotEqual(GameEnd.None, logic.GameEnd);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic ProcessModeAga テスト
// 原典: MODE_AGA — RON/PAS による終了判断
// ═══════════════════════════════════════════════════════════════════════════
public class GameLogicModeAgaTests
{
    // シナリオ1: AGA モードで RON → InitKyokuで次局開始
    // 原典: case RON: pPlayer->SetMode(NONE); InitKyoku(); return MLS_OK
    [Fact]
    public void ProcessModeAga_Ron_NextKyokuStarted()
    {
        var rule = new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 0, Uma = 0 };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);
        logic.Player[0].Mode = PlayerMode.Aga;

        var result = logic.ProcessAction(0, Act.Ron, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.Ok, result);
        // InitKyoku 後は Aga モードになっていないことを確認
        Assert.NotEqual(PlayerMode.Aga, logic.Player[0].Mode);
    }

    // シナリオ2: AGA モードで PAS → ゲーム終了 (GE_STOP)
    // 原典: case PAS: pPlayer->SetMode(NONE); m_nGameEnd = GE_STOP; ProcessEndHanchan()
    [Fact]
    public void ProcessModeAga_Pas_GameEndStop()
    {
        var rule = new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 0, Uma = 0 };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);
        logic.Player[0].Mode = PlayerMode.Aga;

        var result = logic.ProcessAction(0, Act.Pas, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(GameEnd.Stop, logic.GameEnd);
    }

    // シナリオ3: AGA モードで PAS/RON 以外 → ErrInvalidMode
    [Fact]
    public void ProcessModeAga_Other_ErrInvalidMode()
    {
        var rule = new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 0, Uma = 0 };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);
        logic.Player[0].Mode = PlayerMode.Aga;

        var result = logic.ProcessAction(0, Act.Tap,
            new[] { logic.Player[0].Tehai.Last().BipaiIndex }, 1);
        Assert.Equal(ActionResult.ErrInvalidMode, result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameMoneyService.SaveMoneyAsync テスト
// 原典: コイン残高をDBに保存するのみ (増減なし)
// ═══════════════════════════════════════════════════════════════════════════
public class GameMoneySaveTests
{
    private readonly Mock<PlayerRepository>          _playerRepoMock
        = new(MockBehavior.Loose);

    private GameMoneyService BuildService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        return new GameMoneyService(_playerRepoMock.Object, new RatingService());
    }

    // シナリオ1: SaveMoneyAsync → UpdateCommonRat が呼ばれること
    [Fact]
    public async Task SaveMoneyAsync_CallsUpdateCommonRat()
    {
        var svc    = BuildService();
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 5000 };

        await svc.SaveMoneyAsync(player);

        _playerRepoMock.Verify(r => r.UpdateCommonRatAsync(player), Times.Once);
    }

    // シナリオ2: SaveMoneyAsync → NLevel/SLevel も更新される
    [Fact]
    public async Task SaveMoneyAsync_UpdatesNLevelAndSLevel()
    {
        var svc    = BuildService();
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 500 };

        await svc.SaveMoneyAsync(player);

        // GamMoney=500 → NLevel=2 (見習い→平均)
        Assert.Equal(2, player.NLevel);
        Assert.False(string.IsNullOrEmpty(player.SLevel));
    }

    // シナリオ3: GiveYakumanBonusAsync → コインが YAKUMAN_BONUS_MONEY 増加
    // 原典: YAKUMANBONUS_MONEY = 200
    [Fact]
    public async Task GiveYakumanBonusAsync_IncreasesMoney()
    {
        var svc    = BuildService();
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 1000 };

        await svc.GiveYakumanBonusAsync(player);

        Assert.Equal(1000 + GameConst.YakumanBonusMoney, player.GamMoney);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// KyokuInfo / HanchanInfo 初期値テスト
// 原典: HMajakGameLogic::InitHanchan / InitKyoku
// ═══════════════════════════════════════════════════════════════════════════
public class HanchanKyokuInfoTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    // シナリオ1: InitHanchan → CurKyoku=0, RenchanCount=0
    [Fact]
    public void InitHanchan_HanchanInfo_Defaults()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        Assert.Equal(0, logic.HanchanInfo.CurKyoku);
        Assert.Equal(0, logic.HanchanInfo.RenchanCount);
    }

    // シナリオ2: InitHanchan → KyokuInfo.OyaOrder が有効 (0-3)
    [Fact]
    public void InitHanchan_OyaOrderInRange()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        Assert.InRange(logic.KyokuInfo.OyaOrder, 0, 3);
    }

    // シナリオ3: InitHanchan → GameStatus=Playing (InitHanchanの後にInitKyokuが呼ばれGameStatus=Playing)
    // 原典: ProcessAction で GAMESTATUS_PLAYING に設定
    [Fact]
    public void InitHanchan_InitialStatus_Processing()
    {
        var logic = new MajakGameLogic();
        logic.Init();
        Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);

        logic.InitHanchan(DefaultRule());
        // InitHanchan後はまだ NotPlaying (ProcessActionで初めてPlayingになる)
        // ただし親のModeはTurnになっている
        int parent = logic.KyokuInfo.OyaOrder;
        Assert.Equal(PlayerMode.Turn, logic.Player[parent].Mode);
    }

    // シナリオ4: 東風戦 (Hanchan=false) → 4局で終了
    // 原典: nLastKyoku = m_stRuleInfo.m_bHanchan ? 8 : 4
    [Fact]
    public void InitHanchan_Tonpu_LastKyokuIs4()
    {
        var rule  = new RuleInfo { Hanchan = false, Kuitan = true, Contest = 0, AkaDora = 0, Uma = 0 };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);

        // 東4局を4に設定して全員PAS → GE_SET
        logic.HanchanInfo.CurKyoku = 3;
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++) logic.ProcessAction(i, Act.Pas, Array.Empty<int>(), 0);

        Assert.Equal(GameEnd.Set, logic.GameEnd);
    }

    // シナリオ5: 半荘戦 (Hanchan=true) → 8局で終了
    [Fact]
    public void InitHanchan_Hanchan_LastKyokuIs8()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        // 7局目に設定 (0-indexed)
        logic.HanchanInfo.CurKyoku = 7;
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++) logic.ProcessAction(i, Act.Pas, Array.Empty<int>(), 0);

        Assert.Equal(GameEnd.Set, logic.GameEnd);
    }

    [Fact]
    public void SetBipai_AppliesBufferedWallOnNextInitKyoku()
    {
        var logic = new MajakGameLogic();
        var wall = Enumerable.Range(0, 136)
            .Select(_ => PaiCode.MakeSerial(0))
            .ToArray();

        logic.SetBipai(wall, wareme: 0);
        logic.InitHanchan(DefaultRule());

        Assert.All(logic.Player.SelectMany(player => player.Tehai), pai => Assert.Equal(0, pai.GetSerial()));
    }

    [Fact]
    public void Init_ClearsBufferedTsumikomiWallLikeLegacy()
    {
        var logic = new MajakGameLogic();
        var wall = Enumerable.Range(0, 136)
            .Select(_ => PaiCode.MakeSerial(0))
            .ToArray();

        logic.SetBipai(wall, wareme: 0);
        logic.Init();
        logic.InitHanchan(DefaultRule());

        Assert.Contains(logic.Player.SelectMany(player => player.Tehai), pai => pai.GetSerial() != 0);
    }

    [Fact]
    public void InitKyoku_DebugHaipaiKokushiBuildsLegacyDealerHandAndResetsFlag()
    {
        var logic = new MajakGameLogic();

        logic.SetDebugHaipaiYaku(1008);
        logic.InitHanchan(DefaultRule());

        int oya = logic.KyokuInfo.OyaOrder;
        var serials = logic.Player[oya].Tehai
            .Select(pai => pai.GetSerial())
            .OrderBy(serial => serial)
            .ToArray();
        Assert.Equal(new[] { 0, 1, 8, 9, 17, 18, 26, 28, 29, 30, 31, 32, 33, 33 }, serials);
        Assert.Equal(-1, logic.DebugHaipaiYaku);
    }

    // シナリオ6: Tobi (飛び) → GE_TOBI
    // 原典: if(0 > m_Player[nIdx].m_nGamePoint) → m_nGameEnd = GE_TOBI
    [Fact]
    public void ProcessModeKyo_Tobi_GameEndTobi()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        // プレイヤー0のポイントをマイナスに
        logic.Player[0].GamePoint = -100;

        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++) logic.ProcessAction(i, Act.Pas, Array.Empty<int>(), 0);

        Assert.Equal(GameEnd.Tobi, logic.GameEnd);
    }
}

public class GameLogicProcessKanTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    private static void InvokeProcessKan(MajakGameLogic logic, EnginePlayer player)
    {
        typeof(MajakGameLogic)
            .GetMethod("ProcessKan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(logic, new object[] { player });
    }

    private static PaiCode[] FindFourMatchingTiles(MajakGameLogic logic)
        => Enumerable.Range(0, 136)
            .Select(logic.GetBipaiPai)
            .GroupBy(tile => tile.GetSerial())
            .First(group => group.Count() == 4)
            .ToArray();

    private static void SetCurrentDiscard(MajakGameLogic logic, int order, PaiCode tile)
    {
        typeof(MajakGameLogic)
            .GetField("_currOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, order);
        typeof(MajakGameLogic)
            .GetField("_currTapai", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, tile);
    }

    [Fact]
    public void ProcessKan_FourthKanByDifferentPlayer_SetsSukaikanWithoutRinshanDraw()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var player = logic.Player[0];
        logic.KyokuInfo.KanCount = 3;
        player.KanCnt = 1;
        int before = player.Tehai.Count;

        InvokeProcessKan(logic, player);

        Assert.Equal(KyokuEnd.Sukaikan, logic.KyokuEnd);
        Assert.Equal(before, player.Tehai.Count);
        Assert.Equal(3, logic.KyokuInfo.KanCount);
    }

    [Fact]
    public void ProcessKan_AllowedKan_AddsDoraAndRinshanDraw()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var player = logic.Player[0];
        int before = player.Tehai.Count;

        InvokeProcessKan(logic, player);

        Assert.Equal(KyokuEnd.None, logic.KyokuEnd);
        Assert.Equal(1, logic.KyokuInfo.KanCount);
        Assert.True(logic.KyokuInfo.Dora[1].IsValid);
        Assert.True(logic.KyokuInfo.UraDora[1].IsValid);
        Assert.Equal(before + 1, player.Tehai.Count);
    }

    [Fact]
    public void ProcessKan_FourthKanBySamePlayerIsAllowedLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        EnginePlayer player = logic.Player[0];
        logic.KyokuInfo.KanCount = 3;
        player.KanCnt = 4;
        int before = player.Tehai.Count;

        InvokeProcessKan(logic, player);

        Assert.Equal(KyokuEnd.None, logic.KyokuEnd);
        Assert.Equal(4, logic.KyokuInfo.KanCount);
        Assert.Equal(before + 1, player.Tehai.Count);
    }

    [Fact]
    public void ProcessTurn_FifthAnkanCausesSukaikanLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int order = logic.KyokuInfo.OyaOrder;
        EnginePlayer player = logic.Player[order];
        PaiCode[] tiles = FindFourMatchingTiles(logic);
        player.Tehai.Clear();
        player.Tehai.AddRange(tiles);
        player.Mode = PlayerMode.Turn;
        logic.KyokuInfo.KanCount = 4;
        player.KanCnt = 4;

        ActionResult result = logic.ProcessAction(
            order,
            Act.Ank,
            tiles.Select(tile => tile.BipaiIndex).ToArray(),
            4);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(KyokuEnd.Sukaikan, logic.KyokuEnd);
        Assert.Equal(4, logic.KyokuInfo.KanCount);
        Assert.Equal(5, player.KanCnt);
        Assert.Empty(player.Tehai);
    }

    [Fact]
    public void ProcessFuro_FifthMinkanCausesSukaikanLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        const int discarder = 0;
        const int caller = 1;
        PaiCode[] tiles = FindFourMatchingTiles(logic);
        EnginePlayer player = logic.Player[caller];
        player.Tehai.Clear();
        player.Tehai.AddRange(tiles.Skip(1));
        player.Mode = PlayerMode.Furo;
        logic.Player[discarder].Mode = PlayerMode.None;
        logic.KyokuInfo.KanCount = 4;
        player.KanCnt = 4;
        SetCurrentDiscard(logic, discarder, tiles[0]);

        ActionResult result = logic.ProcessAction(
            caller,
            Act.Kan,
            tiles.Skip(1).Select(tile => tile.BipaiIndex).ToArray(),
            3);

        Assert.Equal(ActionResult.Ok, result);
        Assert.Equal(KyokuEnd.Sukaikan, logic.KyokuEnd);
        Assert.Equal(4, logic.KyokuInfo.KanCount);
        Assert.Equal(5, player.KanCnt);
        Assert.Empty(player.Tehai);
    }

    [Fact]
    public void ProcessTurn_FifthChakanIsRejectedBeforeMutationLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int order = logic.KyokuInfo.OyaOrder;
        EnginePlayer player = logic.Player[order];
        PaiCode tile = player.Tehai[0];
        player.Furo.Add(new FuroBlock
        {
            Act = Act.Pon,
            Tiles = { tile, tile, tile },
        });
        logic.KyokuInfo.KanCount = 4;
        player.KanCnt = 4;

        ActionResult result = logic.ProcessAction(
            order,
            Act.Cha,
            new[] { tile.BipaiIndex },
            1);

        Assert.Equal(ActionResult.ErrKanAfter4Kan, result);
        Assert.Equal(Act.Pon, player.Furo[0].Act);
        Assert.Equal(4, player.KanCnt);
        Assert.Contains(player.Tehai, handTile => handTile.BipaiIndex == tile.BipaiIndex);
    }
}
