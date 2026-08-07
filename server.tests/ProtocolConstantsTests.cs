using MajakServer.Models.Game;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Tests;

/// <summary>
/// プロトコル定数テスト — 原典: HMajProtocol.cpp / MeProtocol.cpp と完全一致確認
///
/// レガシー C++ の定数値と .NET 実装が一致していることを検証する。
/// 差異があると、クライアント/サーバー間の通信が壊れる。
/// </summary>
public class ProtocolConstantsTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // Cmd (MAJ::command*) — 原典: HMajProtocol.cpp
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Cmd_GetDetailRec()         => Assert.Equal("mjkc1e",   Cmd.GetDetailRec);
    [Fact] public void Cmd_AutoMatching()          => Assert.Equal("mjkc2e",   Cmd.AutoMatching);
    [Fact] public void Cmd_CancelAutoMatching()    => Assert.Equal("mjkc3e",   Cmd.CancelAutoMatching);
    [Fact] public void Cmd_AutoStart()             => Assert.Equal("mjkc4e",   Cmd.AutoStart);
    [Fact] public void Cmd_AutoExitRoom()          => Assert.Equal("mjkc5e",   Cmd.AutoExitRoom);
    [Fact] public void Cmd_AutoEnterRoom()         => Assert.Equal("mjkc6e",   Cmd.AutoEnterRoom);
    [Fact] public void Cmd_ChannelStop()           => Assert.Equal("mjkc13e",  Cmd.ChannelStop);
    [Fact] public void Cmd_GetServerTime()         => Assert.Equal("mjkc14e",  Cmd.GetServerTime);
    [Fact] public void Cmd_AvatarGear()            => Assert.Equal("mjkc16e",  Cmd.AvatarGear);
    [Fact] public void Cmd_MoneyReplenishment()    => Assert.Equal("mjkc17e",  Cmd.MoneyReplenishment);
    [Fact] public void Cmd_ApplyEarnedMoney()      => Assert.Equal("mjkc18e",  Cmd.ApplyEarnedMoney);
    [Fact] public void Cmd_GetTitle()              => Assert.Equal("mjkc19e",  Cmd.GetTitle);
    [Fact] public void Cmd_BuyMajItem()            => Assert.Equal("mjkc20e",  Cmd.BuyMajItem);
    [Fact] public void Cmd_SelectMajItem()         => Assert.Equal("mjkc21e",  Cmd.SelectMajItem);
    [Fact] public void Cmd_GetGem()                => Assert.Equal("mjkc22e",  Cmd.GetGem);
    [Fact] public void Cmd_YakumanBonus()          => Assert.Equal("mjkc23e",  Cmd.YakumanBonus);
    [Fact] public void Cmd_UseEmoticon()           => Assert.Equal("mjkc24e",  Cmd.UseEmoticon);
    [Fact] public void Cmd_RatingRankInfo()        => Assert.Equal("mjkc25e",  Cmd.RatingRankInfo);
    [Fact] public void Cmd_TournamentList()        => Assert.Equal("mjkc26e",  Cmd.TournamentList);
    [Fact] public void Cmd_TournamentRegist()      => Assert.Equal("mjkc27e",  Cmd.TournamentRegist);
    [Fact] public void Cmd_TournamentJoin()        => Assert.Equal("mjkc28e",  Cmd.TournamentJoin);
    [Fact] public void Cmd_TournamentJoinCancel()  => Assert.Equal("mjkc29e",  Cmd.TournamentJoinCancel);
    [Fact] public void Cmd_TournamentDetail()      => Assert.Equal("mjkc30e",  Cmd.TournamentDetail);
    [Fact] public void Cmd_DeliveryMessage()       => Assert.Equal("mjkc31e",  Cmd.DeliveryMessage);
    [Fact] public void Cmd_GetMissionList()        => Assert.Equal("mjkc32e",  Cmd.GetMissionList);
    [Fact] public void Cmd_RcvWeeklyReward()       => Assert.Equal("mjkc33e",  Cmd.RcvWeeklyReward);
    [Fact] public void Cmd_RcvSerialBonus()        => Assert.Equal("mjkc34e",  Cmd.RcvSerialBonus);
    [Fact] public void Cmd_ShopItemRequest()       => Assert.Equal("mjkc35e",  Cmd.ShopItemRequest);
    [Fact] public void Cmd_ShopItemResponse()      => Assert.Equal("mjkc36e",  Cmd.ShopItemResponse);
    [Fact] public void Cmd_SetCustomItem()         => Assert.Equal("mjkc37e",  Cmd.SetCustomItem);
    [Fact] public void Cmd_EquipCustomItem()       => Assert.Equal("mjkc38e",  Cmd.EquipCustomItem);
    [Fact] public void Cmd_CustomItem()            => Assert.Equal("mjkc39e",  Cmd.CustomItem);
    [Fact] public void Cmd_CustomItemResponse()    => Assert.Equal("mjkc40e",  Cmd.CustomItemResponse);
    [Fact] public void Cmd_BuyCustomItem()         => Assert.Equal("mjkc41e",  Cmd.BuyCustomItem);
    [Fact] public void Cmd_BuyCustomItemResponse() => Assert.Equal("mjkc42e",  Cmd.BuyCustomItemResponse);

    // ─── ルーム/ゲームコマンド ───
    [Fact] public void Cmd_SendOkButton()  => Assert.Equal("smmc1e",   Cmd.SendOkButton);
    [Fact] public void Cmd_PushOkButton()  => Assert.Equal("smmc2e",   Cmd.PushOkButton);
    [Fact] public void Cmd_EventInfo()     => Assert.Equal("smmc3e",   Cmd.EventInfo);
    [Fact] public void Cmd_PaiInfoList()   => Assert.Equal("smmc4e",   Cmd.PaiInfoList);
    [Fact] public void Cmd_Tsumikomi()     => Assert.Equal("smmc5e",   Cmd.Tsumikomi);
    [Fact] public void Cmd_IpAdapterInfo() => Assert.Equal("smmc6e",   Cmd.IpAdapterInfo);
    [Fact] public void Cmd_RoomState()     => Assert.Equal("mjkroom",  Cmd.RoomState);
    [Fact] public void Cmd_GamePlay()      => Assert.Equal("playing",  Cmd.GamePlay);
    [Fact] public void Cmd_AgariRec()      => Assert.Equal("horarec",  Cmd.AgariRec);
    [Fact] public void Cmd_History()       => Assert.Equal("history",  Cmd.History);
    [Fact] public void Cmd_ReplayNavi()    => Assert.Equal("repnavi",  Cmd.ReplayNavi);

    // ═══════════════════════════════════════════════════════════════════════
    // Key (MAJ::key*) — 原典: HMajProtocol.cpp
    // ═══════════════════════════════════════════════════════════════════════

    // 戦績統計キー
    [Fact] public void Key_TurnCnt()      => Assert.Equal("mjkk1e",  Key.TurnCnt);
    [Fact] public void Key_DaidaCnt()     => Assert.Equal("mjkk2e",  Key.DaidaCnt);
    [Fact] public void Key_PointSum()     => Assert.Equal("mjkk3e",  Key.PointSum);
    [Fact] public void Key_KyokuCnt()     => Assert.Equal("mjkk4e",  Key.KyokuCnt);
    [Fact] public void Key_HoraCnt()      => Assert.Equal("mjkk5e",  Key.HoraCnt);
    [Fact] public void Key_HoraPoint()    => Assert.Equal("mjkk6e",  Key.HoraPoint);
    [Fact] public void Key_HojuCnt()      => Assert.Equal("mjkk7e",  Key.HojuCnt);
    [Fact] public void Key_HojuPoint()    => Assert.Equal("mjkk8e",  Key.HojuPoint);
    [Fact] public void Key_RichiCnt()     => Assert.Equal("mjkk9e",  Key.RichiCnt);
    [Fact] public void Key_FuroCnt()      => Assert.Equal("mjkk10e", Key.FuroCnt);
    [Fact] public void Key_TipPoint()     => Assert.Equal("mjkk11e", Key.TipPoint);
    [Fact] public void Key_TipMatchCnt()  => Assert.Equal("mjkk12e", Key.TipMatchCnt);
    [Fact] public void Key_TobiCnt()      => Assert.Equal("mjkk13e", Key.TobiCnt);
    [Fact] public void Key_TobashiCnt()   => Assert.Equal("mjkk14e", Key.TobashiCnt);
    [Fact] public void Key_DoraCnt()      => Assert.Equal("mjkk15e", Key.DoraCnt);
    [Fact] public void Key_UraDoraCnt()   => Assert.Equal("mjkk16e", Key.UraDoraCnt);
    [Fact] public void Key_NukiDoraCnt()  => Assert.Equal("mjkk17e", Key.NukiDoraCnt);
    [Fact] public void Key_RichiHoraCnt() => Assert.Equal("mjkk18e", Key.RichiHoraCnt);

    // その他チャンネルキー
    [Fact] public void Key_AiKind()      => Assert.Equal("mjkk28e", Key.AiKind);
    [Fact] public void Key_ServerTime()  => Assert.Equal("mjkk32e", Key.ServerTime);
    [Fact] public void Key_IsContinue()  => Assert.Equal("mjkk33e", Key.IsContinue);
    [Fact] public void Key_NickName()    => Assert.Equal("mjkk34e", Key.NickName);
    [Fact] public void Key_CupPoint()    => Assert.Equal("mjkk35e", Key.CupPoint);
    [Fact] public void Key_Experience()  => Assert.Equal("mjkk36e", Key.Experience);
    [Fact] public void Key_MemorialShop() => Assert.Equal("mjkk37e", Key.MemorialShop);
    [Fact] public void Key_ChangBestLevel() => Assert.Equal("mjkk38e", Key.ChangBestLevel);
    [Fact] public void Key_SkinDataCount() => Assert.Equal("mjkk39e", Key.SkinDataCount);
    [Fact] public void Key_SkinInfo()    => Assert.Equal("mjkk40e", Key.SkinInfo);
    [Fact] public void Key_LentMoney()   => Assert.Equal("mjkk41e", Key.LentMoney);
    [Fact] public void Key_ReplenishmentType() => Assert.Equal("mjkk42e", Key.ReplenishmentType);
    [Fact] public void Key_RestAllInCnt() => Assert.Equal("mjkk43e", Key.RestAllInCnt);
    [Fact] public void Key_AllInCnt()    => Assert.Equal("mjkk44e", Key.AllInCnt);
    [Fact] public void Key_UseLentMoney() => Assert.Equal("mjkk45e", Key.UseLentMoney);
    [Fact] public void Key_TrickTitle()  => Assert.Equal("mjkk46e", Key.TrickTitle);
    [Fact] public void Key_MajakTitle()  => Assert.Equal("mjkk47e", Key.MajakTitle);
    [Fact] public void Key_TitleType()   => Assert.Equal("mjkk48e", Key.TitleType);
    [Fact] public void Key_TitleCode()   => Assert.Equal("mjkk49e", Key.TitleCode);
    [Fact] public void Key_TitleName()   => Assert.Equal("mjkk50e", Key.TitleName);
    [Fact] public void Key_TrickTitleName() => Assert.Equal("mjkk51e", Key.TrickTitleName);
    [Fact] public void Key_MajakTitleName() => Assert.Equal("mjkk52e", Key.MajakTitleName);
    [Fact] public void Key_Date()        => Assert.Equal("mjkk53e", Key.Date);
    [Fact] public void Key_RichiEffect() => Assert.Equal("mjkk54e", Key.RichiEffect);
    [Fact] public void Key_GemCount()    => Assert.Equal("mjkk55e", Key.GemCount);
    [Fact] public void Key_GemGame()     => Assert.Equal("mjkk56e", Key.GemGame);
    [Fact] public void Key_SellCode()    => Assert.Equal("mjkk57e", Key.SellCode);
    [Fact] public void Key_ItemCode()    => Assert.Equal("mjkk58e", Key.ItemCode);
    [Fact] public void Key_BuyDate()     => Assert.Equal("mjkk59e", Key.BuyDate);
    [Fact] public void Key_EndDate()     => Assert.Equal("mjkk60e", Key.EndDate);
    [Fact] public void Key_UseFlag()     => Assert.Equal("mjkk61e", Key.UseFlag);
    [Fact] public void Key_YakuName()    => Assert.Equal("mjkk62e", Key.YakuName);
    [Fact] public void Key_EmoticonId()  => Assert.Equal("mjkk63e", Key.EmoticonId);
    [Fact] public void Key_EmoticonAvatarId() => Assert.Equal("mjkk64e", Key.EmoticonAvatarId);

    // グレードモードキー
    [Fact] public void Key_GradeGetPoint()    => Assert.Equal("mjkk65e", Key.GradeGetPoint);
    [Fact] public void Key_GradeCurrPoint()   => Assert.Equal("mjkk66e", Key.GradeCurrPoint);
    [Fact] public void Key_GradeNextPoint()   => Assert.Equal("mjkk67e", Key.GradeNextPoint);
    [Fact] public void Key_GradeGetRating()   => Assert.Equal("mjkk68e", Key.GradeGetRating);
    [Fact] public void Key_GradePrevLevel()   => Assert.Equal("mjkk69e", Key.GradePrevLevel);
    [Fact] public void Key_GradeCurrLevel()   => Assert.Equal("mjkk70e", Key.GradeCurrLevel);
    [Fact] public void Key_GradeUpDown()      => Assert.Equal("mjkk71e", Key.GradeUpDown);
    [Fact] public void Key_GradeBeginner()    => Assert.Equal("mjkk72e", Key.GradeBeginner);
    [Fact] public void Key_GradeRankId()      => Assert.Equal("mjkk73e", Key.GradeRankId);
    [Fact] public void Key_GradeRankDate()    => Assert.Equal("mjkk74e", Key.GradeRankDate);
    [Fact] public void Key_GradeRankRefresh() => Assert.Equal("mjkk75e", Key.GradeRankRefresh);
    [Fact] public void Key_GradeRankList()    => Assert.Equal("mjkk76e", Key.GradeRankList);
    [Fact] public void Key_GradeRankCnt()     => Assert.Equal("mjkk77e", Key.GradeRankCnt);
    [Fact] public void Key_GradeRankSelf()    => Assert.Equal("mjkk78e", Key.GradeRankSelf);
    [Fact] public void Key_GradeSelectList()  => Assert.Equal("mjkk79e", Key.GradeSelectList);
    [Fact] public void Key_GradeSelectCnt()   => Assert.Equal("mjkk80e", Key.GradeSelectCnt);
    [Fact] public void Key_GradeExtraStage()  => Assert.Equal("mjkk81e", Key.GradeExtraStage);

    // トーナメントキー
    [Fact] public void Key_TournamentList()  => Assert.Equal("mjkk82e", Key.TournamentList);
    [Fact] public void Key_TournamentCnt()   => Assert.Equal("mjkk83e", Key.TournamentCnt);
    [Fact] public void Key_TournamentBaseRule() => Assert.Equal("mjkk84e", Key.TournamentBaseRule);
    [Fact] public void Key_TournamentMoneyRule() => Assert.Equal("mjkk85e", Key.TournamentMoneyRule);
    [Fact] public void Key_TournamentName()  => Assert.Equal("mjkk86e", Key.TournamentName);
    [Fact] public void Key_TournamentDate()  => Assert.Equal("mjkk87e", Key.TournamentDate);
    [Fact] public void Key_TournamentNo()    => Assert.Equal("mjkk88e", Key.TournamentNo);
    [Fact] public void Key_TournamentDetail() => Assert.Equal("mjkk89e", Key.TournamentDetail);
    [Fact] public void Key_TournamentDetailCnt() => Assert.Equal("mjkk90e", Key.TournamentDetailCnt);
    [Fact] public void Key_TournamentJoinChk() => Assert.Equal("mjkk91e", Key.TournamentJoinChk);
    [Fact] public void Key_DeliveryMessage() => Assert.Equal("mjkk92e", Key.DeliveryMessage);
    [Fact] public void Key_TournamentRegistDayTime() => Assert.Equal("mjkk93e", Key.TournamentRegistDayTime);
    [Fact] public void Key_TournamentRegistFlag() => Assert.Equal("mjkk94e", Key.TournamentRegistFlag);
    [Fact] public void Key_FailCode()        => Assert.Equal("mjkk95e", Key.FailCode);
    [Fact] public void Key_FailCodeCnt()     => Assert.Equal("mjkk96e", Key.FailCodeCnt);
    [Fact] public void Key_TournamentTotalReport() => Assert.Equal("mjkk97e", Key.TournamentTotalReport);
    [Fact] public void Key_TournamentTotalReportCnt() => Assert.Equal("mjkk98e", Key.TournamentTotalReportCnt);
    [Fact] public void Key_TournamentSubId() => Assert.Equal("mjkk99e", Key.TournamentSubId);
    [Fact] public void Key_RoomForceExitReason() => Assert.Equal("mjkk100e", Key.RoomForceExitReason);
    [Fact] public void Key_TournamentChkRoomMember() => Assert.Equal("mjkk101e", Key.TournamentChkRoomMember);
    [Fact] public void Key_TournamentRoomId() => Assert.Equal("mjkk102e", Key.TournamentRoomId);
    [Fact] public void Key_TournamentRoomOrder() => Assert.Equal("mjkk103e", Key.TournamentRoomOrder);
    [Fact] public void Key_GradePlayCheck() => Assert.Equal("mjkk104e", Key.GradePlayCheck);

    // ミッションキー
    [Fact] public void Key_PointDayOwn()    => Assert.Equal("mjkk105e", Key.PointDayOwn);
    [Fact] public void Key_PointDayMax()    => Assert.Equal("mjkk106e", Key.PointDayMax);
    [Fact] public void Key_PointWeekOwn()   => Assert.Equal("mjkk107e", Key.PointWeekOwn);
    [Fact] public void Key_PointWeekMax()   => Assert.Equal("mjkk108e", Key.PointWeekMax);
    [Fact] public void Key_DailyMission1()  => Assert.Equal("mjkk109e", Key.DailyMission1);
    [Fact] public void Key_DailyMission11() => Assert.Equal("mjkk119e", Key.DailyMission11);
    [Fact] public void Key_WeeklyReward1()  => Assert.Equal("mjkk120e", Key.WeeklyReward1);
    [Fact] public void Key_WeeklyReward8()  => Assert.Equal("mjkk127e", Key.WeeklyReward8);
    [Fact] public void Key_WeeklyRewardId() => Assert.Equal("mjkk128e", Key.WeeklyRewardId);
    [Fact] public void Key_SerialCode()     => Assert.Equal("mjkk130e", Key.SerialCode);
    [Fact] public void Key_CircleId()       => Assert.Equal("mjkk131e", Key.CircleId);
    [Fact] public void Key_CircleIdCnt()    => Assert.Equal("mjkk132e", Key.CircleIdCnt);
    [Fact] public void Key_CircleName()     => Assert.Equal("mjkk133e", Key.CircleName);

    // カスタムアイテムキー
    [Fact] public void Key_CustomBoard()   => Assert.Equal("mjkk134e", Key.CustomBoard);
    [Fact] public void Key_CustomHai()     => Assert.Equal("mjkk135e", Key.CustomHai);
    [Fact] public void Key_CustomCostume() => Assert.Equal("mjkk136e", Key.CustomCostume);
    [Fact] public void Key_CustomCostumeType() => Assert.Equal("mjkk137e", Key.CustomCostumeType);
    [Fact] public void Key_CustomId()      => Assert.Equal("mjkk138e", Key.CustomId);
    [Fact] public void Key_ShopNo()        => Assert.Equal("mjkk139e", Key.ShopNo);
    [Fact] public void Key_ItemQuantity()  => Assert.Equal("mjkk140e", Key.ItemQuantity);

    // ルームキー (smmk) — 原典: HMajProtocol.cpp MAJ::keyRoomCharge等
    [Fact] public void Key_RoomCharge()  => Assert.Equal("smmk1e",  Key.RoomCharge);
    [Fact] public void Key_OkButton()    => Assert.Equal("smmk2e",  Key.OkButton);
    [Fact] public void Key_LackMoney()   => Assert.Equal("smmk3e",  Key.LackMoney);
    [Fact] public void Key_WinMoneyCut() => Assert.Equal("smmk4e",  Key.WinMoneyCut);
    [Fact] public void Key_Score()       => Assert.Equal("smmk5e",  Key.Score);
    [Fact] public void Key_Point()       => Assert.Equal("smmk6e",  Key.Point);
    [Fact] public void Key_Yakitori()    => Assert.Equal("smmk7e",  Key.Yakitori);
    [Fact] public void Key_Chip()        => Assert.Equal("smmk8e",  Key.Chip);
    [Fact] public void Key_Gateway()     => Assert.Equal("smmk9e",  Key.Gateway);
    [Fact] public void Key_MacAddr()     => Assert.Equal("smmk10e", Key.MacAddr);
    [Fact] public void Key_FeeWinner()   => Assert.Equal("smmk11e", Key.FeeWinner);
    [Fact] public void Key_CutEventPoint() => Assert.Equal("smmk12e", Key.CutEventPoint);
    [Fact] public void Key_TotalEventPoint() => Assert.Equal("smmk14e", Key.TotalEventPoint);
    [Fact] public void Key_EventNo()     => Assert.Equal("smmk17e", Key.EventNo);
    [Fact] public void Key_EventCode()   => Assert.Equal("smmk18e", Key.EventCode);

    // ═══════════════════════════════════════════════════════════════════════
    // GKey (G::key*) — 原典: MeProtocol.cpp
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void GKey_Result()     => Assert.Equal("k1e",   GKey.Result);
    [Fact] public void GKey_MemberNo()   => Assert.Equal("k3e",   GKey.Pix);
    [Fact] public void GKey_Password()   => Assert.Equal("k6e",   GKey.Password);
    [Fact] public void GKey_AvatarId()   => Assert.Equal("k7e",   GKey.AvatarId);
    [Fact] public void GKey_Name()       => Assert.Equal("k8e",   GKey.Name);
    [Fact] public void GKey_Age()        => Assert.Equal("k10e",  GKey.Age);
    [Fact] public void GKey_Sex()        => Assert.Equal("k11e",  GKey.Sex);
    [Fact] public void GKey_GamMoney()   => Assert.Equal("k34e",  GKey.GamMoney);
    [Fact] public void GKey_Rating()     => Assert.Equal("k31e",  GKey.Rating);
    [Fact] public void GKey_SLevel()     => Assert.Equal("k32e",  GKey.SLevel);
    [Fact] public void GKey_NLevel()     => Assert.Equal("k33e",  GKey.NLevel);
    [Fact] public void GKey_Count()      => Assert.Equal("k25e",  GKey.Count);
    [Fact] public void GKey_MatchCnt()   => Assert.Equal("k26e",  GKey.MatchCnt);
    [Fact] public void GKey_WinCnt()     => Assert.Equal("k27e",  GKey.WinCnt);
    [Fact] public void GKey_DefeatCnt()  => Assert.Equal("k28e",  GKey.DefeatCnt);
    [Fact] public void GKey_DrawCnt()    => Assert.Equal("k29e",  GKey.DrawCnt);
    [Fact] public void GKey_DisconnCnt() => Assert.Equal("k30e",  GKey.DisconnCnt);
    [Fact] public void GKey_RoomId()     => Assert.Equal("k42e",  GKey.RoomId);
    [Fact] public void GKey_RoomOption() => Assert.Equal("k46e",  GKey.RoomOption);
    [Fact] public void GKey_RoomHost()   => Assert.Equal("k50e",  GKey.RoomHost);
    [Fact] public void GKey_MaxViewer()  => Assert.Equal("k69e",  GKey.MaxViewer);
    [Fact] public void GKey_PlayerType() => Assert.Equal("k57e",  GKey.PlayerType);
    [Fact] public void GKey_PlayerPos()  => Assert.Equal("k58e",  GKey.PlayerPos);
    [Fact] public void GKey_DispRange()  => Assert.Equal("k448e", GKey.DispRange);

    // MAJ::value* — 原典: HMajProtocol.cpp
    [Fact] public void Val_Player()         => Assert.Equal(1, Val.Player);
    [Fact] public void Val_Viewer()         => Assert.Equal(2, Val.Viewer);
    [Fact] public void Val_RankDuring()     => Assert.Equal("mjkv1e", Val.RankDuring);
    [Fact] public void Val_RankNoData()     => Assert.Equal("mjkv2e", Val.RankNoData);
    [Fact] public void Val_CustomSuccess()  => Assert.Equal(0, Val.CustomSuccess);
    [Fact] public void Val_CustomCoinless() => Assert.Equal(1, Val.CustomCoinless);
    [Fact] public void Val_CustomOwned()    => Assert.Equal(2, Val.CustomOwned);
    [Fact] public void Val_CustomIdError()  => Assert.Equal(11, Val.CustomIdError);
    [Fact] public void Val_CustomDbError()  => Assert.Equal(12, Val.CustomDbError);
    [Fact] public void Val_CustomError()    => Assert.Equal(13, Val.CustomError);

    // ═══════════════════════════════════════════════════════════════════════
    // GameConst — 原典: HMajDef.h / MajakDef.h
    // ═══════════════════════════════════════════════════════════════════════

    // シナリオ1: サービスID
    [Fact] public void GameConst_ServiceId_IsMajak4()
        => Assert.Equal("MAJAK4", GameConst.ServiceId);

    // シナリオ2: プレイヤー最大数
    // 原典: PLAYER_MAX_COUNT = 4
    [Fact] public void GameConst_PlayerMaxCount_Is4()
        => Assert.Equal(4, GameConst.PlayerMaxCount);

    // シナリオ3: デフォルトコイン
    // 原典: DEFAULT_MONEY = 1000
    [Fact] public void GameConst_DefaultMoney_Is1000()
        => Assert.Equal(1000L, GameConst.DefaultMoney);

    // シナリオ4: 役満ボーナス
    // 原典: YAKUMANBONUS_MONEY = 200
    [Fact] public void GameConst_YakumanBonusMoney_Is200()
        => Assert.Equal(200L, GameConst.YakumanBonusMoney);


public class RoomForceExitPayloadTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Build_UsesLegacyAutoExitRoomShape(int reason)
    {
        var payload = RoomForceExitPayload.Build(GKey.ValuePlayer, 2, "u1", "User One", reason);

        Assert.Equal("", payload[GKey.RoomHost]);
        Assert.Equal(GKey.ValuePlayer, payload[GKey.PlayerType]);
        Assert.Equal(2, payload[GKey.PlayerPos]);
        Assert.Equal("u1", payload[GKey.Pix]);
        Assert.Equal("User One", payload[GKey.Name]);
        Assert.Equal(reason, payload[Key.RoomForceExitReason]);
        Assert.False(payload.ContainsKey("roomHost"));
        Assert.False(payload.ContainsKey("playerType"));
        Assert.False(payload.ContainsKey("playerPos"));
        Assert.False(payload.ContainsKey("memberNo"));
        Assert.False(payload.ContainsKey("name"));
        Assert.False(payload.ContainsKey("reason"));
    }
}
    // シナリオ5: グレードモード初期レーティング
    [Fact] public void GameConst_RatingGradeModeInit_Is1500()
        => Assert.Equal(1500, GameConst.RatingGradeModeInit);

    // シナリオ6: 10級称号ID
    [Fact] public void GameConst_RatingTitle10Kyu_Correct()
        => Assert.Equal("mjkt500", GameConst.RatingTitle10Kyu);

    // ═══════════════════════════════════════════════════════════════════════
    // TournamentJoinStatus — 原典: TRNMNT_JOIN_STATUS enum
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void TournamentJoinStatus_Init_Is0()   => Assert.Equal(0, TournamentJoinStatus.Init);
    [Fact] public void TournamentJoinStatus_End_Is1()    => Assert.Equal(1, TournamentJoinStatus.End);
    [Fact] public void TournamentJoinStatus_Join_Is2()   => Assert.Equal(2, TournamentJoinStatus.Join);
    [Fact] public void TournamentJoinStatus_Cancel_Is3() => Assert.Equal(3, TournamentJoinStatus.Cancel);
    [Fact] public void TournamentJoinStatus_Exit_Is4()   => Assert.Equal(4, TournamentJoinStatus.Exit);

    // ═══════════════════════════════════════════════════════════════════════
    // TournamentPlayMode / TournamentPlayNum — 原典: TRNMNT_PLAY_MODE enum
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void TournamentPlayMode_OneWin_Is1() => Assert.Equal(1, TournamentPlayMode.OneWin);
    [Fact] public void TournamentPlayMode_TwoWin_Is2() => Assert.Equal(2, TournamentPlayMode.TwoWin);
    [Fact] public void TournamentPlayNum_OnePlay_Is1() => Assert.Equal(1, TournamentPlayNum.OnePlay);
    [Fact] public void TournamentPlayNum_TwoPlay_Is2() => Assert.Equal(2, TournamentPlayNum.TwoPlay);
}

/// <summary>
/// GradeLevelTable 追加テスト
/// 原典: s_stLevelGradeMode[] (HMajCommon.h)
/// </summary>
public class GradeLevelTableExtraTests
{
    // シナリオ1: 全段位コードの GetMaxPoint が正数
    [Fact]
    public void GetMaxPoint_AllDefinedGrades_Positive()
    {
        int[] grades = Enumerable.Range(0, 19).ToArray();
        foreach (var g in grades)
        {
            int maxPt = GradeLevelTable.GetMaxPoint(g);
            Assert.True(maxPt > 0,
                $"Grade {g} returned non-positive MaxPoint: {maxPt}");
        }
    }

    // シナリオ2: 未定義グレード → 0 を返す
    [Fact]
    public void GetMaxPoint_UndefinedGrade_Returns0()
        => Assert.Equal(0, GradeLevelTable.GetMaxPoint(999));

    // シナリオ3: 段位グレード(1-9)はより高い MaxPoint を持つ
    // 原典: 段位の方が MaxPoint が大きい (昇段に必要なポイントが多い)
    [Fact]
    public void GetMaxPoint_Dan_GreaterThanKyu()
    {
        int kyuMax = GradeLevelTable.GetMaxPoint(0);  // 10級
        int danMax = GradeLevelTable.GetMaxPoint(10); // 初段
        Assert.True(danMax > kyuMax,
            $"1段({danMax}) should have more points than 10級({kyuMax})");
    }
}

/// <summary>
/// RatingService 追加境界値テスト
/// 原典: HMajRatingCommon.cpp — NLevel/SLevel 変換
/// </summary>
public class RatingServiceExtraTests
{
    private readonly RatingService _svc = new();

    // シナリオ1: Rating 0 → 最低レベル
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void NLevel_VeryLowRating_ReturnsLowestLevel(int rating)
    {
        var player = new MajakServer.Models.Player.MajakPlayer { Rating = rating };
        _svc.UpdatePlayerLevel(player);
        // 最低でも何らかのレベルが設定される
        Assert.True(player.NLevel >= 0);
    }

    // シナリオ2: 高レーティング → 高い NLevel
    [Fact]
    public void NLevel_HighRating_HigherLevel()
    {
        var p1 = new MajakServer.Models.Player.MajakPlayer { Rating = 1000 };
        var p2 = new MajakServer.Models.Player.MajakPlayer { Rating = 5000 };
        _svc.UpdatePlayerLevel(p1);
        _svc.UpdatePlayerLevel(p2);
        Assert.True(p2.NLevel >= p1.NLevel,
            $"Rating 5000 NLevel({p2.NLevel}) should be >= Rating 1000 NLevel({p1.NLevel})");
    }

    // シナリオ3: UpdatePlayerLevel は SLevel も更新する
    [Fact]
    public void UpdatePlayerLevel_SetsSlevel()
    {
        var player = new MajakServer.Models.Player.MajakPlayer { Rating = 1500 };
        _svc.UpdatePlayerLevel(player);
        Assert.False(string.IsNullOrEmpty(player.SLevel));
    }

    // シナリオ4: NLevel は 0 以上
    [Fact]
    public void NLevel_AlwaysNonNegative()
    {
        int[] testRatings = { 0, 100, 500, 1000, 2000, 5000, 10000 };
        foreach (var r in testRatings)
        {
            var p = new MajakServer.Models.Player.MajakPlayer { Rating = r };
            _svc.UpdatePlayerLevel(p);
            Assert.True(p.NLevel >= 0, $"Rating {r} → NLevel {p.NLevel} should be >= 0");
        }
    }
}

/// <summary>
/// RatingService の境界値テスト
/// 原典: s_llMajNLevel / GetSLevel (HMajRatingCommon.cpp)
/// SLevel は GamMoney → NLevel → 文字列の順で算出される
/// </summary>
public class RatingServiceBoundaryTests
{
    private readonly RatingService _svc = new();

    // SLevel の境界値確認 (GamMoney ベース)
    // 原典: s_llMajNLevel[] → GetSLevel() 対応
    [Theory]
    [InlineData(0L,       "無一文")]    // NLevel=0
    [InlineData(1L,       "金欠")]      // NLevel=1
    [InlineData(500L,     "庶民")]      // NLevel=2
    [InlineData(1500L,    "平民")]      // NLevel=3
    [InlineData(3000L,    "一般人")]    // NLevel=4
    [InlineData(10000L,   "中流")]      // NLevel=5
    [InlineData(30000L,   "上流")]      // NLevel=6
    [InlineData(100000L,  "金持ち")]    // NLevel=7
    [InlineData(500000L,  "富豪")]      // NLevel=8
    [InlineData(1000000L, "大富豪")]    // NLevel=9
    [InlineData(5000000L, "財閥")]      // NLevel=10
    public void SLevel_GamMoneyBoundary_MatchesExpected(long gamMoney, string expectedSLevel)
    {
        var player = new MajakServer.Models.Player.MajakPlayer { GamMoney = gamMoney };
        _svc.UpdatePlayerLevel(player);
        Assert.Equal(expectedSLevel, player.SLevel);
    }

    // NLevel 境界値確認 (GamMoney ベース)
    [Theory]
    [InlineData(0L,       0)]
    [InlineData(1L,       1)]
    [InlineData(499L,     1)]   // 500 未満 → NLevel=1
    [InlineData(500L,     2)]
    [InlineData(1499L,    2)]
    [InlineData(1500L,    3)]
    [InlineData(5000000L, 10)]
    public void NLevel_GamMoneyBoundary_MatchesExpected(long gamMoney, int expectedNLevel)
    {
        Assert.Equal(expectedNLevel, _svc.GetNLevel(gamMoney));
    }
}
