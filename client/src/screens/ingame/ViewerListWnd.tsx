/**
 * CMJViewerListWnd 相当 — インゲーム観戦者リストパネル (AP-09 §2-7)
 * レガシー: legacy/client/HgMajak2/MJViewerListWnd.h/cpp
 *
 * 実表示 (MJRoomWnd2.cpp AddViewer/DelViewer):
 *   X_VIEWER_WINDOW=805 Y=235 RIGHT=1010 BOTTOM=310
 *   X_VIEWAVA=834 Y_VIEWAVA=239 DX=26 DY=39 W=22 H=32 CX=6 CY=2
 *   m_listViewerAva に追加し、rc.top < VIEWER_WINDOW_BOTTOM の範囲のみ表示。
 *   CToolTipCtrl.AddTool() でプレイヤー情報ツールチップのみ表示。
 */
import { getDefaultAvatarUrl, getShortAvatarUrl } from '../../utils/resources'

const X_VIEWER_WINDOW = 805
const Y_VIEWER_WINDOW = 235
const W_VIEWER_WINDOW = 1010 - X_VIEWER_WINDOW
const H_VIEWER_WINDOW = 310 - Y_VIEWER_WINDOW
const X_VIEWAVA = 834 - X_VIEWER_WINDOW
const Y_VIEWAVA = 239 - Y_VIEWER_WINDOW
const DX_VIEWAVA = 26
const DY_VIEWAVA = 39
const W_VIEWAVA = 22
const H_VIEWAVA = 32
const CX_VIEWAVA = 6
const CY_VIEWAVA = 2

export interface ViewerEntry {
  pix: string
  name:     string
  avatarId?: string
  sex?:      string
  slevel?:  string   // 称号
  dan?:     string   // 段位
  rating?:  number
  playerPos?: number
}

interface Props {
  viewers: ViewerEntry[]
  x?: number
  y?: number
}

/** ====================================================================
 * CMJViewerListWnd 本体
 * RoomScreen から position:absolute で埋め込まれる静的コンポーネント。
 * ==================================================================== */
export default function ViewerListWnd({ viewers, x = X_VIEWER_WINDOW, y = Y_VIEWER_WINDOW }: Props) {
  const visibleViewers = viewers.slice(0, CX_VIEWAVA * CY_VIEWAVA)

  const getViewerTooltip = (viewer: ViewerEntry): string => {
    const sexText = viewer.sex === 'F' || viewer.sex === 'female' ? '女' : '男'
    const rating = Number.isFinite(viewer.rating) ? viewer.rating : 0
    return `${viewer.name || viewer.pix}[${sexText}]\n${viewer.slevel ?? ''}(${rating})`
  }

  return (
    /* CMJRoomWnd viewer avatar area: (805,235)-(1010,310) */
    <div style={{
      position: 'absolute',
      left: x, top: y,
      width: W_VIEWER_WINDOW, height: H_VIEWER_WINDOW,
      overflow: 'hidden',
      pointerEvents: 'none',
    }}>
      {visibleViewers.map((viewer, idx) => {
        const x = X_VIEWAVA + DX_VIEWAVA * (idx % CX_VIEWAVA)
        const y = Y_VIEWAVA + DY_VIEWAVA * Math.floor(idx / CX_VIEWAVA)
        const sex = viewer.sex === 'F' || viewer.sex === 'female' ? 'female' : 'male'
        const title = getViewerTooltip(viewer)
        return (
          <div
            key={viewer.pix}
            title={title}
            style={{
              position: 'absolute',
              left: x,
              top: y,
              width: W_VIEWAVA,
              height: H_VIEWAVA,
              border: 0,
              padding: 0,
              overflow: 'hidden',
              background: 'transparent',
              pointerEvents: 'auto',
            }}
          >
            <img
              src={getShortAvatarUrl(viewer.avatarId)}
              alt={viewer.name || viewer.pix}
              draggable={false}
              onError={e => { e.currentTarget.src = getDefaultAvatarUrl(sex) }}
              style={{ width: W_VIEWAVA, height: H_VIEWAVA, objectFit: 'cover', imageRendering: 'pixelated' }}
            />
          </div>
        )
      })}
    </div>
  )
}
