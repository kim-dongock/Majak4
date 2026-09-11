import { useState } from 'react'
import { showError } from '../../../utils/msgbox'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

export interface RoomCreateInfo { title: string; password: string; isPrivate: boolean; viewerEnable: boolean; trainingAiLevel: 'Legacy' | 'Advanced' }

interface Props { initialTitle: string; viewerEnable?: boolean; trainingMode?: boolean; onOK: (info: RoomCreateInfo) => void; onCancel: () => void }

const PASSWORD_MAX = 8
const RANDOM_ROOM_TITLES = ['いらっしゃいませ。', '勝負だ！', '気軽にどうぞ～']
const ROOM_TITLES = [...RANDOM_ROOM_TITLES, 'あいさつしよう！', '仲間同士でわいわい♪', '☆チャットしながら…', '☆楽しくネ！', '(^-^*)自由だよ♪', '☆勝っても負けても', 'さくさく', 'つわもの募集', '■初心者部屋■', '■無言部屋■']

export default function RoomCreateDlg({ initialTitle, viewerEnable = true, trainingMode = false, onOK, onCancel }: Props) {
  const layoutMode = useOutgameLayoutMode()
  const [title, setTitle] = useState(() => initialTitle || RANDOM_ROOM_TITLES[Math.floor(Math.random() * RANDOM_ROOM_TITLES.length)])
  const [isPrivate, setIsPrivate] = useState(false)
  const [password, setPassword] = useState('')
  const [allowViewer, setAllowViewer] = useState(viewerEnable)
  const [trainingAiLevel, setTrainingAiLevel] = useState<'Legacy' | 'Advanced'>('Advanced')

  const submit = () => {
    const roomTitle = title.trim().replace(/[&|]/g, '-')
    const roomPassword = password.trim()
    if (isPrivate && !roomPassword) { showError('パスワードを入力してください。'); return }
    onOK({ title: roomTitle, password: roomPassword, isPrivate, viewerEnable: allowViewer, trainingAiLevel })
  }

  return <div className={`majak-mobile-dialog-overlay majak-room-setup-overlay majak-room-setup-overlay--${layoutMode} majak-popup-overlay`} role="presentation">
    <div className="majak-mobile-room-create-dialog majak-room-setup-dialog majak-mobile-dialog-panel majak-popup-panel" role="dialog" aria-modal="true" aria-labelledby="room-create-dialog-title">
      <header id="room-create-dialog-title" className="majak-mobile-dialog-titlebar majak-popup-titlebar"><span>部屋を作る</span><button className="majak-popup-titlebar__close" type="button" onClick={onCancel} aria-label="閉じる">×</button></header>
      <div className="majak-mobile-dialog-body majak-mobile-room-create-body">
        <label className="majak-mobile-dialog-field majak-mobile-dialog-field--wide"><span>部屋の名前</span><select className="majak-mobile-room-title-select" value={title} onChange={event => setTitle(event.target.value)}>{!ROOM_TITLES.includes(title) && <option value={title}>{title}</option>}{ROOM_TITLES.map(roomTitle => <option key={roomTitle} value={roomTitle}>{roomTitle}</option>)}</select></label>
        <fieldset className="majak-mobile-dialog-section"><legend>カギの選択</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--two"><label className="majak-mobile-choice"><input type="radio" name="room-create-private" checked={!isPrivate} onChange={() => setIsPrivate(false)} />かけない</label><label className="majak-mobile-choice"><input type="radio" name="room-create-private" checked={isPrivate} onChange={() => setIsPrivate(true)} />カギ</label></div></fieldset>
        <label className="majak-mobile-dialog-field"><span>パスワード</span><input type="password" value={password} maxLength={PASSWORD_MAX} disabled={!isPrivate} onChange={event => setPassword(event.target.value)} /></label>
        {viewerEnable && <fieldset className="majak-mobile-dialog-section"><legend>観戦者</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--two"><label className="majak-mobile-choice"><input type="radio" name="room-create-viewer" checked={allowViewer} onChange={() => setAllowViewer(true)} />観戦者可</label><label className="majak-mobile-choice"><input type="radio" name="room-create-viewer" checked={!allowViewer} onChange={() => setAllowViewer(false)} />観戦不可</label></div></fieldset>}
        {trainingMode && <fieldset className="majak-mobile-dialog-section"><legend>NPCの強さ</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--two"><label className="majak-mobile-choice"><input type="radio" name="room-create-training-ai" checked={trainingAiLevel === 'Legacy'} onChange={() => setTrainingAiLevel('Legacy')} />標準</label><label className="majak-mobile-choice"><input type="radio" name="room-create-training-ai" checked={trainingAiLevel === 'Advanced'} onChange={() => setTrainingAiLevel('Advanced')} />上級</label></div></fieldset>}
      </div>
      <footer className="majak-mobile-dialog-actions majak-popup-actions"><button type="button" className="majak-standard-dialog__secondary" onClick={onCancel}>キャンセル</button><button type="button" className="majak-standard-dialog__primary is-primary" onClick={submit}>確認</button></footer>
    </div>
  </div>
}
