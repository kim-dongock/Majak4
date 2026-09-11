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

export default function MobileWebDownloadScreen() {
  const storeLink = getStoreLink()

  return (
    <main className="majak-mobile-app-download majak-screen-surface" aria-live="polite">
      <img
        className="majak-mobile-app-download__logo"
        src="/assets/images/common/ico_big_majak4.jpg"
        alt="麻雀4"
        draggable={false}
      />
      <h1>麻雀4 アプリをダウンロード</h1>
      <p>快適にプレイするには、麻雀4アプリをご利用ください。</p>
      <a className="majak-mobile-app-download__store-link" href={storeLink.href} target="_blank" rel="noreferrer">
        {storeLink.label}
      </a>
    </main>
  )
}