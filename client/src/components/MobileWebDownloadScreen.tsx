const GOOGLE_PLAY_URL = 'https://play.google.com/store/apps/details?id=jp.studio35.majak4'
const APP_STORE_SEARCH_URL = 'https://apps.apple.com/jp/search?term=%E9%BA%BB%E9%9B%804'

export function isAppleTablet() {
  return /iPad/i.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
}

function getStoreLink() {
  const isAppleMobile = /iPhone|iPad|iPod/i.test(navigator.userAgent)
    || isAppleTablet()

  return isAppleMobile
    ? { href: APP_STORE_SEARCH_URL, label: 'App Store でダウンロード' }
    : { href: GOOGLE_PLAY_URL, label: 'Google Play でダウンロード' }
}

type MobileWebDownloadScreenProps = {
  onBackToTop?: () => void
}

export default function MobileWebDownloadScreen({ onBackToTop }: MobileWebDownloadScreenProps) {
  const storeLink = getStoreLink()

  return (
    <main className="majak-mobile-app-download majak-screen-surface" aria-live="polite">
      {onBackToTop && <button type="button" className="majak-mobile-app-download__back" onClick={onBackToTop}>トップへ戻る</button>}
      <div className="majak-mobile-app-download__art" aria-hidden="true">
        <div className="majak-mobile-app-download__sun" />
        <div className="majak-mobile-app-download__tiles"><span>東</span><span>南</span><span>白</span></div>
      </div>
      <div className="majak-mobile-app-download__content">
        <img
          className="majak-mobile-app-download__logo"
          src="/assets/images/common/ico_big_majak4.jpg"
          alt="麻雀4"
          draggable={false}
        />
        <p className="majak-mobile-app-download__eyebrow">MAHJONG 4 APP</p>
        <h1>アプリで、<br />いつでも一局。</h1>
        <p>麻雀4の対局はアプリでお楽しみください。<br />快適なゲーム画面で、すぐに卓へ参加できます。</p>
        <a className="majak-mobile-app-download__store-link" href={storeLink.href} target="_blank" rel="noreferrer">
          <span>{storeLink.label}</span><strong>DOWNLOAD</strong>
        </a>
      </div>
    </main>
  )
}