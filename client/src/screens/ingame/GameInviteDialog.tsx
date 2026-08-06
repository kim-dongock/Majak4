import { useEffect, useState } from 'react'

export interface GameInviteMember {
  pix: string
  name: string
  rating: number
  slevel?: string
}

type InviteResult = 'accepted' | 'declined' | 'timeout'

interface Props {
  members: GameInviteMember[]
  targetPix: string | null
  waiting: boolean
  result: InviteResult | null
  onChooseTarget: (pix: string) => void
  onSend: (message: string) => void
  onCancelWait: () => void
  onTimeout: () => void
  onBackToMembers: () => void
  onClose: () => void
}

const DEFAULT_MESSAGE = '一緒に対戦しませんか？'
const WAIT_SECONDS = 15

export default function GameInviteDialog({
  members,
  targetPix,
  waiting,
  result,
  onChooseTarget,
  onSend,
  onCancelWait,
  onTimeout,
  onBackToMembers,
  onClose,
}: Props) {
  const [selectedPix, setSelectedPix] = useState(members[0]?.pix ?? '')
  const [message, setMessage] = useState(DEFAULT_MESSAGE)
  const [remainingSeconds, setRemainingSeconds] = useState(WAIT_SECONDS)
  const selectedMember = members.find(member => member.pix === selectedPix)
  const targetMember = members.find(member => member.pix === targetPix)

  useEffect(() => {
    if (!members.some(member => member.pix === selectedPix)) setSelectedPix(members[0]?.pix ?? '')
  }, [members, selectedPix])

  useEffect(() => {
    if (!waiting) {
      setRemainingSeconds(WAIT_SECONDS)
      return
    }
    const startedAt = Date.now()
    const timer = window.setInterval(() => {
      const next = Math.max(0, WAIT_SECONDS - Math.floor((Date.now() - startedAt) / 1000))
      setRemainingSeconds(next)
      if (next === 0) onTimeout()
    }, 200)
    return () => window.clearInterval(timer)
  }, [onTimeout, waiting])

  const submit = () => {
    if (!selectedMember) return
    onChooseTarget(selectedMember.pix)
  }

  const send = () => {
    if (!targetMember || !message.trim()) return
    onSend(message.trim())
  }

  const resultText = result === 'accepted'
    ? `${targetMember?.name ?? '相手'}さんがゲーム申し込みを承諾しました。`
    : result === 'declined'
      ? `${targetMember?.name ?? '相手'}さんはゲーム申し込みを辞退しました。`
      : `${targetMember?.name ?? '相手'}さんから応答がありませんでした。`

  return (
    <div className="majak-game-invite-backdrop" role="presentation">
      <section className="majak-game-invite-dialog" role="dialog" aria-modal="true" aria-label="ゲームの申し込み">
        {!targetPix && !result && (
          <>
            <header className="majak-game-invite-dialog__header">
              <h2>ゲームの申し込み</h2>
              <button type="button" className="majak-game-invite-dialog__icon-button" aria-label="閉じる" onClick={onClose}>×</button>
            </header>
            <div className="majak-game-invite-dialog__body">
              <p className="majak-game-invite-dialog__lead">招待するメンバーを選択してください。</p>
              <div className="majak-game-invite-dialog__member-list" role="listbox" aria-label="招待するメンバー">
                {members.length === 0 ? (
                  <p className="majak-game-invite-dialog__empty">招待できるメンバーがいません。</p>
                ) : members.map(member => (
                  <button
                    key={member.pix}
                    type="button"
                    role="option"
                    aria-selected={selectedPix === member.pix}
                    className={`majak-game-invite-dialog__member${selectedPix === member.pix ? ' is-selected' : ''}`}
                    onClick={() => setSelectedPix(member.pix)}
                    onDoubleClick={() => { setSelectedPix(member.pix); onChooseTarget(member.pix) }}
                  >
                    <span>{member.name || member.pix}</span>
                    <small>{member.slevel || `${member.rating} P`}</small>
                  </button>
                ))}
              </div>
            </div>
            <footer className="majak-game-invite-dialog__actions">
              <button type="button" className="majak-game-invite-dialog__primary" disabled={!selectedMember} onClick={submit}>対戦申込</button>
              <button type="button" onClick={onClose}>閉じる</button>
            </footer>
          </>
        )}

        {targetPix && !waiting && !result && (
          <>
            <header className="majak-game-invite-dialog__header"><h2>ゲームの申し込み</h2></header>
            <div className="majak-game-invite-dialog__body">
              <p className="majak-game-invite-dialog__lead"><strong>{targetMember?.name ?? targetPix}</strong> さんへの招待メッセージ</p>
              <label className="majak-game-invite-dialog__field">
                <span>招待メッセージ</span>
                <input value={message} onChange={event => setMessage(event.target.value)} autoFocus />
              </label>
            </div>
            <footer className="majak-game-invite-dialog__actions">
              <button type="button" className="majak-game-invite-dialog__primary" disabled={!message.trim()} onClick={send}>OK</button>
              <button type="button" onClick={onBackToMembers}>キャンセル</button>
            </footer>
          </>
        )}

        {waiting && !result && (
          <>
            <header className="majak-game-invite-dialog__header"><h2>ゲームの申し込み</h2></header>
            <div className="majak-game-invite-dialog__body majak-game-invite-dialog__wait">
              <p>{targetMember?.name ?? targetPix} さんの返事を待っています。</p>
              <p>しばらくお待ちください。[最大:15秒まで]</p>
              <progress value={remainingSeconds} max={WAIT_SECONDS} aria-label="応答待ち時間" />
            </div>
            <footer className="majak-game-invite-dialog__actions"><button type="button" onClick={onCancelWait}>キャンセル</button></footer>
          </>
        )}

        {result && (
          <>
            <header className="majak-game-invite-dialog__header"><h2>ゲームの申し込み</h2></header>
            <div className="majak-game-invite-dialog__body majak-game-invite-dialog__result"><p>{resultText}</p></div>
            <footer className="majak-game-invite-dialog__actions"><button type="button" className="majak-game-invite-dialog__primary" onClick={onClose}>OK</button></footer>
          </>
        )}
      </section>
    </div>
  )
}