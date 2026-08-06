namespace MajakServer.Models.Protocol;

/// <summary>
/// MAJ::command* に対応する SignalR メソッド名定数
/// 原典: HMajProtocol.cpp
/// </summary>
public static class Cmd
{
    public const string EnterChannel        = "c1e";
    public const string ForcedLogout        = "forcedLogout";

    // ─── チャンネルコマンド (mjkc*e) ───
    public const string GetDetailRec         = "mjkc1e";
    public const string AutoMatching         = "mjkc2e";
    public const string CancelAutoMatching   = "mjkc3e";
    public const string AutoStart            = "mjkc4e";
    public const string AutoExitRoom         = "mjkc5e";
    public const string AutoEnterRoom        = "mjkc6e";
    public const string ChannelStop          = "mjkc13e";
    public const string GetServerTime        = "mjkc14e";
    public const string AvatarGear           = "mjkc16e";
    public const string MoneyReplenishment   = "mjkc17e";
    public const string ApplyEarnedMoney     = "mjkc18e";
    public const string GetTitle             = "mjkc19e";
    public const string BuyMajItem           = "mjkc20e";
    public const string SelectMajItem        = "mjkc21e";
    public const string GetGem               = "mjkc22e";
    public const string YakumanBonus         = "mjkc23e";
    public const string UseEmoticon          = "mjkc24e";
    public const string RatingRankInfo       = "mjkc25e";
    public const string TournamentList       = "mjkc26e";
    public const string TournamentRegist     = "mjkc27e";
    public const string TournamentJoin       = "mjkc28e";
    public const string TournamentJoinCancel = "mjkc29e";
    public const string TournamentDetail     = "mjkc30e";
    public const string DeliveryMessage      = "mjkc31e";
    public const string GetMissionList       = "mjkc32e";
    public const string RcvWeeklyReward      = "mjkc33e";
    public const string RcvSerialBonus       = "mjkc34e";
    public const string ShopItemRequest      = "mjkc35e";
    public const string ShopItemResponse     = "mjkc36e";   // S→C
    public const string SetCustomItem        = "mjkc37e";
    public const string EquipCustomItem      = "mjkc38e";
    public const string CustomItem           = "mjkc39e";
    public const string CustomItemResponse   = "mjkc40e";   // S→C
    public const string BuyCustomItem        = "mjkc41e";
    public const string BuyCustomItemResponse = "mjkc42e";  // S→C

    /// <summary>
    /// mjkc43e  所持アイテム一覧取得 (GetMajItemList)
    /// 原典: HgMajak2 では theApp.m_UserInfo.m_listMajItem (channel join 時にプッシュ済み) を直接参照。
    /// .NET 移植では SignalR で都度問い合わせる方式とし、ConfirmItemDlg (소지품 화면) で使用する。
    /// </summary>
    public const string GetMajItemList       = "mjkc43e";

    // ─── ゲーム招待 ───────────────────────────────────────────────
    /// <summary>
    /// C→S: ルーム内プレイヤーが同チャンネルの相手を招待
    /// 原典: CHgChannelWnd::SendInviteGameToMember / Cmd_Channel_InviteGameToMemberToS (c22e)
    /// フィールド: targetMemberNo(transport target), G::keyMemberNo, G::keyRoomId, G::keyInviteGameString, G::keyYesNo
    /// S→C 応答: "InviteGame" → G::keyMemberNo, G::keyRoomId, G::keyRoomPwd, G::keyInviteGameString, G::keyYesNo
    /// </summary>
    public const string Invite = "c22e";
    public const string InviteGame = Invite;
    public const string InviteResponse = "c23e";

    // ─── ルーム/ゲームコマンド ───
    public const string SendOkButton   = "smmc1e";
    public const string PushOkButton   = "smmc2e";   // S→C broadcast
    public const string EventInfo      = "smmc3e";
    public const string PaiInfoList    = "smmc4e";
    public const string Tsumikomi      = "smmc5e";
    public const string IpAdapterInfo  = "smmc6e";
    public const string RoomState      = "mjkroom";
    public const string GamePlay       = "playing";
    public const string AgariRec       = "horarec";
    public const string History        = "history";
    public const string ReplayNavi     = "repnavi";
    public const string ReserveChance  = "c55e";

    // ─── G:: フレームワーク由来のチャンネル/ルームコマンド ───────────
    // 原典: G::commandGetRoomList / commandEnterRoom 等 (G::service 系)
    public const string GetRoomList    = "c12e";            // G::commandGetRoomList
    public const string GetMemberList  = "c7e";             // G::commandGetMemberList
    public const string GetRoomMembers = "c16e";            // G::commandMemberList (Web request shim)
    public const string ExitChannel    = "c2e";             // G::commandExitChannel
    public const string ExitRoom       = "c9e";             // G::commandExitRoom
    public const string AlterRoom      = "c61e";            // G::commandAlterRoom
    public const string EnterRoomCmd   = "c14e";            // G::commandEnterRoom
    public const string ViewRoom       = "c18e";            // G::commandViewRoom
    public const string HanChatRelay   = "hc1e";            // HANCHAT::commandHanChatString
    public const string HanChatOneToOne = "hc6e";           // HANCHAT::commandHanChatOneToOneChat
    public const string HanChatOneToOneString = "hc7e";     // HANCHAT::commandHanChatOneToOneChatString
    public const string HanChatOneToOneEnd = "hc8e";        // HANCHAT::commandHanChatOneToOneChatEnd
    public const string MemberList     = "c16e";            // G::commandMemberList (S→C)
    public const string AddMember      = "c5e";             // G::commandAddMember (S→C)
    public const string DeleteMember   = "c6e";             // G::commandDeleteMember (S→C)
    public const string ConnectTypeError = "c34e";          // G::commandConnectTypeError (S→C)
    public const string RoomCreated    = "c8e";             // G::commandCreateRoom
    public const string Complaint      = "c63e";            // G::commandComplaint

    // ─── ゲームロジック内部通知 (S→C) ────────────────────────────────
    public const string GameReport     = "c32e";            // G::commandGameReport

    // ─── トーナメント / カップ 通知 (S→C) ─────────────────────────
    public const string MajAutoMatching = AutoMatching;      // オートマッチング (エイリアス)
    public const string Notice          = "c40e";            // G::commandNotice
}

/// <summary>
/// MAJ::key* に対応するパケットキー定数
/// 原典: HMajProtocol.cpp
/// </summary>
public static class Key
{
    // 戦績統計
    public const string TurnCnt       = "mjkk1e";
    public const string DaidaCnt      = "mjkk2e";
    public const string PointSum      = "mjkk3e";
    public const string KyokuCnt      = "mjkk4e";
    public const string HoraCnt       = "mjkk5e";
    public const string HoraPoint     = "mjkk6e";
    public const string HojuCnt       = "mjkk7e";
    public const string HojuPoint     = "mjkk8e";
    public const string RichiCnt      = "mjkk9e";
    public const string FuroCnt       = "mjkk10e";
    public const string TipPoint      = "mjkk11e";
    public const string TipMatchCnt   = "mjkk12e";
    public const string TobiCnt       = "mjkk13e";
    public const string TobashiCnt    = "mjkk14e";
    public const string DoraCnt       = "mjkk15e";
    public const string UraDoraCnt    = "mjkk16e";
    public const string NukiDoraCnt   = "mjkk17e";
    public const string RichiHoraCnt  = "mjkk18e";

    // その他チャンネルキー
    public const string AiKind        = "mjkk28e";
    public const string ServerTime    = "mjkk32e";
    public const string IsContinue    = "mjkk33e";
    public const string NickName      = "mjkk34e";
    public const string CupPoint      = "mjkk35e";
    public const string Experience    = "mjkk36e";
    public const string MemorialShop  = "mjkk37e";
    public const string ChangBestLevel = "mjkk38e";
    public const string SkinDataCount = "mjkk39e";
    public const string SkinInfo      = "mjkk40e";

    // コイン/オールイン
    public const string LentMoney        = "mjkk41e";
    public const string ReplenishmentType = "mjkk42e";
    public const string RestAllInCnt     = "mjkk43e";
    public const string AllInCnt         = "mjkk44e";
    public const string UseLentMoney     = "mjkk45e";

    // 称号
    public const string TrickTitle     = "mjkk46e";
    public const string MajakTitle     = "mjkk47e";
    public const string TitleType      = "mjkk48e";
    public const string TitleCode      = "mjkk49e";
    public const string TitleName      = "mjkk50e";
    public const string TrickTitleName = "mjkk51e";
    public const string MajakTitleName = "mjkk52e";

    public const string Date           = "mjkk53e";
    public const string RichiEffect    = "mjkk54e";
    public const string GemCount       = "mjkk55e";
    public const string GemGame        = "mjkk56e";
    public const string SellCode       = "mjkk57e";
    public const string ItemCode       = "mjkk58e";
    public const string BuyDate        = "mjkk59e";
    public const string EndDate        = "mjkk60e";
    public const string UseFlag        = "mjkk61e";
    public const string YakuName       = "mjkk62e";
    public const string EmoticonId     = "mjkk63e";
    public const string EmoticonAvatarId = "mjkk64e";

    // グレードモード
    public const string GradeGetPoint    = "mjkk65e";
    public const string GradeCurrPoint   = "mjkk66e";
    public const string GradeNextPoint   = "mjkk67e";
    public const string GradeGetRating   = "mjkk68e";
    public const string GradePrevLevel   = "mjkk69e";
    public const string GradeCurrLevel   = "mjkk70e";
    public const string GradeUpDown      = "mjkk71e";
    public const string GradeBeginner    = "mjkk72e";
    public const string GradeRankId      = "mjkk73e";
    public const string GradeRankDate    = "mjkk74e";
    public const string GradeRankRefresh = "mjkk75e";
    public const string GradeRankList    = "mjkk76e";
    public const string GradeRankCnt     = "mjkk77e";
    public const string GradeRankSelf    = "mjkk78e";
    public const string GradeSelectList  = "mjkk79e";
    public const string GradeSelectCnt   = "mjkk80e";
    public const string GradeExtraStage  = "mjkk81e";

    // トーナメント
    public const string TournamentList           = "mjkk82e";
    public const string TournamentCnt            = "mjkk83e";
    public const string TournamentBaseRule       = "mjkk84e";
    public const string TournamentMoneyRule      = "mjkk85e";
    public const string TournamentName           = "mjkk86e";
    public const string TournamentDate           = "mjkk87e";
    public const string TournamentNo             = "mjkk88e";
    public const string TournamentDetail         = "mjkk89e";
    public const string TournamentDetailCnt      = "mjkk90e";
    public const string TournamentJoinChk        = "mjkk91e";
    public const string DeliveryMessage          = "mjkk92e";
    public const string TournamentRegistDayTime  = "mjkk93e";
    public const string TournamentRegistFlag     = "mjkk94e";
    public const string FailCode                 = "mjkk95e";
    public const string FailCodeCnt              = "mjkk96e";
    public const string TournamentTotalReport    = "mjkk97e";
    public const string TournamentTotalReportCnt = "mjkk98e";
    public const string TournamentSubId          = "mjkk99e";
    public const string RoomForceExitReason      = "mjkk100e";
    public const string TournamentChkRoomMember  = "mjkk101e";
    public const string TournamentRoomId         = "mjkk102e";
    public const string TournamentRoomOrder      = "mjkk103e";
    public const string GradePlayCheck           = "mjkk104e";

    // ミッション
    public const string PointDayOwn   = "mjkk105e";
    public const string PointDayMax   = "mjkk106e";
    public const string PointWeekOwn  = "mjkk107e";
    public const string PointWeekMax  = "mjkk108e";
    public const string DailyMission1 = "mjkk109e";
    public const string DailyMission2 = "mjkk110e";
    public const string DailyMission3 = "mjkk111e";
    public const string DailyMission4 = "mjkk112e";
    public const string DailyMission5 = "mjkk113e";
    public const string DailyMission6 = "mjkk114e";
    public const string DailyMission7 = "mjkk115e";
    public const string DailyMission8 = "mjkk116e";
    public const string DailyMission9 = "mjkk117e";
    public const string DailyMission10 = "mjkk118e";
    public const string DailyMission11 = "mjkk119e";
    public const string WeeklyReward1 = "mjkk120e";
    public const string WeeklyReward2 = "mjkk121e";
    public const string WeeklyReward3 = "mjkk122e";
    public const string WeeklyReward4 = "mjkk123e";
    public const string WeeklyReward5 = "mjkk124e";
    public const string WeeklyReward6 = "mjkk125e";
    public const string WeeklyReward7 = "mjkk126e";
    public const string WeeklyReward8 = "mjkk127e";
    public const string WeeklyRewardId = "mjkk128e";
    public const string SerialCode    = "mjkk130e";
    public const string CircleId      = "mjkk131e";
    public const string CircleIdCnt   = "mjkk132e";
    public const string CircleName    = "mjkk133e";

    // カスタムアイテム
    public const string CustomBoard       = "mjkk134e";
    public const string CustomHai         = "mjkk135e";
    public const string CustomCostume     = "mjkk136e";
    public const string CustomCostumeType = "mjkk137e";
    public const string CustomId          = "mjkk138e";
    public const string ShopNo            = "mjkk139e";
    public const string ItemQuantity      = "mjkk140e";

    // ルームキー (smmk)
    public const string RoomCharge   = "smmk1e";
    public const string OkButton     = "smmk2e";
    public const string LackMoney    = "smmk3e";
    public const string WinMoneyCut  = "smmk4e";
    public const string Score        = "smmk5e";
    public const string Point        = "smmk6e";
    public const string Yakitori     = "smmk7e";
    public const string Chip         = "smmk8e";
    public const string Gateway      = "smmk9e";
    public const string MacAddr      = "smmk10e";
    public const string FeeWinner    = "smmk11e";

    // イベントポイント (smmk)
    public const string GetEventPoint  = "smmk11e";
    public const string CutEventPoint  = "smmk12e";
    public const string TodayEventPoint = "smmk13e";
    public const string TotalEventPoint = "smmk14e";
    public const string EventNo        = "smmk17e";
    public const string EventCode      = "smmk18e";
}

/// <summary>
/// G:: フレームワークキー定数 (MeProtocol.cpp 対応)
/// 原典: G::key* / G::value*
/// </summary>
public static class GKey
{
    public const string Result           = "k1e";    // G::keyResult
    public const string Message          = "k2e";    // G::keyMessage
    public const string Pix         = "k3e";    // G::keyMemberNo
    public const string AvatarId         = "k7e";    // G::keyAvatarId
    public const string Name             = "k8e";    // G::keyName
    public const string Age              = "k10e";   // G::keyAge
    public const string Sex              = "k11e";   // G::keySex
    public const string Location         = "k12e";   // G::keyLocation
    public const string TotMoney         = "k13e";   // G::keyTotMoney
    public const string MoneyRanking     = "k14e";   // G::keyMoneyRanking
    public const string GameId           = "k22e";   // G::keyGameId
    public const string SubId            = "k23e";   // G::keySubId
    public const string ChannelId        = "k24e";   // G::keyChannelId
    public const string Count            = "k25e";   // G::keyCount
    public const string MatchCnt         = "k26e";   // G::keyMatchCnt
    public const string WinCnt           = "k27e";   // G::keyWinCnt
    public const string DefeatCnt        = "k28e";   // G::keyDefeatCnt
    public const string DrawCnt          = "k29e";   // G::keyDrawCnt
    public const string DisconnCnt       = "k30e";   // G::keyDisconnCnt
    public const string Rating           = "k31e";   // G::keyRating
    public const string SLevel           = "k32e";   // G::keySLevel
    public const string NLevel           = "k33e";   // G::keyNLevel
    public const string GamMoney         = "k34e";   // G::keyGamMoney
    public const string GamRanking       = "k35e";   // G::keyGamRanking
    public const string LastGDate        = "k37e";   // G::keyLastGDate
    public const string LastDisconn      = "k38e";   // G::keyLastDisconn
    public const string Target           = "k38e";   // G::keyTarget (legacy alias)
    public const string Color            = "k40e";   // G::keyColor
    public const string String           = "k41e";   // G::keyString
    public const string Password         = "k6e";    // G::keyPassword
    public const string OpPix       = "k4e";    // G::keyOpMemberNo
    public const string ViewerId         = "k5e";    // G::keyViewerId
    public const string RoomId           = "k42e";   // G::keyRoomId
    public const string MemberPos        = "k43e";   // G::keyMemberPos
    public const string ViewerPos        = "k44e";   // G::keyViewerPos
    public const string RoomTitle        = "k45e";   // G::keyRoomTitle
    public const string RoomOption        = "k46e";   // G::keyRoomOption
    public const string RoomStateKey     = "k47e";   // G::keyRoomState
    public const string MemberCnt        = "k48e";   // G::keyMemberCnt
    public const string ViewerCnt        = "k49e";   // G::keyViewerCnt
    public const string RoomHost         = "k50e";   // G::keyRoomHost
    public const string RoomCount        = "k51e";   // G::keyRoomCount
    public const string IPAddress        = "k52e";   // G::keyIPAddress
    public const string Port             = "k53e";   // G::keyPort
    public const string RoomLimitCnt     = "k66e";   // G::keyRoomLimitCnt
    public const string RoomPwd          = "k67e";   // G::keyRoomPwd
    public const string PrivateYn        = "k68e";   // G::keyPrivateYN
    public const string YesNo            = "k64e";   // G::keyYesNo
    public const string ReportingType    = "k81e";   // G::keyReportingType
    public const string InviteGameString = "k65e";   // G::keyInviteGameString
    public const string ConnectFor       = "k82e";   // G::keyConnectFor
    public const string FailCode         = "failcode"; // G::keyFailCode
    public const string MaxViewer        = "k69e";   // G::keyMaxViewer
    public const string PlayerType       = "k57e";   // G::keyPlayerType
    public const string PlayerPos        = "k58e";   // G::keyPlayerPos
    public const string Description      = "k63e";   // G::keyDescription
    public const string NoticeLevel      = "k83e";   // G::keyNoticeLevel
    public const string NoticeSec        = "k86e";   // G::keyNoticeSec
    public const string ReservedString   = "k102e";  // G::keyReservedString
    public const string PreBanishing     = "k103e";  // G::keyPreBanishing
    public const string ReserveBanishing = "k104e";  // G::keyReserveBanishing
    public const string RoomCreator      = "k116e";  // G::keyRoomCreator
    public const string RoomMinCnt       = "k127e";  // G::keyRoomMinCnt
    public const string NoticeType       = "k139e";  // G::keyNoticeType
    public const string UsedChance       = "k122e";  // G::keyUsedChance
    public const string ReserveChance    = "k118e";  // G::keyReserveChance
    public const string IsForDisconn     = "k124e";  // G::keyIsForDisconn
    public const string DayCnt           = "k131e";  // G::keyDayCnt
    public const string RoomPlaying      = "k143e";  // G::keyRoomPlaying
    public const string RecordInfo       = "k144e";  // G::keyRecordInfo
    public const string RecordKind       = "k145e";  // G::keyRecordKind
    public const string ExScoreCnt       = "k151e";  // G::keyExScoreCnt
    public const string ExScoreValue     = "k153e";  // G::keyExScoreValue
    public const string TotalMember      = "k154e";  // G::keyTotalMember
    public const string OpMemberCnt      = "k188e";  // G::keyOpMemberCnt
    public const string OpMemberPos      = "k189e";  // G::keyOpMemberPos
    public const string DispRange        = "k448e";  // G::keyDispRange

    // valuePlayer / valueViewer
    public const string ValuePlayer      = "v4e";
    public const string ValueViewer      = "v5e";
    public const string ValueSuccess     = "v1e";
    public const string ValueFailure     = "v2e";
    public const string ValueAll         = "v3e";
    public const string ValueReportingGamble = "v12e";
    public const string ValueConnectForCreate = "v14e";
    public const string ValueConnectForGameJoin = "v16e";
    public const string ValueConnectForView = "v17e";
    public const string ValueNoticeChannel = "v23e";
    public const string ValueNoticeLevelNormal = "v25e";
    public const string ValueDummy = "v6e";
    public const string ValueYes   = "v7e";
    public const string ValueNo    = "v8e";
}

/// <summary>
/// channel:notice payload builder.
/// 原典: HMajChnlServer::SendNoticeToAll
/// </summary>
public static class NoticePayload
{
    public static IReadOnlyDictionary<string, object> Channel(string message, string channelId, int color = 0)
        => new Dictionary<string, object>
        {
            ["message"]        = message,
            ["text"]           = message,
            [GKey.String]      = message,
            [GKey.NoticeType]  = GKey.ValueNoticeChannel,
            [GKey.ChannelId]   = channelId,
            [GKey.NoticeLevel] = GKey.ValueNoticeLevelNormal,
            [GKey.NoticeSec]   = 10,
            [GKey.Count]       = 1,
            [GKey.Color]       = color,
        };
}

/// <summary>
/// 原典エラーコード。
/// HMajRoomServer::EnterRoom / AutoEnterRoom の commandConnectTypeError で使用される。
/// </summary>
public static class LegacyErrorCode
{
    public const int InvalidPassword        = 10005;
    public const int CannotEnterRoom        = 10009;
    public const int ServerBusy             = 10011;
    public const int InvalidPacket          = 10019;
    public const int NotEmptyRoom           = 10021;
    public const int SameUserAlreadyIn      = 10022;
    public const int NotMatchSocketId       = 10027;
    public const int TournamentViewRoom     = 10027;
    public const int MajAutoEnterRoomFailed = 30001;
    public const int MajNotEntryCircle      = 30011;
}

/// <summary>
/// room:connect_error payload builder.
/// 原典: HMajRoomServer::EnterRoom / AutoEnterRoom の G::commandConnectTypeError
/// </summary>
public static class RoomConnectErrorPayload
{
    public static IReadOnlyDictionary<string, object?> Build(int roomId, string message, int failCode)
    {
        var packet = new Dictionary<string, object?>
        {
            [GKey.RoomId] = roomId,
            [GKey.Message] = message,
            [GKey.FailCode] = failCode,
            ["roomId"] = roomId,
            ["message"] = message,
            ["failCode"] = failCode,
        };

        return packet;
    }
}

/// <summary>
/// commandMajAutoExitRoom payload builder.
/// 原典: HMajRoomServer.cpp ForceExitTimeoutRooms / ForceExitStartErrorRooms.
/// </summary>
public static class RoomForceExitPayload
{
    public static IReadOnlyDictionary<string, object> Build(
        string playerType,
        int playerPos,
        string memberNo,
        string name,
        int reason)
        => new Dictionary<string, object>
        {
            [GKey.RoomHost]           = "",
            [GKey.PlayerType]         = playerType,
            [GKey.PlayerPos]          = playerPos,
            [GKey.Pix]           = memberNo,
            [GKey.Name]               = name,
            [Key.RoomForceExitReason] = reason,
        };
}

/// <summary>
/// MAJ::value* 定数
/// </summary>
public static class Val
{
    public const int  Player         = 1;
    public const int  Viewer         = 2;
    public const string RankDuring   = "mjkv1e";
    public const string RankNoData   = "mjkv2e";

    // BuyCustomItem 結果コード
    public const int CustomSuccess  = 0;
    public const int CustomCoinless = 1;
    public const int CustomOwned    = 2;
    public const int CustomIdError  = 11;
    public const int CustomDbError  = 12;
    public const int CustomError    = 13;
}
