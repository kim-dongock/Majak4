/**
 * CMajakStadiumWnd::DrawMemberInfo() 共通コンポーネント
 *
 * CMJSelGroupWnd::OnPaint() と CMJSelLobbyWnd::OnPaint() の両方から
 * DrawMemberInfo() が呼ばれるため共通化。
 *
 * 座標 (1014×704 canvas 内の絶対位置):
 *   strName (nickname): DT_CENTER CRect(838,37,977,52)  blue bold 12px
 *   i=0: "コイン" CRect(842,58,890,73) / value CRect(890,58,986,73)
 *   i=1: "称号"   CRect(842,74,890,89) / value CRect(890,74,986,89)
 *   i=2: ""       CRect(842,90,890,105)/ value CRect(890,90,986,105)
 *
 * 表示条件: m_pMember->m_szAvatarId が空でない場合のみ
 */
import { useEffect } from 'react'
import { useAuthStore }       from '../store/authStore'
import { useGamePlayerStore } from '../store/gamePlayerStore'

export default function DrawMemberInfo() {
  const player                             = useAuthStore(s => s.player)
  const { data: gpData, fetchProfile }     = useGamePlayerStore()
  const gameMoneyText = typeof gpData?.gamMoney === 'number' && Number.isFinite(gpData.gamMoney)
    ? gpData.gamMoney.toLocaleString('ja-JP')
    : ''

  /** DrawMemberInfo 相当: チャンネル未入室状態でのコイン・称号取得 */
  useEffect(() => {
    if (player?.pix && gpData === null) {
      fetchProfile(player.pix)
    }
  }, [player?.pix, gpData, fetchProfile])

  if (!player?.avatarId) return null

  return (
    <>
      {/* strName — DT_CENTER, RGB(0,114,188), FW_BOLD, CRect(838,37,977,52) */}
      <span
        style={{
          position: 'absolute',
          left: 838, top: 37,
          width: 139, height: 15,
          textAlign: 'center',
          display: 'inline-block',
          fontSize: 12,
          fontWeight: 'bold',
          color: 'rgb(0,114,188)',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
          pointerEvents: 'none',
        }}
      >
        {player.name}
      </span>

      {/* i=0: "コイン" / " : {GamMoney}" */}
      <span style={labelStyle(842, 58)}>コイン</span>
      <span style={valueStyle(890, 58)}>
        {gameMoneyText ? ` : ${gameMoneyText}` : ''}
      </span>

      {/* i=1: "称号" / " : {SLevel}" */}
      <span style={labelStyle(842, 74)}>称号</span>
      <span style={valueStyle(890, 74)}>
        {gpData ? ` : ${gpData.slevel}` : ''}
      </span>

      {/* i=2: 空文字 (初期化されていない CString = 空) */}
      <span style={labelStyle(842, 90)} />
      <span style={valueStyle(890, 90)} />
    </>
  )
}

// ── スタイルヘルパー ──────────────────────────────────────────────────────
// m_pFont 相当: 12px 通常 黒  DT_LEFT
function labelStyle(left: number, top: number): React.CSSProperties {
  return {
    position: 'absolute',
    left, top,
    width: 48, height: 15,
    fontSize: 12, fontWeight: 'normal',
    color: '#000000',
    overflow: 'hidden', whiteSpace: 'nowrap',
    pointerEvents: 'none', textAlign: 'left',
  }
}

function valueStyle(left: number, top: number): React.CSSProperties {
  return {
    position: 'absolute',
    left, top,
    width: 96, height: 15,
    fontSize: 12, fontWeight: 'normal',
    color: '#000000',
    overflow: 'hidden', whiteSpace: 'nowrap',
    pointerEvents: 'none', textAlign: 'left',
  }
}
