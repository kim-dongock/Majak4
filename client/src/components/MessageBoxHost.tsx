import { useEffect, useRef, useState } from 'react'
import { resolveMessageBox, subscribeMessageBox, type MessageBoxRequest } from '../utils/msgbox'

const FONT = 'var(--majak-font-family-ui)'

export default function MessageBoxHost() {
  const [requests, setRequests] = useState<MessageBoxRequest[]>([])
  const okButtonRef = useRef<HTMLButtonElement>(null)
  const request = requests[0]

  useEffect(() => {
    return subscribeMessageBox(setRequests)
  }, [])

  useEffect(() => {
    if (!request) return
    okButtonRef.current?.focus()
  }, [request])

  useEffect(() => {
    if (!request) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Enter') {
        event.preventDefault()
        resolveMessageBox(request.id, true)
      }
      if (event.key === 'Escape' && request.kind === 'confirm') {
        event.preventDefault()
        resolveMessageBox(request.id, false)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [request])

  if (!request) return null

  const messageLines = request.message.split(/\r?\n/)
  const isConfirm = request.kind === 'confirm'

  const buttonStyle: React.CSSProperties = {
    minWidth: 96,
    height: 36,
    padding: '0 18px',
    fontFamily: FONT,
    fontSize: 'var(--majak-font-13)',
    fontWeight: 700,
    color: '#fff',
    background: '#1f6f5b',
    border: 0,
    borderRadius: 6,
    boxShadow: '0 8px 18px rgba(31, 111, 91, 0.22)',
    cursor: 'pointer',
  }

  const secondaryButtonStyle: React.CSSProperties = {
    ...buttonStyle,
    color: '#26322f',
    background: '#eef5f2',
    boxShadow: 'none',
    border: '1px solid #c8d8d2',
  }

  return (
    <div
      role="presentation"
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
        boxSizing: 'border-box',
        background: 'rgba(16, 24, 22, 0.48)',
        backdropFilter: 'blur(3px)',
        fontFamily: FONT,
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="majak-messagebox-title"
        style={{
          width: 'min(420px, 100%)',
          maxHeight: 'calc(100vh - 32px)',
          overflow: 'auto',
          color: '#1f2926',
          background: '#fffdf8',
          border: '1px solid rgba(31, 111, 91, 0.14)',
          borderRadius: 8,
          boxShadow: '0 22px 58px rgba(11, 28, 23, 0.28)',
        }}
      >
        <div style={{
          display: 'grid',
          gridTemplateColumns: '44px minmax(0, 1fr)',
          columnGap: 14,
          padding: '22px 22px 16px',
          boxSizing: 'border-box',
        }}>
          <div style={{
            width: 44,
            height: 44,
            borderRadius: 8,
            color: isConfirm ? '#1f6f5b' : '#b43a32',
            background: isConfirm ? '#e4f3ee' : '#fae9e6',
            textAlign: 'center',
            lineHeight: '44px',
            fontWeight: 700,
            fontSize: 'var(--majak-font-24)',
          }}>
            {isConfirm ? '?' : '!'}
          </div>
          <div style={{ minWidth: 0 }}>
            <div
              id="majak-messagebox-title"
              style={{
                marginBottom: 8,
                color: '#18231f',
                fontSize: 'var(--majak-font-16)',
                lineHeight: '22px',
                fontWeight: 700,
                overflowWrap: 'anywhere',
              }}
            >
              {request.title}
            </div>
            <div style={{
              color: '#44514d',
              fontSize: 'var(--majak-font-13)',
              lineHeight: '20px',
              whiteSpace: 'pre-wrap',
              overflowWrap: 'anywhere',
            }}>
              {messageLines.map((line, index) => (
                <div key={index}>{line || '\u00a0'}</div>
              ))}
            </div>
          </div>
        </div>
        <div style={{
          display: 'flex',
          justifyContent: 'flex-end',
          flexWrap: 'wrap',
          gap: 8,
          padding: '0 22px 22px 80px',
        }}>
          <button ref={okButtonRef} type="button" style={buttonStyle} onClick={() => resolveMessageBox(request.id, true)}>
            {request.confirmLabel ?? 'OK'}
          </button>
          {isConfirm && (
            <button type="button" style={secondaryButtonStyle} onClick={() => resolveMessageBox(request.id, false)}>
              {request.cancelLabel ?? 'キャンセル'}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}