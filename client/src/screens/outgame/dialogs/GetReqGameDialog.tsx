/**
 * CMJGetReqGameDialog 相当 — ゲーム申し込み受信ダイアログ (AP-09 §1-11)
 * レガシー:
 *   - legacy/client/HgChnlM/HgGetReqGameDialog.cpp
 *   - legacy/client/IncludeM2/HgGetReqGameDialog.h
 *   - legacy/client/HgMajak2/MajakChannelWnd.cpp::CMJGetReqGameDialog::OnInitDialog
 *   - legacy/client/HgChnlM/HgChnlJpn.rc IDD_GETREQGAME_TEMPLATE
 *
 * RC: IDD_GETREQGAME_TEMPLATE DIALOG 0,0,166,225 / CAPTION "ゲームの申し込み"
 * Timer: SetTimer(2, 100, NULL), Progress 100→0, timeout is IDCANCEL.
 * OnDestroy: ReplyInviteGame(m_szTargetId, m_nResponse, m_nRoomId, m_szRoomPwd).
 */
import { type CSSProperties, useEffect, useRef, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { getAvatarUrl, getDefaultAvatarUrl } from '../../../utils/resources'
import { useAuthStore } from '../../../store/authStore'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const IMG = '/assets/images/game'

const OPTION_ICON = {
  set: `${IMG}/mj_opt_0.png`,
  kui: `${IMG}/mj_opt_3.png`,
  uma: `${IMG}/mj_opt_1.png`,
  ron: `${IMG}/mj_optron.png`,
  red: `${IMG}/mj_opt_5.png`,
  spd: `${IMG}/mj_opt_4.png`,
  opn: `${IMG}/mj_opt_6.png`,
  cht: `${IMG}/mj_opt_7.png`,
  ach: `${IMG}/mj_opt_8.png`,
} as const

const TIMER_INTERVAL_MS = 100
const PROGRESS_MAX = 100
const TITLE_H = 22
const SX = (du: number) => Math.round(du * 1.5)
const SY = (du: number) => Math.round(du * 1.625)
const DLG_W = SX(166)
const DLG_H = SY(225)
const FONT = 'var(--majak-font-family-ui)'
const DLG_BG = '#d4d0c8'

interface Props {
  inviterId: string
  inviterName: string
  roomId: number
  roomPwd: string
  avatarId?: string
  roomName?: string
  roomOption?: string
  inviteMessage?: string
  inviterSex?: string
  inviterRating?: number
  inviterLevel?: string
  onClose: () => void
  onAccepted?: () => void
}

function readOptionDigit(option: string | undefined, index: number, fallback: number) {
  const char = option?.charAt(index) ?? ''
  return /^\d$/.test(char) ? Number(char) : fallback
}

function optionSprite(src: string, value: number, x: number, y: number, maxFrame = 8) {
  const frame = Math.max(0, Math.min(maxFrame, value))
  return (
    <div
      key={`${src}-${x}-${y}`}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: 17,
        height: 17,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-17 * frame}px 0`,
        backgroundRepeat: 'no-repeat',
        pointerEvents: 'none',
      }}
    />
  )
}

function getSexText(sex?: string) {
  if (!sex) return ''
  return sex === 'F' || sex.toLowerCase() === 'female' ? '女' : '男'
}

function getAvatarFallback(sex?: string) {
  return getDefaultAvatarUrl(sex === 'F' || sex?.toLowerCase() === 'female' ? 'female' : 'male')
}

export default function GetReqGameDialog({
  inviterId,
  inviterName,
  roomId,
  roomPwd,
  avatarId,
  roomName = '',
  roomOption = '',
  inviteMessage = '一緒に対戦しませんか？',
  inviterSex,
  inviterRating = 0,
  inviterLevel = '',
  onClose,
  onAccepted,
}: Props) {
  const [progress, setProgress] = useState(PROGRESS_MAX)
  const layoutMode = useOutgameLayoutMode()
  const isMobile = layoutMode !== 'desktop'
  const [dialogScale, setDialogScale] = useState(1)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const closedRef = useRef(false)

  const stopTimer = () => {
    if (timerRef.current) {
      clearInterval(timerRef.current)
      timerRef.current = null
    }
  }

  const replyInvite = async (accept: boolean) => {
    if (closedRef.current) return
    closedRef.current = true
    stopTimer()
    const pix = useAuthStore.getState().player?.pix ?? ''
    await SignalR.send('c23e', {
      k3e: pix,
      accept: accept ? '1' : '0',
      k64e: accept ? 'v7e' : 'v8e',
      inviterId,
      roomId: String(roomId),
      ...(accept ? { roomPwd } : {}),
    }).catch(() => {})
    onClose()
    if (accept) onAccepted?.()
  }

  useEffect(() => {
    if (!isMobile) {
      setDialogScale(1)
      return
    }
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / DLG_W, (window.innerHeight - margin) / (DLG_H + TITLE_H)))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [isMobile])

  useEffect(() => {
    timerRef.current = setInterval(() => {
      setProgress(value => {
        const next = value - 1
        if (next <= 0) {
          void replyInvite(false)
          return 0
        }
        return next
      })
    }, TIMER_INTERVAL_MS)

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented) return
      if (event.key === 'Enter' || (event.altKey && event.key.toLowerCase() === 'y')) {
        event.preventDefault()
        void replyInvite(true)
      } else if (event.key === 'Escape' || (event.altKey && event.key.toLowerCase() === 'n')) {
        event.preventDefault()
        void replyInvite(false)
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => {
      window.removeEventListener('keydown', handleKeyDown)
      stopTimer()
    }
  }, [])

  const controlBase: CSSProperties = {
    position: 'absolute',
    fontFamily: FONT,
    fontSize: 'calc(12px * var(--majak-type-scale))',
    color: '#000',
    boxSizing: 'border-box',
  }

  const fieldsetStyle = (left: number, top: number, width: number, height: number): CSSProperties => ({
    ...controlBase,
    left: SX(left),
    top: SY(top),
    width: SX(width),
    height: SY(height),
    margin: 0,
    padding: 0,
    border: '2px groove #f0f0f0',
  })

  const inputStyle: CSSProperties = {
    ...controlBase,
    height: SY(14),
    padding: '1px 3px',
    border: '2px inset #d4d0c8',
    background: '#fff',
  }

  const buttonStyle: CSSProperties = {
    ...controlBase,
    width: SX(50),
    height: SY(17),
    padding: 0,
    lineHeight: `${SY(17) - 4}px`,
    background: DLG_BG,
    border: '2px outset #d4d0c8',
    cursor: 'pointer',
  }

  const optionX = SX(10)
  const optionY = SY(33) + 3
  const optionIcons = roomOption ? [
    optionSprite(OPTION_ICON.set, readOptionDigit(roomOption, 0, 1), optionX + 16 * 0, optionY, 1),
    optionSprite(OPTION_ICON.kui, readOptionDigit(roomOption, 3, 0), optionX + 16 * 1, optionY, 1),
    optionSprite(OPTION_ICON.uma, readOptionDigit(roomOption, 1, 2), optionX + 16 * 2, optionY, 3),
    optionSprite(OPTION_ICON.ron, readOptionDigit(roomOption, 12, 0), optionX + 16 * 3, optionY, 2),
    optionSprite(OPTION_ICON.red, readOptionDigit(roomOption, 5, 2), optionX + 16 * 4, optionY, 2),
    optionSprite(OPTION_ICON.spd, readOptionDigit(roomOption, 2, 2), optionX + 16 * 8, optionY, 3),
    optionSprite(OPTION_ICON.opn, readOptionDigit(roomOption, 6, 0), optionX + 16 * 9, optionY, 1),
    optionSprite(readOptionDigit(roomOption, 14, 0) ? OPTION_ICON.cht : OPTION_ICON.ach, readOptionDigit(roomOption, 14, 0) ? readOptionDigit(roomOption, 7, 0) : 0, optionX + 16 * 10, optionY, 1),
  ] : null

  const memberInfo = [
    `性　別 : ${getSexText(inviterSex)}`,
    `ポイント: ${inviterRating}`,
    `称　号: ${inviterLevel}`,
  ].join('\n')
  const avatarFallback = getAvatarFallback(inviterSex)

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="ゲームの申し込み"
      style={{
        position: isMobile ? 'fixed' : 'absolute',
        inset: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'rgba(0,0,0,0.5)',
        zIndex: 200,
      }}
    >
      <div style={{ width: DLG_W * dialogScale, height: (DLG_H + TITLE_H) * dialogScale }}>
      <div style={{ position: 'relative', width: DLG_W, height: DLG_H + TITLE_H, transform: `scale(${dialogScale})`, transformOrigin: 'top left', boxShadow: '3px 3px 8px rgba(0,0,0,0.6)' }}>
        <div style={{ height: TITLE_H, background: 'linear-gradient(to right, #000080, #1060d0)', display: 'flex', alignItems: 'center', paddingLeft: 8, boxSizing: 'border-box', fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', fontWeight: 'bold', color: '#fff', userSelect: 'none' }}>
          ゲームの申し込み
        </div>

        <div style={{ position: 'relative', width: DLG_W, height: DLG_H, background: DLG_BG }}>
          <fieldset style={fieldsetStyle(3, 4, 160, 65)}><legend>お誘いの言葉</legend></fieldset>
          <input readOnly value={roomName} style={{ ...inputStyle, left: SX(10), top: SY(17), width: SX(146) }} />
          {optionIcons}
          <input readOnly value={inviteMessage} style={{ ...inputStyle, left: SX(10), top: SY(49), width: SX(146) }} />

          <fieldset style={fieldsetStyle(3, 73, 160, 73)}><legend>相手の情報</legend></fieldset>
          <div style={{ position: 'absolute', left: SX(17), top: SY(85), width: SX(26), height: SY(43), overflow: 'hidden', background: '#fff' }}>
            <img
              src={getAvatarUrl(avatarId ?? null)}
              alt=""
              draggable={false}
              style={{ width: '100%', height: '100%', objectFit: 'cover' }}
              onError={event => { event.currentTarget.src = avatarFallback }}
            />
          </div>
          <div style={{ ...controlBase, left: SX(8), top: SY(133), width: SX(46), height: SY(8), lineHeight: `${SY(8)}px`, fontWeight: 'bold', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap' }}>
            {inviterName || inviterId}
          </div>
          <fieldset style={fieldsetStyle(61, 80, 94, 59)} />
          <div style={{ ...controlBase, left: SX(66), top: SY(91), width: SX(84), height: SY(43), lineHeight: '16px', whiteSpace: 'pre-line', overflow: 'hidden' }}>
            {memberInfo}
          </div>

          <fieldset style={fieldsetStyle(3, 152, 160, 42)}><legend>制限時間</legend></fieldset>
          <div style={{ ...controlBase, left: SX(7), top: SY(164), width: SX(152), height: SY(8), lineHeight: `${SY(8)}px`, textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap' }}>
            時間内に返答しないと「いいえ」になります。
          </div>
          <div style={{ position: 'absolute', left: SX(14), top: SY(178), width: SX(136), height: SY(9), border: '1px solid #404040', background: '#c0c0c0', boxSizing: 'border-box', overflow: 'hidden' }}>
            <div style={{ width: `${progress}%`, height: '100%', background: '#000080' }} />
          </div>

          <button onClick={() => void replyInvite(true)} accessKey="y" style={{ ...buttonStyle, left: SX(31), top: SY(202), fontWeight: 'bold' }}>
            はい(Y)
          </button>
          <button onClick={() => void replyInvite(false)} accessKey="n" style={{ ...buttonStyle, left: SX(85), top: SY(202) }}>
            いいえ(N)
          </button>
        </div>
      </div>
      </div>
    </div>
  )
}