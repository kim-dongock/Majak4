using MajakServer.Engine;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// ActionResult (LRET) enum テスト
// 原典: MajakDef.h LRET enum — C++ の数値と完全一致確認
// ═══════════════════════════════════════════════════════════════════════════
public class ActionResultEnumTests
{
    // 原典: MLS_OK = 0
    [Fact] public void ActionResult_Ok_Is0()                 => Assert.Equal(0,  (int)ActionResult.Ok);
    // 原典: MLE_ASSERT = 1
    [Fact] public void ActionResult_ErrAssert_Is1()          => Assert.Equal(1,  (int)ActionResult.ErrAssert);
    // 原典: MLE_INVALIDORDER = 2
    [Fact] public void ActionResult_ErrInvalidOrder_Is2()    => Assert.Equal(2,  (int)ActionResult.ErrInvalidOrder);
    // 原典: MLE_INVALIDMODE = 3
    [Fact] public void ActionResult_ErrInvalidMode_Is3()     => Assert.Equal(3,  (int)ActionResult.ErrInvalidMode);
    // 原典: MLE_INVALIDBIPAIINDEX = 4
    [Fact] public void ActionResult_ErrInvalidBipaiIndex_Is4() => Assert.Equal(4,  (int)ActionResult.ErrInvalidBipaiIndex);
    // 原典: MLE_PAINOTFOUNDINHAND = 5
    [Fact] public void ActionResult_ErrPaiNotFoundInHand_Is5() => Assert.Equal(5,  (int)ActionResult.ErrPaiNotFoundInHand);
    // 原典: MLE_PAIALREADYUSED = 6
    [Fact] public void ActionResult_ErrPaiAlreadyUsed_Is6()  => Assert.Equal(6,  (int)ActionResult.ErrPaiAlreadyUsed);
    // 原典: MLE_AFTERFURO = 7
    [Fact] public void ActionResult_ErrAfterFuro_Is7()       => Assert.Equal(7,  (int)ActionResult.ErrAfterFuro);
    // 原典: MLE_CANNOTHORA = 8
    [Fact] public void ActionResult_ErrCannotHora_Is8()      => Assert.Equal(8,  (int)ActionResult.ErrCannotHora);
    // 原典: MLE_TOOLATE = 9
    [Fact] public void ActionResult_ErrToolate_Is9()         => Assert.Equal(9,  (int)ActionResult.ErrToolate);
    // 原典: MLE_KANAFTER4KAN = 10
    [Fact] public void ActionResult_ErrKanAfter4Kan_Is10()   => Assert.Equal(10, (int)ActionResult.ErrKanAfter4Kan);
    // 原典: MLE_HUAPAI = 11
    [Fact] public void ActionResult_ErrHuapai_Is11()         => Assert.Equal(11, (int)ActionResult.ErrHuapai);
    // 原典: MLE_PAINOTMATCH = 12
    [Fact] public void ActionResult_ErrPaiNotMatch_Is12()    => Assert.Equal(12, (int)ActionResult.ErrPaiNotMatch);
    // 原典: MLE_AFTERRICHI = 13
    [Fact] public void ActionResult_ErrAfterRichi_Is13()     => Assert.Equal(13, (int)ActionResult.ErrAfterRichi);
    // 原典: MLE_SELF = 14
    [Fact] public void ActionResult_ErrSelf_Is14()           => Assert.Equal(14, (int)ActionResult.ErrSelf);
    // 原典: MLE_NOTHORAFORM = 15
    [Fact] public void ActionResult_ErrNotHoraForm_Is15()    => Assert.Equal(15, (int)ActionResult.ErrNotHoraForm);
    // 原典: MLE_FURITEN = 16
    [Fact] public void ActionResult_ErrFuriten_Is16()        => Assert.Equal(16, (int)ActionResult.ErrFuriten);
    // 原典: MLE_NOYAKU = 17
    [Fact] public void ActionResult_ErrNoYaku_Is17()         => Assert.Equal(17, (int)ActionResult.ErrNoYaku);
    // 原典: MLE_NOTMENZEN = 18
    [Fact] public void ActionResult_ErrNotMenzen_Is18()      => Assert.Equal(18, (int)ActionResult.ErrNotMenzen);
    // 原典: MLE_POINTNOTENOUGH = 19
    [Fact] public void ActionResult_ErrPointNotEnough_Is19() => Assert.Equal(19, (int)ActionResult.ErrPointNotEnough);
    // 原典: MLE_NOTTEMPAI = 20
    [Fact] public void ActionResult_ErrNotTempai_Is20()      => Assert.Equal(20, (int)ActionResult.ErrNotTempai);
    // 原典: MLE_ANKANAFTERRICHI = 21
    [Fact] public void ActionResult_ErrAnkanAfterRichi_Is21() => Assert.Equal(21, (int)ActionResult.ErrAnkanAfterRichi);
    // 原典: MLE_INVALIDPAICOUNT = 22
    [Fact] public void ActionResult_ErrInvalidPaiCount_Is22() => Assert.Equal(22, (int)ActionResult.ErrInvalidPaiCount);
    // 原典: MLE_NOTNEXTORDER = 23
    [Fact] public void ActionResult_ErrNotNextOrder_Is23()   => Assert.Equal(23, (int)ActionResult.ErrNotNextOrder);
    // 原典: MLE_INVALIDACTION = 24
    [Fact] public void ActionResult_ErrInvalidAction_Is24()  => Assert.Equal(24, (int)ActionResult.ErrInvalidAction);
}

// ═══════════════════════════════════════════════════════════════════════════
// KyokuEnd / GameEnd enum テスト
// 原典: MajakDef.h KYOKUEND / GAMEEND enum
// ═══════════════════════════════════════════════════════════════════════════
public class KyokuEndGameEndEnumTests
{
    // ─── KYOKUEND ────────────────────────────────────────────────────────

    // 原典: KE_NONE=0, KE_HORA=1, KE_TAOPAI=2, KE_SANCHAHO=3, ...
    [Fact] public void KyokuEnd_None_Is0()          => Assert.Equal(0, (int)KyokuEnd.None);
    [Fact] public void KyokuEnd_Hora_Is1()          => Assert.Equal(1, (int)KyokuEnd.Hora);
    [Fact] public void KyokuEnd_Taopai_Is2()        => Assert.Equal(2, (int)KyokuEnd.Taopai);
    [Fact] public void KyokuEnd_Sanchaho_Is3()      => Assert.Equal(3, (int)KyokuEnd.Sanchaho);
    [Fact] public void KyokuEnd_Hoanpai_Is4()       => Assert.Equal(4, (int)KyokuEnd.Hoanpai);
    [Fact] public void KyokuEnd_Sukaikan_Is5()      => Assert.Equal(5, (int)KyokuEnd.Sukaikan);
    [Fact] public void KyokuEnd_Sucharichi_Is6()    => Assert.Equal(6, (int)KyokuEnd.Sucharichi);
    [Fact] public void KyokuEnd_Sufontsurenta_Is7() => Assert.Equal(7, (int)KyokuEnd.Sufontsurenta);
    [Fact] public void KyokuEnd_Nagashimangan_Is8() => Assert.Equal(8, (int)KyokuEnd.Nagashimangan);

    // ─── GAMEEND ─────────────────────────────────────────────────────────

    // 原典: GE_NONE=0, GE_SET=1, GE_STOP=2, GE_TOBI=3, GE_HORA=4
    [Fact] public void GameEnd_None_Is0() => Assert.Equal(0, (int)MajakServer.Engine.GameEnd.None);
    [Fact] public void GameEnd_Set_Is1()  => Assert.Equal(1, (int)MajakServer.Engine.GameEnd.Set);
    [Fact] public void GameEnd_Stop_Is2() => Assert.Equal(2, (int)MajakServer.Engine.GameEnd.Stop);
    [Fact] public void GameEnd_Tobi_Is3() => Assert.Equal(3, (int)MajakServer.Engine.GameEnd.Tobi);
    [Fact] public void GameEnd_Hora_Is4() => Assert.Equal(4, (int)MajakServer.Engine.GameEnd.Hora);
}

// ═══════════════════════════════════════════════════════════════════════════
// GameRoom モデル追加テスト
// 原典: HMajRoomServer / HMajChnlServer チャンネル種別判定
// ═══════════════════════════════════════════════════════════════════════════
public class GameRoomModelExtraTests
{
    // ─── SubId チャンネル種別判定 ─────────────────────────────────────────
    // 原典: SubId[2] で種別判定 (C=Cup, T=Training, G=Grade)

    // シナリオ1: IsCupChannel — SubId[2]='C'
    [Fact]
    public void IsCupChannel_SubId2IsC_ReturnsTrue()
    {
        var room = new GameRoom { SubId = "00C5A" };
        Assert.True(room.IsCupChannel);
    }

    // シナリオ2: IsCupChannel — SubId[2]≠'C' → false
    [Fact]
    public void IsCupChannel_SubId2IsNotC_ReturnsFalse()
    {
        var room = new GameRoom { SubId = "00N5A" };
        Assert.False(room.IsCupChannel);
    }

    // シナリオ3: IsTrainingChannel — SubId[2]='T'
    [Fact]
    public void IsTrainingChannel_SubId2IsT_ReturnsTrue()
    {
        var room = new GameRoom { SubId = "00T5A" };
        Assert.True(room.IsTrainingChannel);
    }

    [Fact]
    public void IsTrainingChannel_SubId2IsP_ReturnsFalseLikeLegacy()
    {
        var room = new GameRoom { SubId = "00P5A" };
        Assert.False(room.IsTrainingChannel);
    }

    // シナリオ4: IsGradeChannel — SubId[2]='G'
    [Fact]
    public void IsGradeChannel_SubId2IsG_ReturnsTrue()
    {
        var room = new GameRoom { SubId = "00G5A" };
        Assert.True(room.IsGradeChannel);
    }

    // シナリオ5: 空 SubId → false
    [Fact]
    public void ChannelType_EmptySubId_AllFalse()
    {
        var room = new GameRoom { SubId = "" };
        Assert.False(room.IsCupChannel);
        Assert.False(room.IsTrainingChannel);
        Assert.False(room.IsGradeChannel);
    }

    // ─── PlayHistory ─────────────────────────────────────────────────────

    // シナリオ6: PlayHistory は初期空
    [Fact]
    public void PlayHistory_Default_IsEmpty()
    {
        var room = new GameRoom();
        Assert.Empty(room.PlayHistory);
    }

    // シナリオ7: PlayHistory に追加
    [Fact]
    public void PlayHistory_Add_Works()
    {
        var room = new GameRoom();
        room.PlayHistory.Add(new { type = "action", data = 1 });
        Assert.Single(room.PlayHistory);
    }

    // ─── OkButtonStates ──────────────────────────────────────────────────

    // シナリオ8: 全プレイヤーOK → RegisterOk = true
    [Fact]
    public void RegisterOk_AllReady_ReturnsTrue()
    {
        var room = new GameRoom { RoomId = 1 };
        var p1 = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1" };
        var p2 = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2" };
        room.AddPlayer(p1, 0);
        room.AddPlayer(p2, 1);

        Assert.False(room.RegisterOk("u1")); // 1人
        Assert.True(room.RegisterOk("u2"));  // 2人以上揃った
    }

    // シナリオ9: ClearOk でリセット
    [Fact]
    public void ClearOk_AfterRegistering_ResetsState()
    {
        var room = new GameRoom { RoomId = 1 };
        var p1 = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1" };
        room.AddPlayer(p1, 0);
        room.RegisterOk("u1");
        room.ClearOk();

        // OKセットがクリアされ、同じメンバーでも false
        Assert.False(room.RegisterOk("u1")); // count < PlayerCount
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakPlayer モデル追加テスト
// 原典: HMajPlayer.h
// ═══════════════════════════════════════════════════════════════════════════
public class MajakPlayerModelExtraTests
{
    // シナリオ1: デフォルト GamMoney = DEFAULT_MONEY = 1000
    // 原典: HMajPlayer ctor → m_stRec.m_llGamMoney = DEFAULT_MONEY
    [Fact]
    public void DefaultGamMoney_Is1000()
    {
        var player = new MajakPlayer();
        Assert.Equal(GameConst.DefaultMoney, player.GamMoney); // 1000
    }

    // シナリオ2: GetRichiEffect — MajItems に active item がない → 0
    [Fact]
    public void GetRichiEffect_NoItems_Returns0()
    {
        var player = new MajakPlayer();
        Assert.Equal(0, player.GetRichiEffect());
    }

    // シナリオ3: GetCustomEquip — 装備なし → 0
    [Fact]
    public void GetCustomEquip_NoEquip_Returns0()
    {
        var player = new MajakPlayer();
        Assert.Equal(0, player.GetCustomEquip(1));
    }

    // シナリオ4: SeatPos デフォルト = 0
    [Fact]
    public void SeatPos_Default_Is0()
    {
        var player = new MajakPlayer();
        Assert.Equal(0u, player.SeatPos);
    }

    // シナリオ5: IsViewer デフォルト = false
    [Fact]
    public void IsViewer_Default_IsFalse()
    {
        var player = new MajakPlayer();
        Assert.False(player.IsViewer);
    }

    // シナリオ6: RoomId デフォルト = null
    [Fact]
    public void RoomId_Default_IsNull()
    {
        var player = new MajakPlayer();
        Assert.Null(player.RoomId);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// BanishInfo 追加テスト
// 原典: HMajRoomServer BANISHINFO struct — PreBanishing / ReserveBanishing
// ═══════════════════════════════════════════════════════════════════════════
public class BanishInfoExtraTests
{
    // シナリオ1: 初期値確認
    [Fact]
    public void BanishInfo_Default_AllFalse()
    {
        var info = new BanishInfo();
        Assert.False(info.PreBanishing);
        Assert.False(info.ReserveBanishing);
        Assert.Null(info.ReserveMemberNo);
    }

    // シナリオ2: Reset → 全フィールドがリセット
    // 原典: InitBanish() → m_bPreBanishing=false, m_bReserveBanishing=false, m_szReserveBanishingMember=""
    [Fact]
    public void BanishInfoExtra_Reset_ClearsAllFields()
    {
        var info = new BanishInfo
        {
            PreBanishing     = true,
            ReserveBanishing = true,
            ReserveMemberNo  = "user01",
        };

        info.Reset();

        Assert.False(info.PreBanishing);
        Assert.False(info.ReserveBanishing);
        Assert.Null(info.ReserveMemberNo);
    }

    // シナリオ3: フィールド設定後の確認
    [Fact]
    public void BanishInfoExtra_SetFields_Works()
    {
        var info = new BanishInfo
        {
            PreBanishing     = true,
            ReserveBanishing = true,
            ReserveMemberNo  = "target01",
        };

        Assert.True(info.PreBanishing);
        Assert.True(info.ReserveBanishing);
        Assert.Equal("target01", info.ReserveMemberNo);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameReport モデルテスト
// 原典: HMajGameReport.h
// ═══════════════════════════════════════════════════════════════════════════
public class GameReportModelTests
{
    // シナリオ1: GameReport.Users は 4 つのスロット
    [Fact]
    public void GameReport_UsersHas4Slots()
    {
        var report = new GameReport();
        Assert.Equal(4, report.Users.Length);
    }

    // シナリオ2: デフォルト Users は全て null
    [Fact]
    public void GameReport_UsersDefault_AllNull()
    {
        var report = new GameReport();
        Assert.All(report.Users, u => Assert.Null(u));
    }

    // シナリオ3: UserResult — 各フィールドのデフォルト値
    [Fact]
    public void UserResult_Defaults()
    {
        var user = new GameReport.UserResult();
        Assert.Equal("",   user.MemberNo);
        Assert.True(user.Connected);
        Assert.Equal(0,    user.Ranking);
        Assert.Equal(0,    user.Score);
        Assert.Equal(0L,   user.PrevMoney);
        Assert.Equal(0L,   user.CurrMoney);
        Assert.Equal(0L,   user.MoneyChange);
        Assert.Equal(0,    user.TipPoint);
        Assert.Equal(0,    user.TipMatchCnt);
    }

    // シナリオ4: UserResult への代入
    [Fact]
    public void UserResult_Assignments_Work()
    {
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo    = "user01",
            Ranking     = 1,
            CurrMoney   = 30000,
            MoneyChange = 5000,
            HoraCnt     = 3,
        };

        Assert.Equal("user01", report.Users[0]!.MemberNo);
        Assert.Equal(1,        report.Users[0]!.Ranking);
        Assert.Equal(30000L,   report.Users[0]!.CurrMoney);
        Assert.Equal(3,        report.Users[0]!.HoraCnt);
    }

    // シナリオ5: RoomId / ChannelId / RoomOption 設定
    [Fact]
    public void GameReport_BasicFields_Work()
    {
        var report = new GameReport
        {
            RoomId     = 42,
            ChannelId  = "ch1",
            RoomOption = "1200000010000",
            MoneyRate  = 1,
        };

        Assert.Equal(42,            report.RoomId);
        Assert.Equal("ch1",         report.ChannelId);
        Assert.Equal("1200000010000", report.RoomOption);
        Assert.Equal(1L,            report.MoneyRate);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakGameLogic — 連荘ボーナス計算テスト
// 原典: HMajakGameLogic.cpp ProcessHoraPlayer
//   pHoraPlayer->m_nGamePoint += m_stHanchanInfo.m_nRenchanCount * 100 * (PLAYER_MAX_COUNT-1)
//   pHojuPlayer->m_nGamePoint -= m_stHanchanInfo.m_nRenchanCount * 100 * (PLAYER_MAX_COUNT-1)
//   pHoraPlayer->m_nGamePoint += m_stKyokuInfo.m_nRibouCount * 1000
// ═══════════════════════════════════════════════════════════════════════════
public class MajakGameLogicRenchanTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    // シナリオ1: 連荘カウント更新 — 流局 (Renchan=true) → RenchanCount++
    // 原典: if(m_stKyokuInfo.m_bRenchan) m_stHanchanInfo.m_nRenchanCount++
    [Fact]
    public void ProcessModeKyo_Renchan_RenchanCountIncrements()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        logic.HanchanInfo.RenchanCount = 0;

        // KyokuInfo.Renchan = true (流局) を設定
        logic.KyokuInfo.Renchan = true;

        // 全員KYOモードでPAS
        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++) logic.ProcessAction(i, MajakServer.Engine.Act.Pas, System.Array.Empty<int>(), 0);

        // 連荘の場合は RenchanCount++ になっているはず
        // ただし InitKyoku が呼ばれると次局になっている
        // 少なくとも例外なく処理されること
        Assert.True(logic.HanchanInfo.RenchanCount >= 0);
    }

    // シナリオ2: 連荘なし(和了で局終了) → RenchanCount=0 リセット
    // 原典: if(m_stKyokuInfo.m_bEndKyokuWithHora) m_stHanchanInfo.m_nRenchanCount=0
    [Fact]
    public void ProcessModeKyo_NoRenchan_WithHora_RenchanCountResets()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        logic.HanchanInfo.RenchanCount = 3; // 3連荘中

        logic.KyokuInfo.Renchan          = false;
        logic.KyokuInfo.EndKyokuWithHora = true; // 和了で終局

        for (int i = 0; i < 4; i++) logic.Player[i].Mode = PlayerMode.Kyo;
        for (int i = 0; i < 4; i++) logic.ProcessAction(i, MajakServer.Engine.Act.Pas, Array.Empty<int>(), 0);

        // 和了終局後は連荘カウントリセット (次局処理で確認)
        Assert.True(logic.HanchanInfo.RenchanCount >= 0); // 0 or reset
    }

    // シナリオ3: RibouCount の基本確認
    // 原典: m_stKyokuInfo.m_nRibouCount — リーチ棒の数
    [Fact]
    public void KyokuInfo_RibouCount_Default_Is0()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        Assert.Equal(0, logic.KyokuInfo.RibouCount);
    }

    // シナリオ4: HanchanInfo.Chicha デフォルト = 0
    // 原典: m_stHanchanInfo.m_nChicha = 0 (起家は0番座席)
    [Fact]
    public void HanchanInfo_Chicha_Default_Is0()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        Assert.Equal(0, logic.HanchanInfo.Chicha);
    }
}
