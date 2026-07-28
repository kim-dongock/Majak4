import { useEffect, useRef } from 'react'

interface Props {
  message?: string
  bannerImageUrl?: string
  bannerLinkUrl?: string
  bannerUrl?: string
  onOK: () => void
  onCancel: () => void
}

export default function EndingPopupWnd({
  message = 'ログアウトしてログイン画面に戻りますか？',
  bannerImageUrl,
  bannerLinkUrl,
  bannerUrl,
  onOK,
  onCancel,
}: Props) {
  const resolvedBannerImageUrl = bannerImageUrl ?? bannerUrl
  const resolvedBannerLinkUrl = bannerLinkUrl ?? bannerUrl
  const confirmButtonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    confirmButtonRef.current?.focus()
  }, [])

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented) return
      const target = event.target
      if (target instanceof HTMLButtonElement || target instanceof HTMLAnchorElement) return

      if (event.key === 'Enter' || (event.altKey && event.key.toLowerCase() === 'y')) {
        event.preventDefault()
        onOK()
      } else if (event.key === 'Escape' || (event.altKey && event.key.toLowerCase() === 'n')) {
        event.preventDefault()
        onCancel()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onCancel, onOK])

  return (
    <div
      style={{
        position: 'absolute',
        inset: 0,
        zIndex: 1000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
        boxSizing: 'border-box',
        background: 'rgba(8, 24, 15, 0.68)',
        backdropFilter: 'blur(4px)',
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="majak-logout-confirm-title"
        style={{
          width: 'min(390px, 100%)',
          overflow: 'hidden',
          border: '1px solid rgba(88, 142, 89, 0.38)',
          borderRadius: 8,
          color: '#21332a',
          background: '#fbfcf7',
          boxShadow: '0 24px 64px rgba(0, 0, 0, 0.42)',
          fontFamily: "'Noto Sans JP', 'Meiryo', 'MS PGothic', sans-serif",
        }}
      >
        <div style={{ height: 6, background: '#2d7b49' }} />
        <div style={{ padding: '24px 24px 22px' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '48px minmax(0, 1fr)', columnGap: 15, alignItems: 'start' }}>
            <div
              aria-hidden="true"
              style={{
                width: 48,
                height: 48,
                display: 'grid',
                placeItems: 'center',
                borderRadius: 8,
                color: '#1d6e41',
                background: '#e3f2e7',
                fontSize: 26,
                fontWeight: 700,
                lineHeight: 1,
                userSelect: 'none',
              }}
            >
              ?
            </div>
            <div style={{ minWidth: 0, paddingTop: 2 }}>
              <div id="majak-logout-confirm-title" style={{ marginBottom: 7, color: '#1c3024', fontSize: 18, fontWeight: 700, lineHeight: '24px' }}>
                ログアウト
              </div>
              <div style={{ color: '#52635a', fontSize: 13, lineHeight: '20px', overflowWrap: 'anywhere' }}>
                {message}
              </div>
            </div>
          </div>

          {resolvedBannerImageUrl && (
            <a
              href={resolvedBannerLinkUrl}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(event) => {
                if (!resolvedBannerLinkUrl) event.preventDefault()
              }}
              style={{ display: 'block', marginTop: 18 }}
            >
              <img
                src={resolvedBannerImageUrl}
                alt="サービス終了案内"
                draggable={false}
                style={{ display: 'block', width: '100%', maxHeight: 110, objectFit: 'contain' }}
              />
            </a>
          )}

          <div style={{ display: 'flex', justifyContent: 'flex-end', flexWrap: 'wrap-reverse', gap: 8, marginTop: 24 }}>
            <button
              type="button"
              onClick={onCancel}
              accessKey="n"
              style={{
                minWidth: 104,
                height: 38,
                padding: '0 16px',
                border: '1px solid #cad9cf',
                borderRadius: 6,
                color: '#34443b',
                background: '#f4f7f3',
                font: 'inherit',
                fontSize: 13,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              キャンセル
            </button>
            <button
              ref={confirmButtonRef}
              type="button"
              onClick={onOK}
              accessKey="y"
              style={{
                minWidth: 104,
                height: 38,
                padding: '0 16px',
                border: '1px solid #1c6339',
                borderRadius: 6,
                color: '#fff',
                background: '#267544',
                boxShadow: '0 7px 16px rgba(30, 96, 55, 0.22)',
                font: 'inherit',
                fontSize: 13,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              ログアウト
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
