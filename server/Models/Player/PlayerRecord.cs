namespace MajakServer.Models.Player;

/// <summary>
/// レーティングチャンネル別戦績レコード — 原典: MajakDef.h HMAJ_RATING_RECORD struct
/// MJKHANGERAT / MJKCOMPETERAT / MJK_HICLASSRAT / MJK_GRADERAT 共通構造
/// </summary>
public class RatingRecord
{
    public int Rating      { get; set; }
    public int MatchCnt    { get; set; }
    public int WinCnt      { get; set; }
    public int DefeatCnt   { get; set; }
    public int DrawCnt     { get; set; }

    public int Grade1      { get; set; }  // 1位回数
    public int Grade2      { get; set; }
    public int Grade3      { get; set; }
    public int Grade4      { get; set; }

    public int TurnCnt     { get; set; }
    public int DaidaCnt    { get; set; }
    public int PointSum    { get; set; }
    public int KyokuCnt    { get; set; }
    public int HoraCnt     { get; set; }
    public int HoraPoint   { get; set; }
    public int HojuCnt     { get; set; }
    public int HojuPoint   { get; set; }
    public int RichiCnt    { get; set; }
    public int FuroCnt     { get; set; }

    // 通常/ハイクラス追加統計
    public int TipPoint    { get; set; }
    public int TipMatchCnt { get; set; }
    public int TobiCnt     { get; set; }
    public int TobashiCnt  { get; set; }
    public int DoraCnt     { get; set; }
    public int UraDoraCnt  { get; set; }
    public int RichiHoraCnt { get; set; }

    // グレードモード専用
    public int Grade       { get; set; }  // 現在の段位レベル
    public int GradePoint  { get; set; }
    public int TotExtraCount { get; set; }
    public DateTime? LastExtraDate { get; set; }

    public int DisconnCnt  { get; set; }
    public DateTime? LastDisconn { get; set; }
    public string ChannelId { get; set; } = "";
}

/// <summary>
/// カップ戦レコード — MAJAKCUPRAT (基本カップポイント)
/// </summary>
public class CupRecord
{
    public int CupPoint    { get; set; }
    public int CupMatchCnt { get; set; }
}

/// <summary>
/// カップイベントスコア — MJK_EVTRAT
/// 原典: HMajPlayer::m_stEventInfo (EVENTINFO_ST)
/// CUP_JTID_GAME_SUM 採点方式 (SUM_MAX/SUM_MIX/SUM_SUC) で使用する
///   TotalPoint : 集計済み合計スコア
///   MatchCnt   : 対局回数
///   Points[]   : 各対局のスコア履歴 (最大7件)
/// </summary>
public class CupEvtRecord
{
    public int   TotalPoint { get; set; }
    public int   MatchCnt   { get; set; }
    public int[] Points     { get; set; } = new int[7];
    public int   EntryTitle { get; set; }
    public bool  BuyItem    { get; set; }
}

/// <summary>
/// スキン所持情報 — MJKUSERSKINLIST
/// </summary>
public class SkinInfo
{
    public int      SkinNo      { get; set; }
    public bool     AttachFlag  { get; set; }
    public DateTime EndDate     { get; set; }
}

/// <summary>
/// カスタムアイテム所持情報 — MJK_USERCUSTOMITEM
/// </summary>
public class UserCustomItem
{
    public int Kind   { get; set; }  // 種別 (背景板/牌/コスチュームなど)
    public int Equip  { get; set; }  // 1=装備中
}

/// <summary>
/// カスタムショップ商品情報 — MJK_CUSTOMSHOPMAST 1レコード
/// 原典: server/legacy/server/HMajDef.h CUSTOMSHOPITEM
/// </summary>
public class CustomShopItemInfo
{
    public int      ShopNo      { get; set; }
    public string   ShopName    { get; set; } = "";
    public int      Price       { get; set; }
    public int      CustomId    { get; set; }
    public string   Description { get; set; } = "";
    public long     GameMoney   { get; set; }
    public DateTime SalesDt     { get; set; }
    public DateTime LimitDt     { get; set; }
}
