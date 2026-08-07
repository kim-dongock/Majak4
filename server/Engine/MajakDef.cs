namespace MajakServer.Engine;

// ─────────────────────────────────────────────────────────────────────────────
// Constants
// ─────────────────────────────────────────────────────────────────────────────
public static class MajakConst
{
    public const int PlayerMaxCount  = 4;
    public const int BipaiMaxCount   = 136;
    public const int WanpaiCount     = 14;
    public const int TehaiCount      = 13;
    public const int InvalidOrder    = -1;
    public const int DoraMaxCount    = 5;
    public const int DiceCount       = 2;
    public const int KaeshiPoint     = 30000;
    public const int DefaultGamePoint = 25000;
    public const int DefaultTip      = 20;
}

// ─────────────────────────────────────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Action codes (ACT enum)</summary>
public enum Act
{
    Inv = 0, Pas, Chi, Pon, Kan, Ron, Tap, Ank, Cha, Ric, Tao, Tsu, Hua,
    // internal
    Shu, Kou, Lbu,
}

/// <summary>Yaku codes (HORAYAKU enum). Yakuman starts at 100.</summary>
public enum HoraYaku
{
    Haitei         = 0,
    Houtei         = 1,
    Rinshan        = 2,
    Tsumo          = 3,
    Richi          = 4,
    Ippatsu        = 5,
    Yakuhai        = 6,
    Pinfu          = 7,
    Tanyao         = 8,
    Iipeikou       = 9,
    Chitoitsu      = 10,
    Ittsuu         = 11,
    Toitoi         = 12,
    Sanshokudoujun = 13,
    Isosanjun      = 14,
    Sanshokudoukou = 15,
    Chankan        = 16,
    Sanankou       = 17,
    Sankantsu      = 18,
    Shosangen      = 19,
    Honroutou      = 20,
    Chanta         = 21,
    Junchan        = 22,
    Ryanpeikou     = 23,
    Honisou        = 24,
    Chinisou       = 25,
    Wrichi         = 26,
    Dora           = 27,
    // Yakuman
    Daisangen      = 100,
    Suuankou       = 101,
    Suukantsu      = 102,
    Shosuushi      = 103,
    Chinroutou     = 104,
    Tsuisou        = 105,
    Ryuisou        = 106,
    Churenpaotou   = 107,
    Kokushi        = 108,
    Tenhou         = 109,
    Chihou         = 110,
    Suuankou2      = 111,
    Daisuushi      = 112,
    Kokushi2       = 113,
    Churenpaotou2  = 114,
}

public enum KyokuEnd
{
    None, Hora, Taopai, Sanchaho, Hoanpai, Sukaikan, Sucharichi, Sufontsurenta, Nagashimangan,
}

public enum KyoResultPin
{
    Ron = 0,
    Tsumo = 1,
    None = 2,
    Taopai = 3,
    Sanchaho = 4,
    Hoanpai = 5,
    Sukaikan = 6,
    Sucharichi = 7,
    Sufontsurenta = 8,
    Nagashimangan = 9,
}

public sealed class KyoResultSnapshot
{
    public KyoResultPin Pin { get; set; } = KyoResultPin.None;
    public int HojuOrder { get; set; } = MajakConst.InvalidOrder;
    public bool[] Hora { get; } = new bool[4];
    public int[] TenBal { get; } = new int[4];
    public int[] PaoBal { get; } = new int[4];
    public int[] WarBal { get; } = new int[4];
    public int[] RibBal { get; } = new int[4];
    public int[] RenBal { get; } = new int[4];
    public int[] TipBal { get; } = new int[4];

    public void Clear(KyoResultPin pin = KyoResultPin.None)
    {
        Pin = pin;
        HojuOrder = MajakConst.InvalidOrder;
        Array.Clear(Hora);
        Array.Clear(TenBal);
        Array.Clear(PaoBal);
        Array.Clear(WarBal);
        Array.Clear(RibBal);
        Array.Clear(RenBal);
        Array.Clear(TipBal);
    }
}

public enum GameEnd { None, Set, Stop, Tobi, Hora, }

public enum PlayerMode  { None, Turn, Furo, Chan, Kyo, Aga, }
public enum RichiType   { None = 0, Richi, Wrichi, }
public enum GameStatus  { NotPlaying, Playing, NewKyoku, EndKyoku, }

/// <summary>Return codes from ProcessAction (LRET enum)</summary>
public enum ActionResult
{
    Ok,
    ErrAssert, ErrInvalidOrder, ErrInvalidMode, ErrInvalidBipaiIndex,
    ErrPaiNotFoundInHand, ErrPaiAlreadyUsed, ErrAfterFuro,
    ErrCannotHora, ErrToolate, ErrKanAfter4Kan, ErrHuapai,
    ErrPaiNotMatch, ErrAfterRichi, ErrSelf, ErrNotHoraForm,
    ErrFuriten, ErrNoYaku, ErrNotMenzen, ErrPointNotEnough,
    ErrNotTempai, ErrAnkanAfterRichi, ErrInvalidPaiCount,
    ErrNotNextOrder, ErrInvalidAction, ErrKuikae,
}

// ─────────────────────────────────────────────────────────────────────────────
// Data Structures
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Game rule configuration (RULEINFO)</summary>
public struct RuleInfo
{
    public bool Yakitori;
    public bool Kuitan;
    public bool Tip;
    public bool Hanchan;     // true = hanchan, false = tonpuusen
    public bool Nagashi;
    public int  Contest;     // 0=normal, 1=grade/contest
    public int  Ron;         // 0=double ron OK, 1=atamahane, 2=always double ron
    public int  Uma;         // 0..3 index into uma table
    public bool Wareme;
    public int  AkaDora;     // number of red dora (0/1/2)
    public bool GradeGame;   // _RATING_GRADE_MODE: 段位戦は bRevaluate=false (ダブル役満不適用)
}

/// <summary>Hanchan-level state (HANCHANINFO)</summary>
public struct HanchanInfo
{
    public int   Chicha;
    public int   CurKyoku;
    public int[] Player;     // [4] seat→room-position mapping
    public int   RenchanCount;
}

/// <summary>Per-kyoku state (KYOKUINFO)</summary>
public struct KyokuInfo
{
    public int[]      Dice;       // [2]
    public int        KanCount;
    public PaiCode[]  Dora;       // [5]
    public PaiCode[]  UraDora;    // [5]
    public int        RibouCount;
    public int        OyaOrder;
    public bool       EndKyokuWithHora;
    public bool       Renchan;
    /// <summary>
    /// トーナメント強制終了フラグ — 原典: _TOURNAMENT_MODE bCutGame
    /// 外部から SetCutGame() でセットされ、局終了後の ProcessModeKyo のパス時にゲームを強制終了する。
    /// </summary>
    public bool       CutGame;
}

/// <summary>Packed tile-info sent to clients (BIPAIINFO)</summary>
public struct BipaiInfo
{
    public int        PaiCnt;
    public PaiCode[]  Pai;        // [136]

    public static BipaiInfo Create() => new() { PaiCnt = 0, Pai = new PaiCode[136] };
}

/// <summary>Per-match statistics tracked by the engine (HMAJ_RATING_RECORD)</summary>
public struct RatingRecord
{
    public int Rating;
    public int MatchCnt, WinCnt, DefeatCnt, DrawCnt;
    public int[] Grade;           // [4]
    public int TurnCnt, DaidaCnt, PointSum, KyokuCnt;
    public int HoraCnt, HoraPoint, HojuCnt, HojuPoint;
    public int RichiCnt, FuroCnt;
    public int TipPoint, TipMatchCnt;
    public int TobiCnt, TobashiCnt;
    public int DoraCnt, UraDoraCnt, RichiHoraCnt;

    public static RatingRecord CreateEmpty() => new() { Grade = new int[4] };
}

/// <summary>A furo (meld) block (FURO_ST)</summary>
public class FuroBlock
{
    public List<PaiCode> Tiles      = new();
    public Act           Act;
    public int           TapaiOrder;

    public bool IsKan()  => Act is Act.Kan or Act.Ank or Act.Cha;
    public bool IsKou()  => Act is not (Act.Shu or Act.Chi);
    public bool IsShu()  => Act is Act.Shu or Act.Chi;

    public bool IsGreen()
    {
        int s = Tiles[0].GetSerial();
        if (s == 32) return true;      // 発 (Hatsu)
        if (s / 9 != 1) return false;  // must be Sou
        if (IsShu()) return s % 9 == 1;
        bool[] tbl = { false, true, true, true, false, true, false, true, false };
        return tbl[s % 9];
    }
}

/// <summary>
/// サーバーが計算したプレイヤーの有効アクション一覧。
/// GetValidActions() が返す。ProxyPlay・クライアント検証に使用する。
/// </summary>
public class ValidActions
{
    public int Order { get; set; }
    public string HoraErrorReason { get; set; } = "";

    // ツモアガリ可否
    public bool CanTsumo  { get; set; }
    // ロンアガリ可否 (フーロモード)
    public bool CanRon    { get; set; }
    // パス可否
    public bool CanPass   { get; set; }
    // 九種九牌
    public bool CanTaopai { get; set; }

    // 打牌候補 (BipaiIndex)
    public List<int>    TapCandidates    { get; } = new();
    // リーチ打牌候補 (BipaiIndex)
    public List<int>    RichiCandidates  { get; } = new();
    // 暗槓候補 (BipaiIndex[4] の配列)
    public List<int[]>  AnkanCandidates  { get; } = new();
    // 加槓候補 (BipaiIndex)
    public List<int>    ChakanCandidates { get; } = new();
    // 花牌候補 (BipaiIndex)
    public List<int>    HuaCandidates    { get; } = new();
    // チー候補 (BipaiIndex[2])
    public List<int[]>  ChiCandidates    { get; } = new();
    // ポン候補 (BipaiIndex[2])
    public List<int[]>  PonCandidates    { get; } = new();
    // カン候補 (BipaiIndex[3])
    public List<int[]>  KanCandidates    { get; } = new();
}
