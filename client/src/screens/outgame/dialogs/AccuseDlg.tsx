/**
 * CHgChatNoticeDlg 相当 — チャット通報ダイアログ
 * レガシー: legacy/client/HgComM/HgChatNoticeDlg.cpp + MJAccuseManager.cpp
 */
import { useState } from 'react'
import { showMessage } from '../../../utils/msgbox'

const FONT = "'MS UI Gothic', 'MS PGothic', 'Meiryo', sans-serif"
const IMG = '/assets/images/common'
const DLG_BG = 'rgb(255,241,200)'
const RADIO_W = 14
const RADIO_H = 15

const REASONS = [
  'めいわくな発言',
  'わいせつな発言',
  '悪口（暴言）',
  '個人情報の発言',
  '利用規約に違反する発言',
  'そのほか',
] as const

const RADIO_POS = [
  { x: 58, y: 196, w: 120 },
  { x: 58, y: 217, w: 120 },
  { x: 58, y: 240, w: 120 },
  { x: 200, y: 196, w: 120 },
  { x: 200, y: 217, w: 147 },
  { x: 200, y: 240, w: 120 },
] as const

export interface AccusePayload {
  targetPix: string
  reasonIndex: number
  reason: string
  chatContent: string
}

interface Props {
  myPix: string
  myMemberName?: string
  speakers: string[]
  speakerNameById?: Map<string, string>
  chatContent: string
  onOK?: (payload: AccusePayload) => void | Promise<void>
  onClose: () => void
}

export default function AccuseDlg({ myPix, myMemberName, speakers, speakerNameById = new Map<string, string>(), chatContent, onOK, onClose }: Props) {
  const [targetPix, setTargetPix] = useState(speakers[0] ?? '')
  const [reasonIndex, setReasonIndex] = useState(0)
  const [okFrame, setOkFrame] = useState(0)
  const [cancelFrame, setCancelFrame] = useState(0)

  const submit = async () => {
    if (!targetPix) {
      void showMessage('通報する人のIDを選択してください。')
      return
    }
    await onOK?.({ targetPix, reasonIndex, reason: REASONS[reasonIndex] ?? REASONS[0], chatContent })
    onClose()
  }

  const spriteButton = (src: string, x: number, y: number, frame: number, setFrame: (frame: number) => void, onClick: () => void, title: string) => (
    <button
      type="button"
      aria-label={title}
      title={title}
      onMouseEnter={() => setFrame(2)}
      onMouseLeave={() => setFrame(0)}
      onMouseDown={() => setFrame(3)}
      onMouseUp={() => setFrame(2)}
      onClick={onClick}
      style={{
        position: 'absolute', left: x, top: y, width: 117, height: 36,
        padding: 0, border: 0, background: `url(${src}) ${-frame * 117}px 0 / auto 36px no-repeat`,
        cursor: 'pointer', outline: 'none',
      }}
    />
  )

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 220,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.25)',
    }}>
      <div style={{
        position: 'relative',
        width: 370,
        height: 308,
        background: DLG_BG,
        backgroundImage: `url(${IMG}/AccuseBkgnd.bmp)`,
        backgroundRepeat: 'no-repeat',
        backgroundPosition: '0 0',
        boxShadow: '2px 2px 7px rgba(0,0,0,0.45)',
        fontFamily: FONT,
        fontSize: 12,
        color: '#000',
      }}>
        <div style={{ position: 'absolute', left: 58, top: 137, width: 50, height: 15 }}>通報者</div>
        <input
          value={myMemberName || myPix}
          readOnly
          style={{ position: 'absolute', left: 155, top: 137, width: 150, height: 20, boxSizing: 'border-box', fontFamily: FONT, fontSize: 12 }}
        />

        <div style={{ position: 'absolute', left: 58, top: 159, width: 50, height: 15 }}>対象者</div>
        <select
          value={targetPix}
          onChange={event => setTargetPix(event.target.value)}
          style={{ position: 'absolute', left: 155, top: 159, width: 150, height: 20, fontFamily: FONT, fontSize: 12 }}
        >
          {speakers.map(pix => <option key={pix} value={pix}>{speakerNameById.get(pix) || pix}</option>)}
        </select>

        <div style={{ position: 'absolute', left: 58, top: 179, width: 50, height: 15 }}>通報理由</div>
        {REASONS.map((reason, index) => {
          const pos = RADIO_POS[index]
          return (
            <label key={reason} style={{ position: 'absolute', left: pos.x, top: pos.y, width: pos.w, height: RADIO_H, whiteSpace: 'nowrap' }}>
              <input
                type="radio"
                name="accuseReason"
                checked={reasonIndex === index}
                onChange={() => setReasonIndex(index)}
                style={{ position: 'absolute', left: 0, top: 0, width: RADIO_W, height: RADIO_H, margin: 0 }}
              />
              <span style={{ position: 'absolute', left: 20, top: 0, lineHeight: `${RADIO_H}px` }}>{reason}</span>
            </label>
          )
        })}

        {spriteButton(`${IMG}/sgurlbtn.png`, 58, 266, okFrame, setOkFrame, () => { void submit() }, 'OK')}
        {spriteButton(`${IMG}/sgcancelbtn.png`, 200, 266, cancelFrame, setCancelFrame, onClose, 'キャンセル')}
      </div>
    </div>
  )
}