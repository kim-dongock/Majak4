/**
 * CMJMiniChannelWnd 相当 — ミニチャンネルウィンドウ (AP-09 §1-7)
 * レガシー:
 *   - legacy/client/HgMajak2/MajakChannelWnd.h/cpp (CMJMiniChannelWnd)
 *   - legacy/client/HgChnlM/cHgMiniChannelWnd.cpp (CHgMiniChannelWnd)
 */
import { useEffect, useRef, useState, type MouseEvent } from 'react'
import * as SignalR from '../../api/signalr'

const MAJAK_IMG = '/assets/images/game'

interface ChatMsg {
  id: string
  name: string
  text: string
  color?: string
}

interface RoomEntry {
  roomId: number
  title: string
  memberCnt: number
  memberMax: number
  isPrivate: boolean
  state?: number
}

interface MemberEntry {
  pix: string
  name: string
  rating: number
  slevel?: string
  location?: string
  roomId?: number
}

interface Props {
  channelId?: string
  members: MemberEntry[]
  rooms?: RoomEntry[]
  slotCount?: number
  fullScreen?: boolean
  compact?: boolean
  scale?: number
  placement?: 'center' | 'bottom'
  onClose: () => void
  onViewProfile?: (pix: string) => void
  onReqGame?: (pix: string) => void
}

function SpriteButton({
  src,
  frameW,
  frameH,
  x,
  y,
  onClick,
  title,
  disabled,
}: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  title?: string
  disabled?: boolean
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  const shownFrame = disabled ? 1 : frameIdx

  return (
    <button
      type="button"
      title={title}
      disabled={disabled}
      onClick={onClick}
      onMouseEnter={() => setFrameIdx(2)}
      onMouseLeave={() => setFrameIdx(0)}
      onMouseDown={() => setFrameIdx(3)}
      onMouseUp={() => setFrameIdx(2)}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: frameW,
        height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-shownFrame * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none',
        padding: 0,
        cursor: disabled ? 'default' : 'pointer',
        outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

function MiniRoomList({
  rooms,
  slotCount,
}: {
  rooms: RoomEntry[]
  slotCount: number
}) {
  const slots = Array.from({ length: slotCount }, (_, index) =>
    rooms.find(room => room.roomId === index + 1) ?? null
  )
  const rows = Math.ceil(slotCount / 4)

  return (
    <div style={{ position: 'absolute', left: 0, top: 0, width: 654, height: 398, overflowY: 'auto', overflowX: 'hidden', background: 'rgb(228,249,176)' }}>
      <div style={{ position: 'relative', width: 654, height: 6 + rows * (133 + 6) }}>
        {slots.map((room, index) => (
          <MiniRoomCell
            key={index}
            slotNo={index + 1}
            room={room}
            x={6 + (150 + 6) * (index % 4)}
            y={6 + (133 + 6) * Math.floor(index / 4)}
          />
        ))}
      </div>
    </div>
  )
}

function MiniRoomCell({
  slotNo,
  room,
  x,
  y,
}: {
  slotNo: number
  room: RoomEntry | null
  x: number
  y: number
}) {
  const tableFrame = room == null ? 0 : (room.state ?? 0) >= 2 ? 2 : 1

  return (
    <div style={{ position: 'absolute', left: x, top: y, width: 150, height: 133 }}>
      <div style={{
        position: 'absolute', left: 0, top: 0,
        width: 150, height: 133,
        backgroundImage: `url(${MAJAK_IMG}/mj_rmimg.png)`,
        backgroundPosition: `${-tableFrame * 150}px 0`,
        backgroundRepeat: 'no-repeat',
        overflow: 'hidden',
        imageRendering: 'pixelated',
      }}>
        <div style={{ position: 'absolute', left: 8, top: 4, width: 116, height: 19, fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))', lineHeight: '19px', color: '#000', overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis' }}>
          {slotNo} : {room?.title ?? ''}
        </div>
        {room?.isPrivate && (
          <img
            src={`${MAJAK_IMG}/mj_rkey.png`}
            alt=""
            draggable={false}
            style={{ position: 'absolute', left: 132, top: 3, width: 12, height: 15, imageRendering: 'pixelated' }}
          />
        )}
      </div>
    </div>
  )
}

function MiniMemberList({
  members,
  selectedPix,
  onSelect,
  onClose,
  onViewProfile,
  onReqGame,
  compact = false,
}: {
  members: MemberEntry[]
  selectedPix: string
  onSelect: (pix: string) => void
  onClose: () => void
  onViewProfile?: (pix: string) => void
  onReqGame?: (pix: string) => void
  compact?: boolean
}) {
  const selectedMember = members.find(member => member.pix === selectedPix)
  const canUseSelected = selectedMember != null
  const width = compact ? 330 : 255
  const height = compact ? 336 : 550
  const listHeight = compact ? 300 : 521
  const idWidth = compact ? 210 : 150
  const titleWidth = compact ? 95 : 70
  const buttonY = compact ? 304 : 521
  const vsButtonX = compact ? 16 : 0
  const profileButtonX = compact ? 124 : 85
  const closeButtonX = compact ? 229 : 170

  return (
    <div style={{ position: 'absolute', left: 0, top: 0, width, height, background: 'rgb(122,185,94)', overflow: 'hidden' }}>
      <div style={{
        position: 'absolute', left: 0, top: 0,
        width, height: listHeight,
        overflowY: 'auto', overflowX: 'hidden',
        background: '#fff',
        border: '2px inset #d4d0c8',
        boxSizing: 'border-box',
      }}>
        <div style={{ display: 'flex', height: 18, alignItems: 'center', background: '#f0f0f0', borderBottom: '1px solid #b8b8b8', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000' }}>
          <span style={{ width: idWidth, paddingLeft: 4, boxSizing: 'border-box' }}>ニックネーム</span>
          <span style={{ width: titleWidth }}>資産</span>
        </div>
        {members.map(member => {
          const selected = selectedPix === member.pix
          return (
            <div
              key={member.pix}
              onClick={() => onSelect(member.pix)}
              onDoubleClick={() => onViewProfile?.(member.pix)}
              style={{
                display: 'flex', alignItems: 'center',
                height: 20,
                fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))',
                background: selected ? '#0a246a' : '#fff',
                color: selected ? '#fff' : '#000',
                cursor: 'pointer',
              }}
            >
              <span style={{ width: idWidth, paddingLeft: 4, overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis', boxSizing: 'border-box' }}>
                {member.name}
              </span>
              <span style={{ width: titleWidth, overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis' }}>
                {member.name || member.slevel || member.rating}
              </span>
            </div>
          )
        })}
      </div>

      <SpriteButton src={`${MAJAK_IMG}/mj_btn_vs.png`} frameW={85} frameH={29} x={vsButtonX} y={buttonY} onClick={() => selectedMember && onReqGame?.(selectedMember.pix)} disabled={!canUseSelected || !onReqGame} title="対戦申込" />
      <SpriteButton src={`${MAJAK_IMG}/mj_btn_profile.png`} frameW={82} frameH={26} x={profileButtonX} y={buttonY} onClick={() => selectedMember && onViewProfile?.(selectedMember.pix)} disabled={!canUseSelected || !onViewProfile} title="プロフィール" />
      <SpriteButton src={`${MAJAK_IMG}/mj_btn_cancel.png`} frameW={85} frameH={29} x={closeButtonX} y={buttonY} onClick={onClose} title="閉じる" />
    </div>
  )
}

export default function MiniChannelWnd({
  channelId,
  members,
  rooms = [],
  slotCount = 12,
  fullScreen = false,
  compact = false,
  scale = 1,
  placement = 'center',
  onClose,
  onViewProfile,
  onReqGame,
}: Props) {
  const [chatLog, setChatLog] = useState<ChatMsg[]>([])
  const [chatText, setChatText] = useState('')
  const [selectedPix, setSelectedPix] = useState(members[0]?.pix ?? '')
  const chatRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      setChatLog(prev => {
        const next = [
          ...prev,
          {
            id: `${Date.now()}-${prev.length}`,
            name: String(data.k3e ?? data.pix ?? ''),
            text: String(data.k41e ?? data.string ?? ''),
          },
        ]
        return next.length > 80 ? next.slice(next.length - 80) : next
      })
    }
    SignalR.on('hc1e', handler)
    return () => SignalR.off('hc1e', handler)
  }, [])

  useEffect(() => {
    if (!members.some(member => member.pix === selectedPix)) {
      setSelectedPix(members[0]?.pix ?? '')
    }
  }, [members, selectedPix])

  useEffect(() => {
    if (chatRef.current) chatRef.current.scrollTop = chatRef.current.scrollHeight
  }, [chatLog])

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onClose])

  const sendChat = async () => {
    const text = chatText.trim()
    if (!text) return
    setChatText('')
    await SignalR.send('hc1e', { k41e: text, string: text }).catch(() => {})
  }

  const stopPopupMouseDown = (event: MouseEvent) => {
    event.stopPropagation()
  }

  const compactFullScreen = fullScreen && compact
  const width = compactFullScreen ? 336 : fullScreen ? 261 : 915
  const height = compactFullScreen ? 346 : 575
  const title = fullScreen ? '観戦' : 'ロビー'
  const bottomPlacement = placement === 'bottom'

  return (
    <div
      role="dialog"
      aria-label={`${title}${channelId ? ` ${channelId}` : ''}`}
      onMouseDown={stopPopupMouseDown}
      style={{
        position: 'fixed',
        left: '50%',
        top: bottomPlacement ? undefined : '50%',
        bottom: bottomPlacement ? 'max(8px, env(safe-area-inset-bottom))' : undefined,
        transform: bottomPlacement ? `translateX(-50%) scale(${scale})` : `translate(-50%, -50%) scale(${scale})`,
        transformOrigin: bottomPlacement ? 'bottom center' : 'center center',
        width,
        height,
        background: 'rgb(156,156,156)',
        border: '2px outset #d4d0c8',
        boxSizing: 'border-box',
        zIndex: 200,
        overflow: 'hidden',
        fontFamily: 'var(--majak-font-family-ui)',
      }}
    >
      <div style={{ position: 'absolute', left: fullScreen ? 0 : 654, top: 0, width: compactFullScreen ? 330 : 255, height: compactFullScreen ? 336 : 550 }}>
        <MiniMemberList
          members={members}
          selectedPix={selectedPix}
          onSelect={setSelectedPix}
          onClose={onClose}
          onViewProfile={onViewProfile}
          onReqGame={onReqGame}
          compact={compactFullScreen}
        />
      </div>

      {!fullScreen && (
        <>
          <MiniRoomList rooms={rooms} slotCount={slotCount} />

          <div style={{ position: 'absolute', left: 0, top: 400, width: 654, height: 150, background: '#fff', border: '2px inset #d4d0c8', boxSizing: 'border-box', overflow: 'hidden' }}>
            <div
              ref={chatRef}
              style={{
                position: 'absolute', left: 0, top: 0,
                width: 650, height: 126,
                overflowY: 'auto', overflowX: 'hidden',
                fontFamily: 'var(--majak-font-family-ui)',
                fontSize: 'calc(12px * var(--majak-type-scale))',
                color: '#000',
                background: '#fff',
                boxSizing: 'border-box',
              }}
            >
              {chatLog.map(message => (
                <div key={message.id} style={{ color: message.color ?? '#000', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  [{message.name}] {message.text}
                </div>
              ))}
            </div>
            <input
              value={chatText}
              onChange={event => setChatText(event.target.value)}
              onKeyDown={event => { if (event.key === 'Enter') sendChat() }}
              maxLength={80}
              style={{
                position: 'absolute', left: 0, top: 126,
                width: 630, height: 20,
                border: '2px inset #d4d0c8',
                boxSizing: 'border-box',
                outline: 'none',
                padding: '0 2px',
                fontFamily: 'var(--majak-font-family-ui)',
                fontSize: 'calc(12px * var(--majak-type-scale))',
                color: '#000',
                background: '#fff',
              }}
            />
            <button
              type="button"
              aria-label="chat color"
              style={{
                position: 'absolute', left: 630, top: 126,
                width: 20, height: 20,
                border: '2px inset #d4d0c8',
                boxSizing: 'border-box',
                padding: 0,
                background: '#000',
              }}
            />
          </div>
        </>
      )}
    </div>
  )
}
