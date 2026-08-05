/**
 * Rating ranking dialog — ShowRankingDialog 相当の入口。
 * レガシー: CMajakChannelWnd::OnBtnRankingClicked → ShowRankingDialog(...)
 * サーバー: mjkc25e RatingRankInfo → gradeRankList / gradeRankSelf
 */
import { useEffect, useState } from 'react'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

export type RankingItem = {
  pix: string
  rank: number
  rating: number
  grade: number
  lastDate?: string
  isSelf?: number
}

export type RankingData = {
  rankDate?: number | string
  rankId?: number | string
  gradeRankList: RankingItem[]
  gradeRankSelf?: RankingItem & { szIndex?: string }
}

type Props = {
  data: RankingData
  onClose: () => void
  memberNameByPix?: Map<string, string>
}

const DIALOG_W = 500
const DIALOG_H = 520

export default function RankingDlg({ data, onClose, memberNameByPix = new Map<string, string>() }: Props) {
  const layoutMode = useOutgameLayoutMode()
  const isMobile = layoutMode !== 'desktop'
  const displayName = (pix: string) => memberNameByPix.get(pix) || pix
  const [dialogScale, setDialogScale] = useState(1)

  useEffect(() => {
    if (!isMobile) {
      setDialogScale(1)
      return
    }
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / DIALOG_W, (window.innerHeight - margin) / DIALOG_H))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [isMobile])

  return (
    <div
      role="dialog"
      aria-modal="true"
      style={{
        position: isMobile ? 'fixed' : 'absolute', inset: 0, zIndex: 260,
        display: isMobile ? 'flex' : undefined,
        alignItems: isMobile ? 'center' : undefined,
        justifyContent: isMobile ? 'center' : undefined,
        background: 'rgba(0,0,0,0.35)', fontFamily: 'var(--majak-font-family-ui)',
      }}
      onContextMenu={event => event.preventDefault()}
    >
      <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
      <div style={{
        position: isMobile ? 'relative' : 'absolute',
        left: isMobile ? 0 : 260,
        top: isMobile ? 0 : 95,
        width: DIALOG_W,
        height: DIALOG_H,
        transform: `scale(${dialogScale})`,
        transformOrigin: 'top left',
        background: '#ece9d8', border: '2px solid #404040', boxShadow: '4px 4px 0 rgba(0,0,0,0.35)',
      }}>
        <div style={{ position: 'absolute', left: 0, top: 0, right: 0, height: 24, background: '#0a246a', color: '#fff', fontSize: 'calc(13px * var(--majak-type-scale))', lineHeight: '24px', paddingLeft: 8 }}>
          ランキング
        </div>
        <button type="button" onClick={onClose} style={{ position: 'absolute', right: 4, top: 3, width: 18, height: 18, padding: 0, lineHeight: '16px' }}>×</button>

        <div style={{ position: 'absolute', left: 12, top: 36, fontSize: 'calc(12px * var(--majak-type-scale))' }}>
          対象年月: {data.rankDate ?? '-'}　種別: {data.rankId ?? '-'}
        </div>

        {data.gradeRankSelf && (
          <div style={{ position: 'absolute', left: 12, top: 60, right: 12, height: 44, border: '1px solid #808080', background: '#fff', padding: 6, fontSize: 'calc(12px * var(--majak-type-scale))' }}>
            自分: {data.gradeRankSelf.rank || '-'}位　{displayName(data.gradeRankSelf.pix)}　R {data.gradeRankSelf.rating}
            {data.gradeRankSelf.szIndex ? `　${data.gradeRankSelf.szIndex}` : ''}
          </div>
        )}

        <div style={{ position: 'absolute', left: 12, top: 118, right: 12, bottom: 48, border: '1px solid #808080', background: '#fff', overflowY: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'calc(12px * var(--majak-type-scale))' }}>
            <thead>
              <tr style={{ background: '#d4d0c8' }}>
                <th style={{ width: 52, textAlign: 'right', padding: '3px 6px' }}>順位</th>
                <th style={{ textAlign: 'left', padding: '3px 6px' }}>ニックネーム</th>
                <th style={{ width: 70, textAlign: 'right', padding: '3px 6px' }}>R</th>
                <th style={{ width: 64, textAlign: 'right', padding: '3px 6px' }}>段位</th>
              </tr>
            </thead>
            <tbody>
              {data.gradeRankList.map(item => (
                <tr key={`${item.rank}-${item.pix}`} style={{ color: item.isSelf ? '#c00000' : '#000' }}>
                  <td style={{ textAlign: 'right', padding: '3px 6px' }}>{item.rank}</td>
                  <td style={{ padding: '3px 6px' }}>{displayName(item.pix)}</td>
                  <td style={{ textAlign: 'right', padding: '3px 6px' }}>{item.rating}</td>
                  <td style={{ textAlign: 'right', padding: '3px 6px' }}>{item.grade}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <button type="button" onClick={onClose} style={{ position: 'absolute', right: 12, bottom: 12, width: 80, height: 26 }}>閉じる</button>
      </div>
      </div>
    </div>
  )
}