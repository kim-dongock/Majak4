namespace MajakServer.Models.Game;

/// <summary>
/// バニシュ (退場予約) 状態
/// 原典: HMajRoomServer.cpp の m_stBanishInfo (BANISHINFO 型)
///
/// フィールド:
///   PreBanishing     — 次局終了時にバニシュする予約フラグ (G::keyPreBanishing / k103e)
///   ReserveBanishing — バニシュ予約を別メンバーが既に行っているフラグ (G::keyReserveBanishing / k104e)
///   ReserveMemberNo  — バニシュ予約したメンバーの MemberNo (G::keyMemberNo / k3e)
///
/// 初期化: InitBanish() 相当 — ゲーム開始時 (new GameRoom) に全フィールド false/null
/// </summary>
public class BanishInfo
{
    /// <summary>
    /// 次局終了時にそのプレイヤーをルームから追い出す予約フラグ。
    /// 原典: m_bPreBanishing (G::keyPreBanishing = k103e)
    /// </summary>
    public bool   PreBanishing     { get; set; } = false;

    /// <summary>
    /// 他のプレイヤーがバニシュ予約を持っているかどうかのフラグ。
    /// 原典: m_bReserveBanishing (G::keyReserveBanishing = k104e)
    /// </summary>
    public bool   ReserveBanishing { get; set; } = false;

    /// <summary>
    /// バニシュ予約を行ったメンバーの MemberNo。
    /// 原典: m_szReserveBanishingMember (G::keyMemberNo = k3e で送出)
    /// </summary>
    public string? ReserveMemberNo  { get; set; } = null;

    /// <summary>
    /// バニシュ情報のリセット。
    /// 原典: InitBanish() — 局開始時に呼ばれる
    /// </summary>
    public void Reset()
    {
        PreBanishing     = false;
        ReserveBanishing = false;
        ReserveMemberNo  = null;
    }
}
