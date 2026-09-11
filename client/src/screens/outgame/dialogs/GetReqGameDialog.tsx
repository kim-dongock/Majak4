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
import { useEffect, useRef, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { getAvatarUrl, getDefaultAvatarUrl } from '../../../utils/resources'
import { useAuthStore } from '../../../store/authStore'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const TIMER_INTERVAL_MS = 100
const PROGRESS_MAX = 100

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
  inviteMessage = '一緒に対戦しませんか？',
  inviterSex,
  inviterRating = 0,
  inviterLevel = '',
  onClose,
  onAccepted,
}: Props) {
  const [progress, setProgress] = useState(PROGRESS_MAX)
  const layoutMode = useOutgameLayoutMode()
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

  const avatarFallback = getAvatarFallback(inviterSex)

  return (
    <div className={`majak-popup-overlay majak-popup-overlay--${layoutMode}`} role="presentation">
      <section className="majak-popup-panel" role="dialog" aria-modal="true" aria-label="ゲームの申し込み">
        <header className="majak-popup-titlebar">
          <h2>ゲームの申し込み</h2>
          <button className="majak-popup-titlebar__close" type="button" onClick={() => void replyInvite(false)} aria-label="閉じる">×</button>
        </header>
        <div className="majak-popup-body">
          <p className="majak-popup-kicker">対戦リクエスト</p>
          <section className="majak-popup-message" aria-label="お誘いの言葉">
            <span className="majak-popup-message__label">ルーム</span>
            <strong>{roomName || 'ルーム'}</strong>
            <p>{inviteMessage}</p>
          </section>
          <section className="majak-popup-profile" aria-label="相手の情報">
            <div className="majak-popup-avatar">
            <img
              src={getAvatarUrl(avatarId ?? null)}
              alt={`${inviterName || inviterId}のアバター`}
              draggable={false}
              onError={event => { event.currentTarget.src = avatarFallback }}
            />
            </div>
            <div className="majak-popup-profile__copy">
              <h3>{inviterName || inviterId}</h3>
              <dl>
                <div><dt>性別</dt><dd>{getSexText(inviterSex) || '-'}</dd></div>
                <div><dt>レーティング</dt><dd>{inviterRating}</dd></div>
                <div><dt>称号</dt><dd>{inviterLevel || '-'}</dd></div>
              </dl>
            </div>
          </section>
          <section className="majak-popup-countdown" aria-label="制限時間">
            <div><span>返答時間</span><strong>{Math.ceil(progress / 10)}秒</strong></div>
            <div className="majak-popup-countdown__progress" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress}>
              <div style={{ width: `${progress}%` }} />
            </div>
          </section>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" className="is-primary" onClick={() => void replyInvite(true)}>はい</button>
          <button type="button" onClick={() => void replyInvite(false)}>いいえ</button>
        </footer>
      </section>
    </div>
  )
}