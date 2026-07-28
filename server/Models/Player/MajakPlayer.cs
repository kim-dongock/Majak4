using MajakServer.Models.Protocol;

namespace MajakServer.Models.Player;

/// <summary>
/// 接続中のプレイヤーセッション状態 — 原典: HMajPlayer.h
/// SignalR ConnectionId と 1:1 マッピング。メモリ専用。
/// </summary>
public class MajakPlayer
{
    // ─── 識別 ───
    public string ConnectionId { get; set; } = "";
    public string MemberNo     { get; set; } = "";
    public string Pix          { get; set; } = "";
    public string NickName     { get; set; } = "";
    public string AvatarId     { get; set; } = "";
    public string Sex          { get; set; } = "";
    public string ChannelId    { get; set; } = "";
    public int?   RoomId       { get; set; }

    /// <summary>
    /// Hangame ログインクッキーから復号したパスワード。
    /// メモリ上のみ保持、シリアライズ/ログ出力禁止。クライアントに返さないこと。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Password     { get; set; } = "";

    // ─── 共通レーティング (MJKCOMMONRAT) ───
    public long   GamMoney       { get; set; } = GameConst.DefaultMoney;
    public long   GamMoneyU      { get; set; }   // 未確定コイン
    public long   EarnedMoney    { get; set; }
    public int    Rating         { get; set; }
    public string SLevel         { get; set; } = "";
    public int    NLevel         { get; set; }
    public int    Experience     { get; set; }
    public int    AllinCnt       { get; set; }
    public DateTime? LastAllinDt { get; set; }
    public int    GemCount       { get; set; }
    public int    MemorialShop   { get; set; }   // ビットマスク: bit0=役満, bit1=リーチ一発ツモ
    public string TrickTitle     { get; set; } = "";
    public string MajakTitle     { get; set; } = "";
    public string LastGameDate   { get; set; } = "";

    // ─── チャンネル種別レーティングレコード ───
    public RatingRecord RegularRecord  { get; set; } = new();   // MJKHANGERAT
    public RatingRecord HiClassRecord  { get; set; } = new();   // MJK_HICLASSRAT
    public RatingRecord GradeRecord    { get; set; } = new();   // MJK_GRADERAT
    public RatingRecord CompeteRecord  { get; set; } = new();   // MJKCOMPETERAT

    /// <summary>現在のチャンネル種別に応じたアクティブレコードを指すポインター役</summary>
    public RatingRecord ActiveRecord   { get; set; } = new();

    // ─── カップ戦 ───
    public CupRecord    CupRec       { get; set; } = new();
    public CupEvtRecord CupEvtRec    { get; set; } = new();   // MJK_EVTRAT (CUP_JTID_GAME_SUM)
    public int          CupPointGain { get; set; }            // 今回対局で獲得した CUPPOINT
    public string       NickNameCup  { get; set; } = "";

    // ─── PlayPark mission bridge ───
    public DateTime? PlayParkDailyMissionAt { get; set; }
    public int       PlayParkAttrMission    { get; set; }

    // ─── 2014.12 CMS mission event ───
    public DateTime? MissionEventCmsClearAt { get; set; }

    // ─── サークル ───
    /// <summary>
    /// このプレイヤーが所属するサークル一覧 — 原典: HMajPlayer::m_mapCircleInfo
    /// キー = CircleId, 値 = CircleName。
    /// CIRCLEMAST × CIRCLEMEMBERINFO JOIN で取得。
    /// サークルチャンネルのルーム入室時に CheckCircleLimit で参照される。
    /// </summary>
    public Dictionary<string, string> CircleInfo { get; set; } = new();

    // ─── スキン ───
    public List<SkinInfo> SkinList { get; set; } = new();

    // ─── カスタムアイテム (key=CustomId) ───
    public Dictionary<int, UserCustomItem> CustomItems { get; set; } = new();

    // ─── マジャクアイテム (MJK_ITEMLIST) ───
    // 原典: HMajPlayer::m_mapMajItem — アイテムコード → MJITEMINFO マップ
    // MajakGameHub.OnConnectedAsync / GetItemInfo 時に更新する
    public List<MajakServer.Repositories.MySQL.MajItemInfo> MajItems { get; set; } = new();

    // ─── 称号 ───
    public int[]    TitleClear  { get; set; } = new int[32];  // index 0 未使用
    public int[]    TrickLevel  { get; set; } = new int[5];   // N/F/W/E/A トリックレベル
    public int[]    YakuCount   { get; set; } = new int[28];  // 0-27
    public int[]    YmanCount   { get; set; } = new int[15];  // 100-114
    public List<string> GradeTitleList { get; set; } = new();

    // ─── ゲーム内一時状態 ───
    public int    TrickTitleId  { get; set; }
    public int    MajakTitleId  { get; set; }   // 0-999=mjkt, 1000+= mjkc
    public long   RoomCharge    { get; set; }
    public string UsedBadaiFreeItem { get; set; } = "";
    public bool   ReserveChanceItem { get; set; }
    public int    HoraDoraMax   { get; set; }
    public long   FeeWinner     { get; set; }
    public bool   IsOutPlayer   { get; set; }
    public bool   IsViewer      { get; set; }   // 観戦者フラグ
    public int    DispRange     { get; set; }   // 表示範囲設定 (0=全体公開)
    public uint   SeatPos       { get; set; }
    public int    EngineOrder   { get; set; } = GameConst.PlayerMaxCount;
    public int    ContWinDefeat { get; set; }
    /// <summary>
    /// 直前のチャット送信時刻 — 0.5 秒制限のため
    /// 原典: GChildSocket::m_clChatTime (HMajChnlServer.cpp:3801-3815)
    /// </summary>
    public DateTime LastChatTime { get; set; } = DateTime.MinValue;
    public int    H_ContTopMax  { get; set; }
    public int    H_ContTopNow  { get; set; }
    /// <summary>
    /// 直前の対局で一緒だったプレイヤーの MemberNo (再マッチング回避用)
    /// 原典: HMajPlayer::m_szPreMatchMemberNo[3]
    /// AutoEnterRoom 時に対局相手全員の ID を記録する。
    /// </summary>
    public string[] PreMatchMemberNos { get; set; } = Array.Empty<string>();

    // ─── ネットワーク ───
    public string IpAddress     { get; set; } = "";
    public string Gateway       { get; set; } = "";
    public string MacAddr       { get; set; } = "";
    public bool   IsNetCafeIp   { get; set; }
    public bool   IsGuestId     { get; set; }
    public bool   IsAdminId     { get; set; }   // HMajAdminIdInfo::ADCMD_IsAdminId()

    // ─── チャンネル設定 ───
    /// <summary>招待拒否フラグ — 原典: IDC_CK_REJECTINVITE / m_btnDenyInvGame</summary>
    public bool   RejectInvite  { get; set; }

    /// <summary>プロプレイヤーフラグ — 原典: HMajPlayer::m_bMjkProIDFlg (IsMjkProMemberNo)</summary>
    public bool   IsPro         { get; set; }
    public string ProPictureUrl { get; set; } = "";

    /// <summary>カスタムアイテム種別 (Kind) で装備中の CustomId を返す</summary>
    public int GetCustomEquip(int kind)
    {
        var item = CustomItems.FirstOrDefault(kv => kv.Value.Equip == 1 && IsSameCustomEquipGroup(kind, kv.Value.Kind));
        return item.Key;
    }

    private static bool IsSameCustomEquipGroup(int requestedKind, int itemKind)
        => requestedKind switch
        {
            10 or 11 or 12 => itemKind is 10 or 11 or 12,
            30 or 31 or 32 => itemKind is 30 or 31 or 32,
            _              => itemKind == requestedKind,
        };

    /// <summary>
    /// リーチ演出アイテムの subCode を返す。
    /// 原典: HMajPlayer::GetRichiEffect() — m_mapMajItem から CAT_RICHI カテゴリの
    ///   UseFlag=true なアイテムを探し、そのサブコードを返す。なければ 0。
    ///
    /// MJK_ITEMLIST の ITEMCODE は "item001" 〜 "item004" (CAT_RICHI 相当)。
    /// UseFlag=true = 使用中のアイテム。
    /// </summary>
    public int GetRichiEffect()
    {
        // MJK_ITEMLIST で USEFLG='Y' かつ ITEMCODE が "item00x" 形式 (CAT_RICHI)
        // 原典: it->second.pItemMast->nSubCode — サブコード (1=普通リーチ, 2=重リーチ, 3=一点リーチ)
        // .NET ではアイテムコードで判定する (item001=1, item002=2, item004=3)
        if (!MajItems.Any()) return 0;
        var richiItem = MajItems.FirstOrDefault(i =>
            i.UseFlag &&
            i.ItemCode.StartsWith("item") &&
            i.ItemCode is "item001" or "item002" or "item004");
        if (richiItem == null) return 0;
        return richiItem.ItemCode switch
        {
            "item001" => 1,
            "item002" => 2,
            "item004" => 3,
            _         => 0,
        };
    }
}
