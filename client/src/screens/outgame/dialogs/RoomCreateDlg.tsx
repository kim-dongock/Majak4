/**
 * CHgMajakChannelRoomOptionDialog / CHgChannelRoomOptionDialog 相当 — ルーム作成情報
 * レガシー:
 *   - legacy/client/HgMajak2/MajakChannelWnd.cpp ShowCreateRoomOptionDialog()
 *   - legacy/client/HgChnlM/HgChannelRoomOptionDialog.cpp OnInitDialog()/OnOK()
 *   - legacy/client/HgChnlM/HgChnlJpn.rc IDD_CHANNEL_ROOMOPTION
 */
import { useEffect, useRef, useState } from 'react'
import { showError } from '../../../utils/msgbox'

const SX = (du: number) => Math.round(du * 1.5)
const SY = (du: number) => Math.round(du * 1.625)
const FONT = 'var(--majak-font-family-ui)'
const DLG_BG = '#d4d0c8'

export interface RoomCreateInfo {
  title: string
  password: string
  isPrivate: boolean
  viewerEnable: boolean
}

interface Props {
  initialTitle: string
  viewerEnable?: boolean
  onOK: (info: RoomCreateInfo) => void
  onCancel: () => void
}

const ROOM_TITLE_MAX = 32
const PASSWORD_MAX = 8
const RANDOM_ROOM_TITLES = [
  'いらっしゃいませ。',
  '勝負だ！',
  '気軽にどうぞ～',
]
const ROOM_TITLES = [
  ...RANDOM_ROOM_TITLES,
  'あいさつしよう！',
  '仲間同士でわいわい♪',
  '☆チャットしながら…',
  '☆楽しくネ！',
  '(^-^*)自由だよ♪',
  '☆勝っても負けても',
  'さくさく',
  'つわもの募集',
  '■初心者部屋■',
  '■無言部屋■',
]

export default function RoomCreateDlg({ initialTitle, viewerEnable = true, onOK, onCancel }: Props) {
  const [title, setTitle] = useState(() => initialTitle || RANDOM_ROOM_TITLES[Math.floor(Math.random() * RANDOM_ROOM_TITLES.length)])
  const [isPrivate, setIsPrivate] = useState(false)
  const [password, setPassword] = useState('')
  const [allowViewer, setAllowViewer] = useState(viewerEnable)
  const [titleListOpen, setTitleListOpen] = useState(false)
  const titleInputRef = useRef<HTMLInputElement>(null)
  const titleComboRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    titleInputRef.current?.focus()
    titleInputRef.current?.select()
  }, [])

  useEffect(() => {
    const onPointerDown = (event: PointerEvent) => {
      if (!titleComboRef.current?.contains(event.target as Node)) {
        setTitleListOpen(false)
      }
    }
    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [])

  const submit = () => {
    const roomTitle = title.trim().replace(/[&|]/g, '-')
    const roomPassword = password.trim()
    if (isPrivate && !roomPassword) {
      showError('パスワードを入力してください。')
      return
    }
    onOK({ title: roomTitle, password: roomPassword, isPrivate, viewerEnable: allowViewer })
  }

  {
    return (
      <div className="majak-mobile-dialog-overlay">
        <div className="majak-mobile-room-create-dialog majak-mobile-dialog-panel">
          <div className="majak-mobile-dialog-titlebar">部屋を作る</div>
          <div className="majak-mobile-dialog-body majak-mobile-room-create-body">
            <label className="majak-mobile-dialog-field majak-mobile-dialog-field--wide">
              <span>部屋の名前</span>
              <select
                  className="majak-mobile-room-title-select"
                  value={title}
                  onChange={event => setTitle(event.target.value)}
                >
                  {!ROOM_TITLES.includes(title) && <option value={title}>{title}</option>}
                  {ROOM_TITLES.map(roomTitle => <option key={roomTitle} value={roomTitle}>{roomTitle}</option>)}
              </select>
            </label>

            <fieldset className="majak-mobile-dialog-section">
              <legend>カギの選択</legend>
              <div className="majak-mobile-choice-grid majak-mobile-choice-grid--two">
                <label className="majak-mobile-choice"><input type="radio" name="room-create-private-mobile" checked={!isPrivate} onChange={() => setIsPrivate(false)} />かけない</label>
                <label className="majak-mobile-choice"><input type="radio" name="room-create-private-mobile" checked={isPrivate} onChange={() => setIsPrivate(true)} />カギ</label>
              </div>
            </fieldset>

            <label className="majak-mobile-dialog-field">
              <span>パスワード</span>
              <input
                type="password"
                value={password}
                maxLength={PASSWORD_MAX}
                disabled={!isPrivate}
                onChange={event => setPassword(event.target.value)}
              />
            </label>

            {viewerEnable && (
              <fieldset className="majak-mobile-dialog-section">
                <legend>観戦者</legend>
                <div className="majak-mobile-choice-grid majak-mobile-choice-grid--two">
                  <label className="majak-mobile-choice"><input type="radio" name="room-create-viewer-mobile" checked={allowViewer} onChange={() => setAllowViewer(true)} />観戦者可</label>
                  <label className="majak-mobile-choice"><input type="radio" name="room-create-viewer-mobile" checked={!allowViewer} onChange={() => setAllowViewer(false)} />観戦不可</label>
                </div>
              </fieldset>
            )}
          </div>
          <div className="majak-mobile-dialog-actions">
            <button type="button" onClick={submit}>OK</button>
            <button type="button" onClick={onCancel}>キャンセル</button>
          </div>
        </div>
      </div>
    )
  }

  const px = SX
  const py = SY
  const dlgW = px(147)
  const titleH = 24
  const viewerHeight = viewerEnable ? py(24) : 0
  const contentH = py(193) - 115 + viewerHeight
  const dlgH = titleH + contentH
  const viewerTop = py(165) - 115

  const btnStyle: React.CSSProperties = {
    position: 'absolute', fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111',
    background: '#f4f4f4',
    borderTop: '1px solid #fff', borderLeft: '1px solid #fff',
    borderRight: '1px solid #9a9a9a', borderBottom: '1px solid #9a9a9a',
    cursor: 'pointer', outline: 'none',
  }

  const groupStyle: React.CSSProperties = {
    position: 'absolute',
    border: '1px solid #b8b8b8',
    borderTopColor: '#a9a9a9',
    margin: 0,
    padding: 0,
    boxSizing: 'border-box',
  }

  const legendStyle: React.CSSProperties = {
    fontFamily: FONT,
    fontSize: 'calc(12px * var(--majak-type-scale))',
    color: '#111',
    lineHeight: '14px',
    padding: '0 4px',
    marginLeft: 6,
  }

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 210,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.25)',
    }}>
      <div style={{
        position: 'relative',
        width: dlgW,
        height: dlgH,
        overflow: 'hidden',
        background: DLG_BG,
        border: '1px solid #808080',
        boxShadow: '3px 3px 8px rgba(0,0,0,0.45)',
      }}>
        <div style={{
          height: titleH,
          background: '#f0f0f0',
          display: 'flex', alignItems: 'center', paddingLeft: 8,
        }}>
          <span style={{ fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111' }}>部屋を作る</span>
        </div>

        <div style={{ position: 'relative', width: dlgW, height: contentH, background: DLG_BG }}>
          <fieldset style={{ ...groupStyle, left: px(4), top: py(5), width: px(138), height: py(33) }}>
            <legend style={legendStyle}>部屋の名前</legend>
          </fieldset>
          <div ref={titleComboRef} style={{ position: 'absolute', left: px(8), top: py(18), width: px(132), height: py(14), zIndex: 5 }}>
            <input
              ref={titleInputRef}
              value={title}
              maxLength={ROOM_TITLE_MAX}
              onChange={e => setTitle(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter') submit()
                if (e.key === 'Escape') {
                  if (titleListOpen) setTitleListOpen(false)
                  else onCancel()
                }
              }}
              style={{
                position: 'absolute', left: 0, top: 0, width: px(132) - 18, height: py(14),
                fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', boxSizing: 'border-box', padding: '0 2px',
                border: '1px inset #d4d0c8', background: '#fff', color: '#111', outline: 'none',
              }}
            />
            <button
              type="button"
              onMouseDown={e => e.preventDefault()}
              onClick={() => {
                setTitleListOpen(v => !v)
                titleInputRef.current?.focus()
              }}
              style={{
                position: 'absolute', right: 0, top: 0, width: 18, height: py(14), padding: 0,
                fontFamily: FONT, fontSize: 'calc(9px * var(--majak-type-scale))', lineHeight: `${py(14) - 2}px`, color: '#111',
                background: '#f4f4f4', borderTop: '1px solid #fff', borderLeft: '1px solid #fff',
                borderRight: '1px solid #777', borderBottom: '1px solid #777', cursor: 'pointer',
              }}
            >▼</button>
            {titleListOpen && (
              <div style={{
                position: 'absolute', left: 0, top: py(14), width: px(132), maxHeight: py(70),
                overflowY: 'auto', background: '#fff', border: '1px solid #777', boxSizing: 'border-box',
                fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111', zIndex: 10,
              }}>
                {ROOM_TITLES.map(roomTitle => (
                  <div
                    key={roomTitle}
                    onMouseDown={e => e.preventDefault()}
                    onClick={() => {
                      setTitle(roomTitle.slice(0, ROOM_TITLE_MAX))
                      setTitleListOpen(false)
                      titleInputRef.current?.focus()
                    }}
                    style={{ height: 18, lineHeight: '18px', padding: '0 3px', cursor: 'default', whiteSpace: 'nowrap' }}
                  >{roomTitle}</div>
                ))}
              </div>
            )}
          </div>

          <fieldset style={{ ...groupStyle, left: px(4), top: py(41), width: px(138), height: py(56) - 18 }}>
            <legend style={legendStyle}>カギの選択</legend>
          </fieldset>
          <label style={{ position: 'absolute', left: px(9), top: py(54), display: 'flex', alignItems: 'center', gap: 4, fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111', cursor: 'pointer' }}>
            <input type="radio" name="room-create-private" checked={!isPrivate} onChange={() => setIsPrivate(false)} style={{ margin: 0, width: 13, height: 13 }} />
            かけない
          </label>
          <label style={{ position: 'absolute', left: px(9), top: py(67), display: 'flex', alignItems: 'center', gap: 4, fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111', cursor: 'pointer' }}>
            <input type="radio" name="room-create-private" checked={isPrivate} onChange={() => setIsPrivate(true)} style={{ margin: 0, width: 13, height: 13 }} />
            カギ
          </label>
          <span style={{ position: 'absolute', left: px(53), top: 102, fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111' }}>パスワード</span>
          <input
            type="password"
            value={password}
            maxLength={PASSWORD_MAX}
            disabled={!isPrivate}
            onChange={e => setPassword(e.target.value)}
            style={{
              position: 'absolute', left: px(89), top: 98, width: px(46), height: 21,
              fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', boxSizing: 'border-box', padding: '0 2px',
              border: '1px inset #d4d0c8', background: isPrivate ? '#fff' : '#eee', color: '#111', outline: 'none',
            }}
          />

          {viewerEnable && (
            <fieldset style={{ ...groupStyle, left: px(4), top: viewerTop, width: px(138), height: py(24) }}>
              <legend style={legendStyle}>観戦者</legend>
              <label style={{ position: 'absolute', left: px(8), top: 4, display: 'flex', alignItems: 'center', gap: 4, fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111', cursor: 'pointer' }}>
                <input type="radio" name="room-create-viewer" checked={allowViewer} onChange={() => setAllowViewer(true)} style={{ margin: 0, width: 13, height: 13 }} />
                観戦者可
              </label>
              <label style={{ position: 'absolute', left: px(83), top: 4, display: 'flex', alignItems: 'center', gap: 4, fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111', cursor: 'pointer' }}>
                <input type="radio" name="room-create-viewer" checked={!allowViewer} onChange={() => setAllowViewer(false)} style={{ margin: 0, width: 13, height: 13 }} />
                観戦不可
              </label>
            </fieldset>
          )}

          <button onClick={submit} style={{ ...btnStyle, left: px(20), top: py(169) - 115 + viewerHeight, width: px(50), height: py(17) }}>OK</button>
          <button onClick={onCancel} style={{ ...btnStyle, left: px(75), top: py(169) - 115 + viewerHeight, width: px(50), height: py(17) }}>キャンセル</button>
        </div>
      </div>
    </div>
  )
}