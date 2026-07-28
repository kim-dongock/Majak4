namespace MajakServer.Models.Protocol;

/// <summary>
/// ゲーム整数定数 — 原典: HMajDef.h / MajakDef.h
/// </summary>
public static class GameConst
{
    public const string ServiceId       = "MAJAK4";
    public const int    PlayerMaxCount  = 4;
    public const int    MemberNoLen     = 24;
    public const int    TitleIdLen      = 10;
    public const int    TitleNameLen    = 30;
    public const int    CupNickNameLen  = 40;
    public const int    HistPlayerCnt   = 4;

    // コイン
    public const long DefaultMoney        = 1000L;
    public const long DefaultMoneyNetCafe = 2000L;
    public const long AllinMoney          = 1000L;
    public const long AllinMoney2Dan      = 2000L;
    public const long YakumanBonusMoney   = 200L;

    // オールイン
    public const int AllinCountMax       = 1;
    public const int AllinCountMaxNetCafe = 2;

    // グレードモード
    public const int Grade2Dan           = 11;
    public const int RatingGradeModeInit  = 1500;
    public const int RatingRankAll        = 99;
    public const int RatingRankBeginner   = 98;
    public const int RatingRankMaxCnt     = 1000;

    // 称号 ID プレフィックス
    public const string RatingTitleGradeHead = "mjkt5";
    public const string RatingTitle10Kyu     = "mjkt500";
    public const string RatingTitleFormat    = "mjkt{0:D3}";

    // カスタムアイテムデフォルト値 (MajakDef.h)
    public const int CustomBoardDefault   = 100000;
    public const int CustomHaiDefault     = 100003;
    public const int CustomCostumeDefault = 100011;
    public const int CustomEquipMax       = 10;

    // イベントコード (HMajDef.h GH_EVTCODE_*)
    public const string EvtCodeDefaultMoney    = "JM00068";
    public const string EvtCodeRoomCharge      = "JM00069";
    public const string EvtCodeFreeMoney       = "JM00070";
    public const string EvtCodeCollectInsurance = "JM00100";
    public const string EvtCodeProvideInsurance = "JM00101";
    public const string EvtCodeDragonGem       = "JM00119";
    public const string EvtCodeYakumanBonus    = "JM00171";
    public const string EvtCodeGradeBeginnerPresent = "JM00206";
    public const string EvtCodeGeneralCode     = "JM00184";
    public const string EvtCodeTournamentPlan        = "JM00213";
    public const string EvtCodeTournamentResultPlan  = "JM00214";
    public const string EvtCodeTournamentResultGrade = "JM00215";
    public const string EvtCodeTournamentJoin        = "JM00216";
    public const string EvtCodeTournamentJoinCancel  = "JM00217";
    public const string EvtCodeTournamentRejectPlan  = "JM00218";
    public const string EvtCodeTournamentRejectJoin  = "JM00219";
    public const string EvtCodeTournamentStopPlan    = "JM00220";
    public const string EvtCodeTournamentStopJoin    = "JM00221";
    public const string EvtCodeLoginGiftMoney        = "JM00276";

    // 汎用ログインギフト (HMajDef.h EVTCODE_GIFT_EVENT_GENERAL / RECV_GIFTCODE_*)
    public const string LoginGiftEventCode = "6262";
    public const int    LoginGiftEventNo   = 0;
    public const int    LoginGiftCodeCoin  = 1;

    // PlayPark mission bridge (HMajDef.h / HMajCommon.h)
    public const int PlayParkMissionTypeDay  = 0;
    public const int PlayParkMissionTypeAttr = 1;
    public const int PlayParkMissionNo       = 1;
    public const int PlayParkProcTypeAdd     = 1;
    public const int PlayParkAttrMissionMax  = 1000;

    // 2014.12 Hangame CMS mission event
    public const string MissionEventCmsCode = "5629";
    public const int    MissionEventCmsNo   = 1;
    public static readonly DateTime MissionEventCmsEndTime = new(2014, 12, 26, 11, 0, 0);

    public const string GradeModeProSaveEventCode = "5422";

    // グレードレーティング計算定数 (HMajDef.h)
    public const int    RatingCarcMatchCount          = 400;
    public const double RatingCarcScale               = 0.5;
    public const double RatingCarcCorrectBase         = 20.0;
    public const double RatingCarcPlayNumCorrectHigh  = 0.2;
    public const double RatingCarcPlayNumCorrectLow   = 0.002;
}
