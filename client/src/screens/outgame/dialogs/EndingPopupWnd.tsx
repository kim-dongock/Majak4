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
    <div className="majak-popup-overlay">
      <div
        className="majak-popup-panel majak-ending-popup"
        role="dialog"
        aria-modal="true"
        aria-labelledby="majak-logout-confirm-title"
      >
        <header id="majak-logout-confirm-title" className="majak-popup-titlebar"><span>ログアウト</span><button className="majak-popup-titlebar__close" type="button" onClick={onCancel} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-ending-popup__body">
          <p>{message}</p>

          {resolvedBannerImageUrl && (
            <a
              href={resolvedBannerLinkUrl}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(event) => {
                if (!resolvedBannerLinkUrl) event.preventDefault()
              }}
              className="majak-ending-popup__banner"
            >
              <img
                src={resolvedBannerImageUrl}
                alt="サービス終了案内"
                draggable={false}
                className="majak-ending-popup__banner-image"
              />
            </a>
          )}

        </div>
        <footer className="majak-popup-actions">
          <button type="button" onClick={onCancel} accessKey="n">キャンセル</button>
          <button ref={confirmButtonRef} type="button" className="is-primary" onClick={onOK} accessKey="y">ログアウト</button>
        </footer>
      </div>
    </div>
  )
}
