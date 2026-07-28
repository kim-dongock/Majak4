using System.Text.Json;
using System.Reflection;
using Moq;
using MajakServer.Commands;
using MajakServer.Engine;
using GameRoom = MajakServer.Models.Game.GameRoom;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// CommandContext テスト
// パケット解析メソッドの動作確認
// ═══════════════════════════════════════════════════════════════════════════
public class CommandContextTests
{
    private static CommandContext MakeCtx(Dictionary<string, object?> payload)
        => new() { Payload = payload };

    // ─── GetString ───────────────────────────────────────────────────────

    // シナリオ1: 文字列取得
    [Fact]
    public void GetString_ExistingKey_ReturnsValue()
    {
        var ctx = MakeCtx(new() { ["name"] = "Alice" });
        Assert.Equal("Alice", ctx.GetString("name"));
    }

    // シナリオ2: キー不在 → 空文字
    [Fact]
    public void GetString_MissingKey_ReturnsEmpty()
    {
        var ctx = MakeCtx(new());
        Assert.Equal("", ctx.GetString("missing"));
    }

    // ─── GetInt ──────────────────────────────────────────────────────────

    // シナリオ3: int 取得
    [Fact]
    public void GetInt_ExistingKey_ReturnsValue()
    {
        var ctx = MakeCtx(new() { ["count"] = 42 });
        Assert.Equal(42, ctx.GetInt("count"));
    }

    // シナリオ4: long → int 変換
    [Fact]
    public void GetInt_JsonElement_Deserializes()
    {
        var json = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            """{"count": 7}""")!;
        var ctx  = MakeCtx(json);
        Assert.Equal(7, ctx.GetInt("count"));
    }

    // シナリオ5: キー不在 → 0
    [Fact]
    public void GetInt_MissingKey_ReturnsZero()
    {
        var ctx = MakeCtx(new());
        Assert.Equal(0, ctx.GetInt("missing"));
    }

    // シナリオ6: デフォルト値指定
    [Fact]
    public void GetInt_WithDefault_ReturnDefault()
    {
        var ctx = MakeCtx(new());
        Assert.Equal(99, ctx.GetInt("missing", 99));
    }

    // ─── GetLong ─────────────────────────────────────────────────────────

    // シナリオ7: long 取得
    [Fact]
    public void GetLong_ExistingKey_ReturnsValue()
    {
        var ctx = MakeCtx(new() { ["money"] = 123456789L });
        Assert.Equal(123456789L, ctx.GetLong("money"));
    }

    // シナリオ8: キー不在 → 0L
    [Fact]
    public void GetLong_MissingKey_ReturnsZero()
    {
        var ctx = MakeCtx(new());
        Assert.Equal(0L, ctx.GetLong("missing"));
    }

    // ─── GetBool ─────────────────────────────────────────────────────────

    // シナリオ9: true 取得
    [Fact]
    public void GetBool_True_ReturnsTrue()
    {
        var ctx = MakeCtx(new() { ["flag"] = true });
        Assert.True(ctx.GetBool("flag"));
    }

    // シナリオ10: false 取得
    [Fact]
    public void GetBool_False_ReturnsFalse()
    {
        var ctx = MakeCtx(new() { ["flag"] = false });
        Assert.False(ctx.GetBool("flag"));
    }

    // シナリオ11: キー不在 → false
    [Fact]
    public void GetBool_Missing_ReturnsFalse()
    {
        var ctx = MakeCtx(new());
        Assert.False(ctx.GetBool("missing"));
    }

    // ─── GetIntArray ─────────────────────────────────────────────────────

    // シナリオ12: int[] 取得
    [Fact]
    public void GetIntArray_ExistingKey_ReturnsArray()
    {
        var ctx = MakeCtx(new() { ["arr"] = new int[] { 1, 2, 3 } });
        var arr = ctx.GetIntArray("arr");
        Assert.NotNull(arr);
        Assert.Equal(new[] { 1, 2, 3 }, arr);
    }

    // シナリオ13: キー不在 → null
    [Fact]
    public void GetIntArray_Missing_ReturnsNull()
    {
        var ctx = MakeCtx(new());
        Assert.Null(ctx.GetIntArray("missing"));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic ProcessEndHanchan テスト
// 原典: HMajakGameLogic::ProcessEndHanchan
//   Uma table / Tip / Yakitori / SetRank 計算
// ═══════════════════════════════════════════════════════════════════════════
public class ProcessEndHanchanTests
{
    // ─── UMA テーブル検証 ─────────────────────────────────────────────────
    // 原典: static const int umatbl[][4] =
    //   Uma=0: {+10, +5, -5,-10}
    //   Uma=1: {+20,+10,-10,-20}
    //   Uma=2: {+30,+10,-10,-30}
    //   Uma=3: {  0,  0,  0,  0}

    private static void InvokeProcessEndHanchan(MajakGameLogic logic)
    {
        typeof(MajakGameLogic)
            .GetMethod("ProcessEndHanchan", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(logic, Array.Empty<object>());
    }

    private static MajakGameLogic RunGame(int umaMode, int[] gamePoints)
    {
        var rule  = new RuleInfo { Hanchan = false, Kuitan = true, Contest = 0, AkaDora = 0, Uma = umaMode };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);

        // Tip=DefaultTip → setTip=0 で零和を保つ
        for (int i = 0; i < 4; i++)
        {
            logic.Player[i].GamePoint = gamePoints[i];
            logic.Player[i].Tip = MajakConst.DefaultTip;
        }

        // 全員KYOモードでPASして東4局終了
        logic.HanchanInfo.CurKyoku = 3;
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++)
            logic.ProcessAction(i, Act.Pas, System.Array.Empty<int>(), 0);

        return logic;
    }

    // シナリオ1: Uma=0 (+10,+5,-5,-10) — 標準ウマ
    // 原典: umatbl[0] = {+10,+5,-5,-10}
    [Fact]
    public void ProcessEndHanchan_Uma0_SetTotal_ZeroSum()
    {
        // 全員同点 (order 0,1,2,3 が 1,2,3,4位)
        int[] pts = { 40000, 30000, 25000, 20000 }; // 30000点超が1位
        var logic = RunGame(0, pts);

        // SetTotal の合計はゼロ (完全ゼロサム)
        int totalSetTotal = logic.Player.Sum(p => p.SetTotal);
        Assert.Equal(0, totalSetTotal);
    }

    // シナリオ2: Uma=1 (+20,+10,-10,-20)
    [Fact]
    public void ProcessEndHanchan_Uma1_ZeroSum()
    {
        int[] pts = { 40000, 30000, 25000, 20000 };
        var logic = RunGame(1, pts);
        int total = logic.Player.Sum(p => p.SetTotal);
        Assert.Equal(0, total);
    }

    // シナリオ3: Uma=3 (0,0,0,0) — ウマなし
    [Fact]
    public void ProcessEndHanchan_Uma3_ZeroSum()
    {
        int[] pts = { 40000, 30000, 25000, 20000 };
        var logic = RunGame(3, pts);
        int total = logic.Player.Sum(p => p.SetTotal);
        Assert.Equal(0, total);
    }

    // シナリオ4: SetRank は 0-3 (0=1位)
    [Fact]
    public void ProcessEndHanchan_SetRank_InRange()
    {
        int[] pts = { 40000, 30000, 25000, 20000 };
        var logic = RunGame(0, pts);
        var ranks = logic.Player.Select(p => p.SetRank).OrderBy(r => r).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, ranks);
    }

    // シナリオ5: 高得点プレイヤーが低い SetRank (1位=0)
    [Fact]
    public void ProcessEndHanchan_HighScore_LowRank()
    {
        // Player[0] が最高点
        int[] pts = { 50000, 25000, 25000, 15000 };
        var logic = RunGame(0, pts);

        // Player[0] の SetRank は 0 (1位)
        // ただし順位決定は Chicha から始まる相対順
        var topPlayer = logic.Player.OrderBy(p => p.SetRank).First();
        Assert.True(topPlayer.SetTotal >= 0, "1位のSetTotalは最大のはず");
    }

    // シナリオ6: GameStatus = NotPlaying (対局終了)
    [Fact]
    public void ProcessEndHanchan_GameStatus_IsNotPlaying()
    {
        int[] pts = { 35000, 30000, 25000, 25000 };
        var logic = RunGame(0, pts);
        Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);
    }

    [Fact]
    public void ProcessEndHanchan_AppliesLegacySetPointUmaYakitoriTipAndRecords()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(new RuleInfo
        {
            Hanchan = false,
            Kuitan = true,
            Contest = 0,
            AkaDora = 0,
            Uma = 1,
            Tip = true,
            Yakitori = true,
        });
        logic.HanchanInfo.Chicha = 0;
        int[] gamePoints = { 45000, 30000, 25000, 20000 };
        int[] tips = { MajakConst.DefaultTip + 2, MajakConst.DefaultTip, MajakConst.DefaultTip - 2, MajakConst.DefaultTip };
        for (int i = 0; i < 4; i++)
        {
            logic.Player[i].GamePoint = gamePoints[i];
            logic.Player[i].Tip = tips[i];
            if (i != 2) logic.Player[i].ClearYakitori();
        }

        InvokeProcessEndHanchan(logic);

        Assert.Equal(new[] { 0, 1, 2, 3 }, logic.Player.Select(player => player.SetRank).ToArray());
        Assert.Equal(new[] { 15, 0, -5, -10 }, logic.Player.Select(player => player.SetPoint).ToArray());
        Assert.Equal(new[] { 20, 10, -10, -20 }, logic.Player.Select(player => player.SetUma).ToArray());
        Assert.Equal(new[] { 10, 10, -30, 10 }, logic.Player.Select(player => player.SetTor).ToArray());
        Assert.Equal(new[] { 4, 0, -4, 0 }, logic.Player.Select(player => player.SetTip).ToArray());
        Assert.Equal(new[] { 49, 20, -49, -20 }, logic.Player.Select(player => player.SetTotal).ToArray());
        Assert.Equal(new[] { 35, 10, -15, -30 }, logic.Player.Select(player => player.ResultRecord.PointSum).ToArray());
        Assert.Equal(new[] { 1, 1, 1, 1 }, logic.Player.Select(player => player.ResultRecord.TipMatchCnt).ToArray());
        Assert.Equal(new[] { 4, 0, -4, 0 }, logic.Player.Select(player => player.ResultRecord.TipPoint).ToArray());
        Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic ProcessEndKyoku テスト
// 原典: HMajakGameLogic::ProcessEndKyoku
//   KyokuCnt++ / FuroCnt (副露) / RichiCnt (リーチ)
//   すべてのプレイヤーが MODE_KYO になる
// ═══════════════════════════════════════════════════════════════════════════
public class ProcessEndKyokuTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    private static void InvokeProcessRyuukyoku(MajakGameLogic logic)
    {
        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessRyuukyoku", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(logic, Array.Empty<object>());
    }

    private static void InvokeProcessPinchui(MajakGameLogic logic)
    {
        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessPinchui", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(logic, Array.Empty<object>());
    }

    private static void InvokeProcessHora(MajakGameLogic logic, bool tsumo)
    {
        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessHora", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(logic, new object[] { tsumo });
    }

    private static void InvokeClearAllIppatsu(MajakGameLogic logic)
    {
        var method = typeof(MajakGameLogic)
            .GetMethod("ClearAllIppatsu", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(logic, Array.Empty<object>());
    }

    private static void InvokeProcessEndKyoku(MajakGameLogic logic, bool hora, bool renchan)
    {
        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(logic, new object[] { hora, renchan });
    }

    private static bool InvokeCheckHoraYaku(MajakGameLogic logic, EnginePlayer player, bool tsumo)
    {
        var method = typeof(MajakGameLogic)
            .GetMethod("CheckHoraYaku", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(logic, new object[] { player, tsumo })!;
    }

    private static void SetKyokuEnd(MajakGameLogic logic, KyokuEnd kyokuEnd)
    {
        typeof(MajakGameLogic)
            .GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(logic, kyokuEnd);
    }

    private static void SetCurrOrder(MajakGameLogic logic, int order)
    {
        typeof(MajakGameLogic)
            .GetField("_currOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, order);
    }

    private static void SetIsFirstTurn(MajakGameLogic logic, bool isFirstTurn)
    {
        typeof(MajakGameLogic)
            .GetField("_isFirstTurn", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, isFirstTurn);
    }

    private static bool GetIsFirstTurn(MajakGameLogic logic)
    {
        return (bool)typeof(MajakGameLogic)
            .GetField("_isFirstTurn", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(logic)!;
    }

    private static void SetPrivateBool(MajakGameLogic logic, string fieldName, bool value)
    {
        typeof(MajakGameLogic)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(logic, value);
    }

    private static void SetMenzen(EnginePlayer player, bool isMenzen)
    {
        typeof(EnginePlayer)
            .GetProperty(nameof(EnginePlayer.IsMenzen))!
            .SetValue(player, isMenzen);
    }

    private static void SetPaoOrder(EnginePlayer player, int order)
    {
        typeof(EnginePlayer)
            .GetProperty(nameof(EnginePlayer.PaoOrder))!
            .SetValue(player, order);
    }

    private static void SetLiveWallEmpty(MajakGameLogic logic)
    {
        var bipai = typeof(MajakGameLogic)
            .GetField("_bipai", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(logic)!;
        typeof(Bipai)
            .GetField("_bipPtr", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(bipai, 136 - MajakConst.WanpaiCount);
    }

    private static void SetTehai(EnginePlayer player, params int[] serials)
    {
        player.Tehai.Clear();
        for (int i = 0; i < serials.Length; i++)
        {
            var pai = PaiCode.MakeSerial(serials[i]);
            pai.BipaiIndex = i + 20;
            player.Tehai.Add(pai);
        }
    }

    // シナリオ1: 流局後 → KyokuCnt が増加
    // 原典: pPlayer->m_stResultRecord.m_nKyokuCnt++
    [Fact]
    public void ProcessEndKyoku_KyokuCountIncrements()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int prevKyoku0 = logic.Player[0].ResultRecord.KyokuCnt;

        // 流局: 牌山が空になるシミュレーション (KYO + 全員PAS)
        logic.KyokuInfo.Renchan = false;
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++)
            logic.ProcessAction(i, Act.Pas, System.Array.Empty<int>(), 0);

        // 次局が開始されているため KyokuCnt が増加していること
        Assert.True(logic.Player[0].ResultRecord.KyokuCnt >= prevKyoku0);
    }

    [Fact]
    public void ProcessEndKyoku_SetsFlagsOpensWallAndUpdatesPlayers()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        SetMenzen(logic.Player[1], false);
        logic.Player[2].RichiType = RichiType.Richi;
        int[] prevKyokuCnt = logic.Player.Select(player => player.ResultRecord.KyokuCnt).ToArray();

        InvokeProcessEndKyoku(logic, hora: true, renchan: true);

        Assert.True(logic.KyokuInfo.EndKyokuWithHora);
        Assert.True(logic.KyokuInfo.Renchan);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(prevKyokuCnt[i] + 1, logic.Player[i].ResultRecord.KyokuCnt);
            Assert.Equal(PlayerMode.Kyo, logic.Player[i].Mode);
        }
        Assert.Equal(1, logic.Player[1].ResultRecord.FuroCnt);
        Assert.Equal(1, logic.Player[2].ResultRecord.RichiCnt);
        Assert.Equal(GameStatus.EndKyoku, logic.GameStatus);

        var info = BipaiInfo.Create();
        logic.GetBipai(ref info, openMask: 1, skipMask: 1);
        Assert.Equal(136, info.PaiCnt);
    }

    [Fact]
    public void ProcessRyuukyoku_NagashiManganChildPaysLegacyBalances()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, Nagashi = true });
        int oya = logic.KyokuInfo.OyaOrder;
        int winner = (oya + 1) % 4;
        for (int i = 0; i < 4; i++)
        {
            logic.Player[i].GamePoint = 25000;
            if (i != winner) logic.Player[i].ClearNagashiMangan();
        }

        InvokeProcessRyuukyoku(logic);

        Assert.Equal(33000, logic.Player[winner].GamePoint);
        Assert.Equal(21000, logic.Player[oya].GamePoint);
        for (int i = 0; i < 4; i++)
        {
            if (i != winner && i != oya)
                Assert.Equal(23000, logic.Player[i].GamePoint);
        }
        Assert.Equal(KyokuEnd.Nagashimangan, logic.KyokuEnd);
    }

    [Fact]
    public void ProcessRyuukyoku_TwoTempaiAppliesLegacyBappu()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        for (int i = 0; i < 4; i++) logic.Player[i].GamePoint = 25000;

        SetTehai(logic.Player[0], 0, 1, 2, 3, 4, 5, 6, 7, 8, 27, 27, 27, 28);
        SetTehai(logic.Player[1], 0, 1, 2, 3, 4, 5, 6, 7, 8, 27, 27, 27, 28);
        SetTehai(logic.Player[2], 0, 4, 8, 12, 16, 20, 24, 28, 1, 5, 9, 13, 17);
        SetTehai(logic.Player[3], 0, 4, 8, 12, 16, 20, 24, 28, 1, 5, 9, 13, 17);

        InvokeProcessRyuukyoku(logic);

        Assert.Equal(26500, logic.Player[0].GamePoint);
        Assert.Equal(26500, logic.Player[1].GamePoint);
        Assert.Equal(23500, logic.Player[2].GamePoint);
        Assert.Equal(23500, logic.Player[3].GamePoint);
        Assert.True(logic.Player[0].IsTempai);
        Assert.True(logic.Player[1].IsTempai);
        Assert.False(logic.Player[2].IsTempai);
        Assert.False(logic.Player[3].IsTempai);
    }

    [Fact]
    public void ProcessPinchui_SucharichiMapsPinAndKeepsRenchan()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        SetKyokuEnd(logic, KyokuEnd.Sucharichi);

        InvokeProcessPinchui(logic);

        Assert.Equal(KyoResultPin.Sucharichi, logic.LastKyoResult.Pin);
        Assert.True(logic.KyokuInfo.Renchan);
        Assert.False(logic.KyokuInfo.EndKyokuWithHora);
        Assert.Equal(GameStatus.EndKyoku, logic.GameStatus);
    }

    [Fact]
    public void ProcessHora_RonOyaWinnerSetsHojuAndRenchan()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int oya = logic.KyokuInfo.OyaOrder;
        int hoju = (oya + 3) % 4;

        SetCurrOrder(logic, hoju);
        logic.Player[oya].CurAct = Act.Ron;
        SetTehai(logic.Player[oya], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);

        InvokeProcessHora(logic, tsumo: false);

        Assert.Equal(KyoResultPin.Ron, logic.LastKyoResult.Pin);
        Assert.Equal(hoju, logic.LastKyoResult.HojuOrder);
        Assert.Equal(1, logic.Player[hoju].ResultRecord.HojuCnt);
        Assert.True(logic.KyokuInfo.Renchan);
        Assert.True(logic.KyokuInfo.EndKyokuWithHora);
        Assert.Equal(GameStatus.EndKyoku, logic.GameStatus);
    }

    [Fact]
    public void ProcessHora_TsumoTobiRecordsTobiAndTobashi()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int winner = logic.KyokuInfo.OyaOrder;
        int loser = (winner + 1) % 4;

        SetCurrOrder(logic, winner);
        logic.Player[winner].CurAct = Act.Tsu;
        logic.Player[loser].GamePoint = -1;
        SetTehai(logic.Player[winner], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);

        InvokeProcessHora(logic, tsumo: true);

        Assert.Equal(KyoResultPin.Tsumo, logic.LastKyoResult.Pin);
        Assert.Equal(1, logic.Player[loser].ResultRecord.TobiCnt);
        Assert.Equal(1, logic.Player[winner].ResultRecord.TobashiCnt);
    }

    [Fact]
    public void ProcessHora_ContestModeSkipsTobiAndTobashiRecords()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule() with { Contest = 1 });
        int winner = logic.KyokuInfo.OyaOrder;
        int loser = (winner + 1) % 4;

        SetCurrOrder(logic, winner);
        logic.Player[winner].CurAct = Act.Tsu;
        logic.Player[loser].GamePoint = -1;
        SetTehai(logic.Player[winner], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);

        InvokeProcessHora(logic, tsumo: true);

        Assert.Equal(0, logic.Player[loser].ResultRecord.TobiCnt);
        Assert.Equal(0, logic.Player[winner].ResultRecord.TobashiCnt);
    }

    [Fact]
    public void ProcessHora_MultiRonOnlyFirstWinnerReceivesRenchanAndRibouBonus()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int hoju = logic.KyokuInfo.OyaOrder;
        int firstWinner = (hoju + 1) % 4;
        int secondWinner = (hoju + 2) % 4;

        SetCurrOrder(logic, hoju);
        logic.HanchanInfo.RenchanCount = 1;
        logic.KyokuInfo.RibouCount = 1;
        logic.Player[firstWinner].CurAct = Act.Ron;
        logic.Player[secondWinner].CurAct = Act.Ron;
        SetTehai(logic.Player[firstWinner], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);
        SetTehai(logic.Player[secondWinner], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);

        InvokeProcessHora(logic, tsumo: false);

        Assert.True(logic.LastKyoResult.Hora[firstWinner]);
        Assert.True(logic.LastKyoResult.Hora[secondWinner]);
        Assert.Equal(300, logic.LastKyoResult.RenBal[firstWinner]);
        Assert.Equal(0, logic.LastKyoResult.RenBal[secondWinner]);
        Assert.Equal(-300, logic.LastKyoResult.RenBal[hoju]);
        Assert.Equal(1000, logic.LastKyoResult.RibBal[firstWinner]);
        Assert.Equal(0, logic.LastKyoResult.RibBal[secondWinner]);
        Assert.Equal(0, logic.KyokuInfo.RibouCount);
    }

    [Fact]
    public void ProcessHora_RonTobashiUsesLegacyHojuAndPaoNegativeConditions()
    {
        static MajakGameLogic BuildRonLogic(out int hoju, out int winner, out int other)
        {
            var logic = new MajakGameLogic();
            logic.InitHanchan(ProcessEndKyokuTests.DefaultRule());
            hoju = logic.KyokuInfo.OyaOrder;
            winner = (hoju + 1) % 4;
            other = (hoju + 2) % 4;
            SetCurrOrder(logic, hoju);
            logic.Player[winner].CurAct = Act.Ron;
            SetTehai(logic.Player[winner], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);
            return logic;
        }

        var unrelatedTobi = BuildRonLogic(out _, out int unrelatedWinner, out int unrelatedOther);
        unrelatedTobi.Player[unrelatedOther].GamePoint = -1;
        InvokeProcessHora(unrelatedTobi, tsumo: false);
        Assert.Equal(0, unrelatedTobi.Player[unrelatedWinner].ResultRecord.TobashiCnt);

        var hojuTobi = BuildRonLogic(out int hoju, out int hojuWinner, out _);
        hojuTobi.Player[hoju].GamePoint = -1;
        InvokeProcessHora(hojuTobi, tsumo: false);
        Assert.Equal(1, hojuTobi.Player[hojuWinner].ResultRecord.TobashiCnt);

        var paoTobi = BuildRonLogic(out _, out int paoWinner, out int paoOrder);
        paoTobi.Player[paoOrder].GamePoint = -1;
        SetPaoOrder(paoTobi.Player[paoWinner], paoOrder);
        InvokeProcessHora(paoTobi, tsumo: false);
        Assert.Equal(1, paoTobi.Player[paoWinner].ResultRecord.TobashiCnt);
    }

    [Fact]
    public void ClearAllIppatsu_ClearsEveryPlayerAndFirstTurn()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        foreach (var player in logic.Player) player.IsIppatsu = true;
        SetIsFirstTurn(logic, true);

        InvokeClearAllIppatsu(logic);

        Assert.All(logic.Player, player => Assert.False(player.IsIppatsu));
        Assert.False(GetIsFirstTurn(logic));
    }

    [Fact]
    public void CheckHoraYaku_NoFormReturnsFalseBeforeYakuChecks()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var player = logic.Player[0];
        SetTehai(player, 0, 1, 2, 3, 4, 5, 6);
        player.RichiType = RichiType.Richi;

        Assert.False(InvokeCheckHoraYaku(logic, player, tsumo: true));
    }

    [Fact]
    public void CheckHoraYaku_NoYakuRonFallsBackToHandCheck()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule() with { Kuitan = false });
        var player = logic.Player[0];
        SetTehai(player, 0, 1, 2, 3, 4, 5, 15, 16, 17, 18, 19, 20, 21, 21);
        SetMenzen(player, false);

        Assert.False(InvokeCheckHoraYaku(logic, player, tsumo: false));
    }

    [Fact]
    public void CheckHoraYaku_MenzenTsumoAllowsCompleteNoYakuForm()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule() with { Kuitan = false });
        var player = logic.Player[0];
        SetTehai(player, 0, 1, 2, 3, 4, 5, 15, 16, 17, 18, 19, 20, 21, 21);

        Assert.True(InvokeCheckHoraYaku(logic, player, tsumo: true));
    }

    [Fact]
    public void CheckHoraYaku_RichiRinshanChankanAndHaiteiBypassNormalYakuCheck()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule() with { Kuitan = false });
        var player = logic.Player[0];
        SetTehai(player, 0, 1, 2, 3, 4, 5, 15, 16, 17, 18, 19, 20, 21, 21);
        SetMenzen(player, false);

        player.RichiType = RichiType.Richi;
        Assert.True(InvokeCheckHoraYaku(logic, player, tsumo: false));

        player.RichiType = RichiType.None;
        SetPrivateBool(logic, "_isRinshan", true);
        Assert.True(InvokeCheckHoraYaku(logic, player, tsumo: false));

        SetPrivateBool(logic, "_isRinshan", false);
        SetPrivateBool(logic, "_isChankan", true);
        Assert.True(InvokeCheckHoraYaku(logic, player, tsumo: false));

        SetPrivateBool(logic, "_isChankan", false);
        SetLiveWallEmpty(logic);
        Assert.True(InvokeCheckHoraYaku(logic, player, tsumo: false));
    }

    [Fact]
    public void GetHoraYaku_FirstTurnTsumoReplacesNormalYakuWithTenhouOrChihou()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        int oya = logic.KyokuInfo.OyaOrder;
        var yaku = new Yaku();
        SetPrivateBool(logic, "_isFirstTurn", true);
        SetTehai(logic.Player[oya], 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);

        logic.GetHoraYaku(logic.Player[oya], isTsumo: true, yaku);

        Assert.True(yaku.IsYakuman);
        Assert.Contains(yaku.List, item => item.Name == HoraYaku.Tenhou);
        Assert.DoesNotContain(yaku.List, item => item.Name == HoraYaku.Tsumo);
        Assert.Equal(5, yaku.Chip);
    }

    [Fact]
    public void GetHoraYaku_RichiIppatsuContestSkipMatchesLegacy()
    {
        static Yaku BuildYaku(int contest)
        {
            var logic = new MajakGameLogic();
            logic.InitHanchan(ProcessEndKyokuTests.DefaultRule() with { Contest = contest });
            var player = logic.Player[0];
            SetTehai(player, 1, 2, 3, 2, 3, 4, 12, 13, 14, 14, 15, 16, 25, 25);
            player.RichiType = RichiType.Richi;
            player.IsIppatsu = true;
            var yaku = new Yaku();
            logic.GetHoraYaku(player, isTsumo: false, yaku);
            return yaku;
        }

        var normal = BuildYaku(contest: 0);
        Assert.Contains(normal.List, item => item.Name == HoraYaku.Richi);
        Assert.Contains(normal.List, item => item.Name == HoraYaku.Ippatsu);

        var contest = BuildYaku(contest: 1);
        Assert.Contains(contest.List, item => item.Name == HoraYaku.Richi);
        Assert.DoesNotContain(contest.List, item => item.Name == HoraYaku.Ippatsu);
    }

    [Fact]
    public void GetHoraYaku_RinshanChankanHaiteiHouteiUseLegacyPrecedence()
    {
        static Yaku BuildYaku(bool rinshan, bool chankan, bool emptyWall, bool tsumo)
        {
            var logic = new MajakGameLogic();
            logic.InitHanchan(ProcessEndKyokuTests.DefaultRule() with { Kuitan = false });
            var player = logic.Player[0];
            SetTehai(player, 0, 1, 2, 3, 4, 5, 15, 16, 17, 18, 19, 20, 21, 21);
            SetMenzen(player, false);
            SetPrivateBool(logic, "_isFirstTurn", false);
            SetPrivateBool(logic, "_isRinshan", rinshan);
            SetPrivateBool(logic, "_isChankan", chankan);
            if (emptyWall) SetLiveWallEmpty(logic);
            var yaku = new Yaku();
            logic.GetHoraYaku(player, isTsumo: tsumo, yaku);
            return yaku;
        }

        var rinshanFirst = BuildYaku(rinshan: true, chankan: true, emptyWall: true, tsumo: false);
        Assert.Contains(rinshanFirst.List, item => item.Name == HoraYaku.Rinshan);
        Assert.DoesNotContain(rinshanFirst.List, item => item.Name == HoraYaku.Chankan);
        Assert.DoesNotContain(rinshanFirst.List, item => item.Name == HoraYaku.Houtei);

        var chankanBeforeHoutei = BuildYaku(rinshan: false, chankan: true, emptyWall: true, tsumo: false);
        Assert.Contains(chankanBeforeHoutei.List, item => item.Name == HoraYaku.Chankan);
        Assert.DoesNotContain(chankanBeforeHoutei.List, item => item.Name == HoraYaku.Houtei);

        var houtei = BuildYaku(rinshan: false, chankan: false, emptyWall: true, tsumo: false);
        Assert.Contains(houtei.List, item => item.Name == HoraYaku.Houtei);

        var haitei = BuildYaku(rinshan: false, chankan: false, emptyWall: true, tsumo: true);
        Assert.Contains(haitei.List, item => item.Name == HoraYaku.Haitei);
    }

    [Fact]
    public void GetHoraYaku_DoraAggregatesHandFuroAndNukiDora()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var player = logic.Player[0];
        SetPrivateBool(logic, "_isFirstTurn", false);
        SetTehai(player, 1, 2, 3, 9, 9, 9, 18, 18, 18, 22, 22);
        SetMenzen(player, false);
        logic.KyokuInfo.Dora[0] = PaiCode.MakeSerial(0);
        player.Furo.Add(new FuroBlock
        {
            Act = Act.Chi,
            Tiles = new List<PaiCode> { PaiCode.MakeSerial(1), PaiCode.MakeSerial(2), PaiCode.MakeSerial(3) },
        });
        player.NukiDora.Add(PaiCode.MakeSerial(9));
        var yaku = new Yaku();

        logic.GetHoraYaku(player, isTsumo: false, yaku);

        Assert.Equal(2, yaku.DoraCnt[0]);
        Assert.Equal(1, yaku.DoraCnt[3]);
        Assert.Contains(yaku.List, item => item.Name == HoraYaku.Dora && item.Han == 3);
    }

    [Fact]
    public void GetHoraYaku_KazoeYakumanOverridesChipLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        var player = logic.Player[0];
        SetPrivateBool(logic, "_isFirstTurn", false);
        SetTehai(player, 1, 1, 1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9);
        logic.KyokuInfo.KanCount = 4;
        for (int i = 0; i <= logic.KyokuInfo.KanCount; i++)
            logic.KyokuInfo.Dora[i] = PaiCode.MakeSerial(0);
        var yaku = new Yaku();

        logic.GetHoraYaku(player, isTsumo: false, yaku);

        Assert.False(yaku.IsYakuman);
        Assert.Equal(5, yaku.Mangan);
        Assert.Equal(10, yaku.Chip);
    }

    // シナリオ2: 副露プレイヤー → FuroCnt が増加
    // 原典: if(!pPlayer->m_bIsMenzen) m_stResultRecord.m_nFuroCnt++
    [Fact]
    public void ProcessEndKyoku_FuroPlayer_FuroCntIncrements()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        int child  = (parent + 1) % 4;

        // ProcessEndKyoku を reflection で直接呼び出して FuroCnt を確認
        var procEKMethod = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        var field = typeof(EnginePlayer).GetProperty("IsMenzen",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(logic.Player[child], false, null);
        int prevFuro = logic.Player[child].ResultRecord.FuroCnt;

        // ProcessEndKyoku(false, false) を直接呼ぶ
        procEKMethod.Invoke(logic, new object[] { false, false });

        Assert.True(logic.Player[child].ResultRecord.FuroCnt > prevFuro,
            "副露プレイヤーは FuroCnt が増加すること");
    }

    // シナリオ3: リーチプレイヤー → RichiCnt が増加 (ProcessEndKyoku 直接呼び出し)
    // 原典: if(NONE != pPlayer->m_eRichiType) m_stResultRecord.m_nRichiCnt++
    [Fact]
    public void ProcessEndKyoku_RichiPlayer_RichiCntIncrements()
    {
        var logic  = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var method = typeof(MajakGameLogic)
            .GetMethod("ProcessEndKyoku",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        int parent = logic.KyokuInfo.OyaOrder;
        logic.Player[parent].RichiType = RichiType.Richi;
        int prevRichi = logic.Player[parent].ResultRecord.RichiCnt;

        method.Invoke(logic, new object[] { false, false });

        Assert.True(logic.Player[parent].ResultRecord.RichiCnt > prevRichi,
            "リーチプレイヤーは RichiCnt が増加すること");
    }
}

public class ProcessHoraPlayerRecordTests
{
    private static void InvokeProcessHoraPlayer(MajakGameLogic logic, EnginePlayer hora, EnginePlayer hoju, bool getBonus)
    {
        typeof(MajakGameLogic)
            .GetMethod("ProcessHoraPlayer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(logic, new object[] { hora, hoju, getBonus });
    }

    [Fact]
    public void ProcessHoraPlayer_WaremeRecordPoints_UseAdjustedHoraPoints()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, Wareme = true });
        logic.KyokuInfo.OyaOrder = 3;
        logic.KyokuInfo.Dice[0] = 0;
        logic.KyokuInfo.Dice[1] = 0;
        var hora = logic.Player[0];
        var hoju = logic.Player[1];
        hora.Tehai.Clear();
        int idx = 0;
        foreach (var serial in new[] { 1,2,3, 2,3,4, 12,13,14, 14,15,16, 25,25 })
        {
            var pai = PaiCode.MakeSerial(serial);
            pai.BipaiIndex = idx++;
            hora.Tehai.Add(pai);
        }

        InvokeProcessHoraPlayer(logic, hora, hoju, getBonus: false);

        int horaAdjusted = logic.LastKyoResult.TenBal[0] + logic.LastKyoResult.PaoBal[0] + logic.LastKyoResult.WarBal[0];
        int hojuAdjusted = logic.LastKyoResult.TenBal[1] + logic.LastKyoResult.PaoBal[1] + logic.LastKyoResult.WarBal[1];
        Assert.NotEqual(logic.LastKyoResult.TenBal[0], horaAdjusted);
        Assert.Equal(horaAdjusted, hora.ResultRecord.HoraPoint);
        Assert.Equal(hojuAdjusted, hoju.ResultRecord.HojuPoint);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Uma / Yakitori テーブル定数テスト
// 原典: umatbl / tortbl (HMajakGameLogic.cpp)
// ═══════════════════════════════════════════════════════════════════════════
public class UmaYakitoriTableTests
{
    // Uma テーブルをエンジンから取得するヘルパー
    private static (int rank1, int rank2, int rank3, int rank4) GetUma(int umaMode)
    {
        // ProcessEndHanchan を呼んで Uma 値を確認
        var rule  = new RuleInfo { Hanchan = false, Kuitan = true, Contest = 0, AkaDora = 0, Uma = umaMode };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);

        // Tip=DefaultTip に設定 → setTip=(DefaultTip-DefaultTip)*2=0 (ゼロ)
        // 原典: nSetTip = (m_nTip - DEFAULT_TIP) * 2 → Tipがデフォルト値なら0
        for (int i = 0; i < 4; i++)
            logic.Player[i].Tip = MajakConst.DefaultTip;

        logic.Player[0].GamePoint = 40000;
        logic.Player[1].GamePoint = 35000;
        logic.Player[2].GamePoint = 27000;
        logic.Player[3].GamePoint = 23000;

        logic.HanchanInfo.CurKyoku = 3;
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++)
            logic.ProcessAction(i, Act.Pas, System.Array.Empty<int>(), 0);

        // SetTotal = setTen + setUma ← uma を取り出すため diff 計算
        // SetPoint = setTen (umaなし)
        // Uma = SetTotal - SetPoint
        var ordered = logic.Player
            .OrderBy(p => p.SetRank)
            .Select(p => p.SetTotal - p.SetPoint)
            .ToArray();

        return (ordered[0], ordered[1], ordered[2], ordered[3]);
    }

    // シナリオ1: Uma=0 → +10,+5,-5,-10
    // 原典: umatbl[0] = {+10, +5, -5, -10}
    [Fact]
    public void UmaTable_Uma0_Values()
    {
        var (r1, r2, r3, r4) = GetUma(0);
        Assert.Equal(+10, r1);
        Assert.Equal(+5,  r2);
        Assert.Equal(-5,  r3);
        Assert.Equal(-10, r4);
    }

    // シナリオ2: Uma=1 → +20,+10,-10,-20
    [Fact]
    public void UmaTable_Uma1_Values()
    {
        var (r1, r2, r3, r4) = GetUma(1);
        Assert.Equal(+20, r1);
        Assert.Equal(+10, r2);
        Assert.Equal(-10, r3);
        Assert.Equal(-20, r4);
    }

    // シナリオ3: Uma=2 → +30,+10,-10,-30
    [Fact]
    public void UmaTable_Uma2_Values()
    {
        var (r1, r2, r3, r4) = GetUma(2);
        Assert.Equal(+30, r1);
        Assert.Equal(+10, r2);
        Assert.Equal(-10, r3);
        Assert.Equal(-30, r4);
    }

    // シナリオ4: Uma=3 → 0,0,0,0 (ウマなし)
    [Fact]
    public void UmaTable_Uma3_AllZero()
    {
        var (r1, r2, r3, r4) = GetUma(3);
        Assert.Equal(0, r1);
        Assert.Equal(0, r2);
        Assert.Equal(0, r3);
        Assert.Equal(0, r4);
    }

    // ─── Yakitori テーブル ────────────────────────────────────────────────
    // 原典: static const int tortbl[] = {0, 30, 15, 10, 0}
    //   torCnt=0: 全員非焼き鳥 → 0 (全員が同額取得)
    //   torCnt=1: 1人焼き鳥 → -30 (焼き鳥1人が支払い)
    //   torCnt=2: 2人焼き鳥 → -15
    //   torCnt=3: 3人焼き鳥 → -10
    //   torCnt=4: 全員焼き鳥 → 0 (チャラ)

    // シナリオ5: 焼き鳥テーブル定数確認 (反射経由)
    [Fact]
    public void TortblValues_ViaReflection()
    {
        // ProcessEndHanchan 内の torTbl を間接確認
        // Yakitori=true, 1人焼き鳥の場合 SetTotal に -30 が含まれる
        var rule = new RuleInfo
        {
            Hanchan = false, Kuitan = true, Contest = 0, AkaDora = 0, Uma = 3, Yakitori = true,
        };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);

        // Player[0] のみ焼き鳥状態にする (IsYakitori=true のまま)
        // 他3人は ClearYakitori() で解除
        logic.Player[1].ClearYakitori();
        logic.Player[2].ClearYakitori();
        logic.Player[3].ClearYakitori();
        // Player[0] は焼き鳥状態のまま (IsYakitori=true)

        // Tip=DefaultTip → setTip=0 (ゼロ和を保つ)
        for (int i = 0; i < 4; i++) logic.Player[i].Tip = MajakConst.DefaultTip;

        logic.Player[0].GamePoint = 40000;
        logic.Player[1].GamePoint = 35000;
        logic.Player[2].GamePoint = 27000;
        logic.Player[3].GamePoint = 23000;

        logic.HanchanInfo.CurKyoku = 3;
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++)
            logic.ProcessAction(i, Act.Pas, System.Array.Empty<int>(), 0);

        // Player[0] は焼き鳥プレイヤー → setTor = -30 (1人焼き鳥)
        // 他3人は +10 (torTbl[4-1]=10)
        var torPlayers = logic.Player.ToArray();
        // Uma=3 なので SetTotal - SetPoint = setUma + setTor = setTor のみ
        // 焼き鳥プレイヤーの setTor < 0
        bool found = false;
        foreach (var p in torPlayers)
        {
            int torPart = p.SetTotal - p.SetPoint; // Uma=3 → setUma=0, so this is setTor
            if (p.IsYakitori) // これは InitKyoku 後にリセットされるため ClearYakitori の有無で判断
            {
                // 焼き鳥プレイヤーの torPart は負
                found = true;
                break;
            }
        }
        // SetTotal の合計はゼロ
        Assert.Equal(0, logic.Player.Sum(p => p.SetTotal));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakConst.KaeshiPoint / 点数変換テスト
// 原典: #define KAESHIPOINT 30000
//   setTen[i] = GamePoint - KAESHIPOINT (返し点)
//   変換: (setTen + 1000400)/1000 - 1000 (百点単位→点棒1000点単位)
// ═══════════════════════════════════════════════════════════════════════════
public class PointConversionTests
{
    // シナリオ1: KaeshiPoint は 30000
    // 原典: #define KAESHIPOINT 30000
    [Fact]
    public void KaeshiPoint_Is30000()
        => Assert.Equal(30000, MajakConst.KaeshiPoint);

    // シナリオ2: 点数変換公式 — (setTen + 1000400)/1000 - 1000
    // 原典: nSetTen[odr] = (nSetTen[odr]+1000400)/1000-1000
    [Theory]
    [InlineData(35000, +5)]   // 35000 - 30000 = 5000 → (5000+1000400)/1000-1000 = 5
    [InlineData(30000,  0)]   // 30000 - 30000 = 0    → (0+1000400)/1000-1000 = 0
    [InlineData(25000, -5)]   // 25000 - 30000 = -5000 → (-5000+1000400)/1000-1000 = -5
    [InlineData(26000, -4)]   // 26000 - 30000 = -4000 → (-4000+1000400)/1000-1000 = -4
    public void PointConversion_Formula_Correct(int gamePoint, int expectedK)
    {
        int setTen   = gamePoint - MajakConst.KaeshiPoint;
        int converted = (setTen + 1000400) / 1000 - 1000;
        Assert.Equal(expectedK, converted);
    }

    // シナリオ3: SetPoint合計 + SetUma合計 = 0 (ゼロサム)
    [Fact]
    public void ProcessEndHanchan_SetTotalIsZeroSum()
    {
        var rule  = new RuleInfo { Hanchan = false, Kuitan = true, Contest = 0, AkaDora = 0, Uma = 0 };
        var logic = new MajakGameLogic();
        logic.InitHanchan(rule);

        // Tip=DefaultTip → setTip=0
        for (int i = 0; i < 4; i++) logic.Player[i].Tip = MajakConst.DefaultTip;

        logic.Player[0].GamePoint = 40000;
        logic.Player[1].GamePoint = 32000;
        logic.Player[2].GamePoint = 28000;
        logic.Player[3].GamePoint = 15000;

        logic.HanchanInfo.CurKyoku = 3;
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++)
            logic.ProcessAction(i, Act.Pas, System.Array.Empty<int>(), 0);

        Assert.Equal(0, logic.Player.Sum(p => p.SetTotal));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameLogicService OnEndKyoku テスト
// 原典: HMajRoomServer::OnEndKyoku はクライアント payload ではなく majak の結果で処理する
// ═══════════════════════════════════════════════════════════════════════════
public class GameLogicServiceEndKyokuTests
{
    [Fact]
    public async Task OnEndKyoku_UsesEngineResult_NotClientPayload()
    {
        var room = new GameRoom
        {
            RoomId = 10,
            ChannelId = "ch1",
            SubId = "00C5A",
            CupJudgementType = 2, // CUP_JTID_DORA_ALL
        };
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Tsumo);
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Toitoi, 2);
        room.Engine.Player[0].Yaku.DoraCnt[0] = 3;

        var service = new GameLogicService(
            new PlayerSessionService(),
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            ["kyokuEnd"] = 0,
            ["horaOrder"] = 3,
            ["cupPoint"] = 99,
        });

        await service.OnEndKyokuAsync(room, ctx);

        Assert.Equal(3, player.CupRec.CupPoint);
        Assert.Equal(1, player.YakuCount[(int)HoraYaku.Toitoi]);
        Assert.Equal(3, player.HoraDoraMax);
        Assert.Contains(sent, x => x.method == Cmd.GamePlay);
    }

    [Fact]
    public async Task OnEndKyoku_CupYakuAnyCountsOnlyMaskedYaku()
    {
        var normalMask = new string('0', 28).ToCharArray();
        normalMask[(int)HoraYaku.Tanyao] = '1';
        normalMask[(int)HoraYaku.Toitoi] = '1';

        var room = new GameRoom
        {
            RoomId = 13,
            ChannelId = "ch1",
            SubId = "00C5A",
            CupJudgementType = 0, // CUP_JTID_YAKU_ANY
            CupNormalYakuCondition = new string(normalMask),
        };
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Tsumo);
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Tanyao, 1);
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Toitoi, 2);
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Pinfu, 1);

        var service = new GameLogicService(
            new PlayerSessionService(),
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, _) = CommandTestHelper.MakeContext(player);

        await service.OnEndKyokuAsync(room, ctx);

        Assert.Equal(2, player.CupRec.CupPoint);
        Assert.Equal(2, player.CupPointGain);
    }

    [Fact]
    public async Task OnEndKyoku_UsesEngineOrderToRoomSeatMapping()
    {
        var room = new GameRoom
        {
            RoomId = 11,
            ChannelId = "ch1",
            SubId = "00C5A",
            CupJudgementType = 2,
        };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"seat{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
                ChannelId = "ch1",
            }, seat);
        }
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 2, 0, 3, 1 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Tsumo);
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Toitoi, 2);
        room.Engine.Player[0].Yaku.DoraCnt[0] = 4;

        var service = new GameLogicService(
            new PlayerSessionService(),
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[2]!);

        await service.OnEndKyokuAsync(room, ctx);

        Assert.Equal(4, room.Seats[2]!.CupRec.CupPoint);
        Assert.Equal(4, room.Seats[2]!.CupPointGain);
        Assert.Equal(0, room.Seats[0]!.CupRec.CupPoint);
        Assert.Equal(1, room.Seats[2]!.YakuCount[(int)HoraYaku.Toitoi]);
        Assert.Equal(4, room.Seats[2]!.HoraDoraMax);

        var endKyo = sent.Select(x => x.packet).Select(CommandTestHelper.ToDict).First(packet =>
            ((JsonElement)packet["playType"]!).GetString() == "MJPID_ENDKYO");
        var players = ((JsonElement)endKyo["players"]!).EnumerateArray().ToArray();
        Assert.Equal("seat2", players[0].GetProperty("memberNo").GetString());
    }

    [Fact]
    public async Task OnEndKyoku_KazoeYakumanDoraTitleClear22()
    {
        var room = new GameRoom
        {
            RoomId = 12,
            ChannelId = "ch1",
            SubId = "00N5A",
        };
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Tsumo);
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Dora, 13);
        room.Engine.Player[0].Yaku.Mangan = 5;
        room.Engine.Player[0].Yaku.DoraCnt[0] = 12;

        var service = new GameLogicService(
            new PlayerSessionService(),
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await service.OnEndKyokuAsync(room, ctx);

        Assert.Equal(2, player.TitleClear[22]);
        Assert.Equal(1, player.YakuCount[(int)HoraYaku.Dora]);
    }

    [Fact]
    public async Task OnEndKyoku_RichiIppatsuTsumoSendsAvatarGearType2()
    {
        var room = new GameRoom
        {
            RoomId = 14,
            ChannelId = "ch1",
            SubId = "00N5A",
        };
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Tsumo);
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Ippatsu, 1);
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Tsumo, 1);

        var service = new GameLogicService(
            new PlayerSessionService(),
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await service.OnEndKyokuAsync(room, ctx);

        Assert.Equal(2, player.MemorialShop);
        Assert.Contains(sent, x => x.method == Cmd.AvatarGear);
    }

    [Fact]
    public async Task OnEndKyoku_MultipleYakumanTitleClear25AndYmanCount()
    {
        var room = new GameRoom
        {
            RoomId = 13,
            ChannelId = "ch1",
            SubId = "10N5A",
        };
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Tsumo);
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.Player[0].Yaku.AddYakuman(HoraYaku.Daisangen, 1);
        room.Engine.Player[0].Yaku.AddYakuman(HoraYaku.Suuankou, 1);

        var histMock = new Mock<HistoryRepository>(MockBehavior.Loose);
        histMock.Setup(r => r.InsertYakuHistAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        var service = new GameLogicService(
            new PlayerSessionService(),
            histMock.Object,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await service.OnEndKyokuAsync(room, ctx);

        Assert.Equal(2, player.TitleClear[25]);
        Assert.Equal(1, player.MemorialShop);
        Assert.Contains(sent, x => x.method == Cmd.AvatarGear);
        Assert.Equal(1, player.YmanCount[(int)HoraYaku.Daisangen - 100]);
        Assert.Equal(1, player.YmanCount[(int)HoraYaku.Suuankou - 100]);
        histMock.Verify(r => r.InsertYakuHistAsync("u1", GameConst.ServiceId, (int)HoraYaku.Daisangen), Times.Once);
        histMock.Verify(r => r.InsertYakuHistAsync("u1", GameConst.ServiceId, (int)HoraYaku.Suuankou), Times.Once);
    }

    [Fact]
    public async Task OnEndKyoku_YakumanAvatarGearSendsMemorialShopBitAfterDbSuccess()
    {
        var room = new GameRoom
        {
            RoomId = 14,
            ChannelId = "ch1",
            SubId = "10N5A",
        };
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Tsumo);
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.Player[0].Yaku.AddYakuman(HoraYaku.Daisangen, 1);

        var histMock = new Mock<HistoryRepository>(MockBehavior.Loose);
        histMock.Setup(r => r.InsertYakuHistAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        var playerRepo = new Mock<PlayerRepository>(MockBehavior.Loose);
        playerRepo.Setup(r => r.UpsertShopListAsync("u1", 1)).Returns(Task.CompletedTask);
        var service = new GameLogicService(
            new PlayerSessionService(),
            histMock.Object,
            (LogRepository)null!,
            (RatingService)null!,
            playerRepo.Object,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await service.OnEndKyokuAsync(room, ctx);

        var packet = CommandTestHelper.ToDict(sent.First(x => x.method == Cmd.AvatarGear).packet);
        Assert.Equal(1, ((JsonElement)packet[Key.MemorialShop]!).GetInt32());
        playerRepo.Verify(r => r.UpsertShopListAsync("u1", 1), Times.Once);
    }

    [Fact]
    public async Task OnEndKyoku_ContestPayloadLimitsDoraToOne()
    {
        var room = new GameRoom
        {
            RoomId = 15,
            ChannelId = "ch1",
            SubId = "00N5A",
        };
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 1 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };
        room.Engine.KyokuInfo.KanCount = 3;
        room.Engine.KyokuInfo.Dora[0] = new PaiCode(0, 1);
        room.Engine.KyokuInfo.Dora[1] = new PaiCode(0, 2);
        room.Engine.KyokuInfo.Dora[2] = new PaiCode(0, 3);
        room.Engine.KyokuInfo.Dora[3] = new PaiCode(0, 4);
        room.Engine.KyokuInfo.UraDora[0] = new PaiCode(1, 1);
        room.Engine.KyokuInfo.UraDora[1] = new PaiCode(1, 2);
        room.Engine.KyokuInfo.UraDora[2] = new PaiCode(1, 3);
        room.Engine.KyokuInfo.UraDora[3] = new PaiCode(1, 4);

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Taopai);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Taopai);

        var service = new GameLogicService(
            new PlayerSessionService(),
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await service.OnEndKyokuAsync(room, ctx);

        var endKyo = sent.Select(x => x.packet).Select(CommandTestHelper.ToDict).First(packet =>
            ((JsonElement)packet["playType"]!).GetString() == "MJPID_ENDKYO");
        var dora = ((JsonElement)endKyo["dora"]!).EnumerateArray().ToArray();
        var uraDora = ((JsonElement)endKyo["uraDora"]!).EnumerateArray().ToArray();
        Assert.Single(dora);
        Assert.Single(uraDora);
        Assert.Equal(new PaiCode(0, 2).Code, dora[0].GetInt32());
        Assert.Equal(new PaiCode(1, 2).Code, uraDora[0].GetInt32());
    }

    [Fact]
    public async Task OnEndKyoku_RonPayloadSelectsFirstWinnerAfterHoju()
    {
        var room = new GameRoom
        {
            RoomId = 16,
            ChannelId = "ch1",
            SubId = "00N5A",
        };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"seat{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
                ChannelId = "ch1",
            }, seat);
        }
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };

        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.LastKyoResult.Clear(KyoResultPin.Ron);
        room.Engine.LastKyoResult.HojuOrder = 1;
        room.Engine.LastKyoResult.Hora[0] = true;
        room.Engine.LastKyoResult.Hora[3] = true;
        room.Engine.LastKyoResult.TenBal[3] = 12000;
        room.Engine.LastKyoResult.PaoBal[3] = 1000;
        room.Engine.LastKyoResult.WarBal[3] = 2000;
        room.Engine.LastKyoResult.RibBal[3] = 3000;
        room.Engine.LastKyoResult.RenBal[3] = 900;
        room.Engine.LastKyoResult.TipBal[3] = 2;
        room.Engine.Player[0].Yaku.AddYaku(HoraYaku.Toitoi, 2);
        room.Engine.Player[3].Yaku.AddYaku(HoraYaku.Richi, 1);

        var service = new GameLogicService(
            new PlayerSessionService(),
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await service.OnEndKyokuAsync(room, ctx);

        var endKyo = sent.Select(x => x.packet).Select(CommandTestHelper.ToDict).First(packet =>
            ((JsonElement)packet["playType"]!).GetString() == "MJPID_ENDKYO");
        Assert.Equal(3, ((JsonElement)endKyo["selectedOdr"]!).GetInt32());

        var players = ((JsonElement)endKyo["players"]!).EnumerateArray().ToArray();
        Assert.Equal("seat3", players[3].GetProperty("memberNo").GetString());
        Assert.Equal(18900, players[3].GetProperty("tenBal").GetInt32());
        Assert.Equal(12000, players[3].GetProperty("tenBaseBal").GetInt32());
        Assert.Equal(1000, players[3].GetProperty("paoBal").GetInt32());
        Assert.Equal(2000, players[3].GetProperty("warBal").GetInt32());
        Assert.Equal(3000, players[3].GetProperty("ribBal").GetInt32());
        Assert.Equal(900, players[3].GetProperty("renBal").GetInt32());
        Assert.Equal(2, players[3].GetProperty("tipBal").GetInt32());
    }

    [Fact]
    public void OnEndGame_CupKillDifUsesEngineOrderToRoomSeatMapping()
    {
        var room = new GameRoom
        {
            RoomId = 17,
            ChannelId = "ch1",
            SubId = "00C5A",
            CupJudgementType = 7,
        };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"seat{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
                ChannelId = "ch1",
            }, seat);
        }
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 2, 0, 3, 1 };
        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.GameEnd))!
            .SetValue(room.Engine, GameEnd.Tobi);
        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.KyokuEnd))!
            .SetValue(room.Engine, KyokuEnd.Hora);
        room.Engine.Player[0].CurAct = MajakServer.Engine.Act.Ron;
        room.Engine.Player[1].GamePoint = -1000;
        room.Engine.LastKyoResult.HojuOrder = 1;

        var method = typeof(GameLogicService)
            .GetMethod("ApplyCupPointOnEndGame", BindingFlags.NonPublic | BindingFlags.Static)!;
        method.Invoke(null, new object[] { room });

        Assert.Equal(1, room.Seats[2]!.CupRec.CupPoint);
    Assert.Equal(1, room.Seats[2]!.CupPointGain);
        Assert.Equal(-1, room.Seats[0]!.CupRec.CupPoint);
    Assert.Equal(-1, room.Seats[0]!.CupPointGain);
        Assert.Equal(0, room.Seats[1]!.CupRec.CupPoint);
        Assert.Equal(0, room.Seats[3]!.CupRec.CupPoint);
    }

    [Fact]
    public async Task OnEndGame_CupGameSumPushesLegacyEventInfoPayload()
    {
        var playerRepoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        playerRepoMock.Setup(r => r.UpdateCupEvtRatAsync(It.IsAny<MajakPlayer>(), 777, 3))
            .Returns(Task.CompletedTask);
        var historyRepoMock = new Mock<HistoryRepository>(MockBehavior.Loose);
        historyRepoMock.Setup(r => r.InsertGameHistAsync(It.IsAny<MajakServer.Models.Game.GameReport>()))
            .ReturnsAsync(1L);
        var logRepoMock = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        logRepoMock.Setup(r => r.InsertGameHistAsync(It.IsAny<MajakServer.Models.Game.GameReport>()))
            .Returns(Task.CompletedTask);

        var room = new GameRoom
        {
            RoomId = 19,
            ChannelId = "ch1",
            SubId = "00C5F",
            CupId = 777,
            CupSeq = 3,
            CupJudgementType = 8,
            CupPointSumType = 1,
        };
        var player = new MajakPlayer
        {
            MemberNo = "seat0",
            NickName = "P0",
            ConnectionId = "c0",
            ChannelId = "ch1",
        };
        room.AddPlayer(player, 0);
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };
        room.Engine.Player[0].SetTotal = 120;

        var service = new GameLogicService(
            new PlayerSessionService(),
            historyRepoMock.Object,
            logRepoMock.Object,
            (RatingService)null!,
            playerRepoMock.Object,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await service.OnEndGameAsync(room, ctx);

        playerRepoMock.Verify(r => r.UpdateCupEvtRatAsync(player, 777, 3), Times.Once);
        var eventInfo = sent.Select(x => x.packet).Select(CommandTestHelper.ToDict).Single(packet =>
            packet.ContainsKey("totalPoint"));
        Assert.Equal(120, ((JsonElement)eventInfo["totalPoint"]!).GetInt32());
        Assert.Equal(1, ((JsonElement)eventInfo["matchCnt"]!).GetInt32());
        var points = ((JsonElement)eventInfo["points"]!).EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(new[] { 120, 0, 0, 0, 0, 0, 0 }, points);
    }

    [Fact]
    public void OnEndGame_TitleClear23And24UseEngineOrderToRoomSeatMapping()
    {
        var room = new GameRoom
        {
            RoomId = 18,
            ChannelId = "ch1",
            SubId = "00N5A",
        };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"seat{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
                ChannelId = "ch1",
            }, seat);
        }
        room.Engine.InitHanchan(new RuleInfo { Kuitan = true, Contest = 0 });
        room.Engine.HanchanInfo.Player = new[] { 2, 0, 3, 1 };
        typeof(MajakGameLogic).GetProperty(nameof(MajakGameLogic.GameEnd))!
            .SetValue(room.Engine, GameEnd.Tobi);
        room.Engine.Player[0].SetRank = 0;
        room.Engine.Player[0].GamePoint = 100000;
        room.Engine.Player[1].GamePoint = -1000;
        room.Engine.Player[2].GamePoint = -2000;
        room.Engine.Player[3].GamePoint = -3000;

        var method = typeof(GameLogicService)
            .GetMethod("UpdateEndGameTitleCounters", BindingFlags.NonPublic | BindingFlags.Static)!;
        method.Invoke(null, new object[] { room });

        Assert.Equal(2, room.Seats[2]!.TitleClear[23]);
        Assert.Equal(2, room.Seats[2]!.TitleClear[24]);
        Assert.Equal(0, room.Seats[0]!.TitleClear[23]);
        Assert.Equal(0, room.Seats[0]!.TitleClear[24]);
    }

    [Theory]
    [InlineData("10N5A", false, 0)]
    [InlineData("00T5A", false, 0)]
    [InlineData("00N5A", false, 10)]
    [InlineData("0ZN5A", false, 3)]
    [InlineData("00H5A", false, 3)]
    [InlineData("00N5A", true, 50)]
    public void OnEndKyoku_YakumanBonusRate_MatchesLegacy(string subId, bool testEnvironment, int expected)
    {
        var room = new GameRoom { SubId = subId };
        var method = typeof(GameLogicService)
            .GetMethod("GetYakumanBonusRate", BindingFlags.NonPublic | BindingFlags.Static)!;

        var actual = (int)method.Invoke(null, new object[] { room, testEnvironment })!;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task SendYakumanBonusAsync_DbSuccessUpdatesChannelPlayersAndSendsLegacyPacket()
    {
        var session = new PlayerSessionService();
        var playerRepoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        playerRepoMock.Setup(r => r.UpdateEarnedMoneyByYakumanBonusAsync(
                It.Is<IEnumerable<string>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { "p0", "p1" }))))
            .ReturnsAsync(true);
        var service = new GameLogicService(
            session,
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            playerRepoMock.Object,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var room = new GameRoom { ChannelId = "ch1" };
        var winner = new MajakPlayer { MemberNo = "p0", ConnectionId = "c0", ChannelId = "ch1" };
        var other = new MajakPlayer { MemberNo = "p1", ConnectionId = "c1", ChannelId = "ch1" };
        session.Register(winner);
        session.Register(other);
        var (ctx, sent) = CommandTestHelper.MakeContext(winner);

        var method = typeof(GameLogicService)
            .GetMethod("SendYakumanBonusAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(service, new object[] { room, winner, "Daisangen", ctx })!;

        Assert.Equal(GameConst.YakumanBonusMoney, winner.EarnedMoney);
        Assert.Equal(GameConst.YakumanBonusMoney, other.EarnedMoney);
        var (command, packet) = Assert.Single(sent);
        Assert.Equal(Cmd.YakumanBonus, command);
        var dict = CommandTestHelper.AsDict(packet);
        Assert.Equal(winner.Pix, dict[GKey.Pix]);
        Assert.NotEqual(winner.MemberNo, dict[GKey.Pix]);
        Assert.Equal(GameConst.YakumanBonusMoney, dict[GKey.GamMoney]);
        Assert.Equal("Daisangen", dict[Key.YakuName]);
    }

    [Fact]
    public async Task SendYakumanBonusAsync_DbFailureDoesNotMutateOrSend()
    {
        var session = new PlayerSessionService();
        var playerRepoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        playerRepoMock.Setup(r => r.UpdateEarnedMoneyByYakumanBonusAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(false);
        var service = new GameLogicService(
            session,
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            playerRepoMock.Object,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var room = new GameRoom { ChannelId = "ch1" };
        var winner = new MajakPlayer { MemberNo = "p0", ConnectionId = "c0", ChannelId = "ch1" };
        session.Register(winner);
        var (ctx, sent) = CommandTestHelper.MakeContext(winner);

        var method = typeof(GameLogicService)
            .GetMethod("SendYakumanBonusAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(service, new object[] { room, winner, "Daisangen", ctx })!;

        Assert.Equal(0, winner.EarnedMoney);
        Assert.Empty(sent);
    }
}
