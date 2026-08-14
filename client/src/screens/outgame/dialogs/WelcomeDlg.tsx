/**
 * CMJWelcomeDlg 相当 — ウェルカムメッセージ (AP-09 §3-1-3)
 * レガシー: legacy/client/HgMajak2/MJWelcomeDlg.h/cpp
 *
 * ウィンドウサイズ: 495×470px (MoveWindow(rc.left+263, rc.top+136, 495, 470))
 * Web 版では背景画像ではなく、共通ポップアップ配色のテキスト案内を表示する。
 *
 * OnNcHitTest → HTCAPTION: ドラッグ移動可能、システム X ボタンなし
 * ゲーム妨害防止: × ボタン以外での閉じ操作不可
 */
interface Props {
  onClose: () => void
}

export default function WelcomeDlg({ onClose }: Props) {
  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-welcome-dialog" role="dialog" aria-modal="true" aria-labelledby="welcome-dialog-title">
        <header id="welcome-dialog-title" className="majak-popup-titlebar"><span>ようこそ</span><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-welcome-dialog__body">
          <p className="majak-welcome-dialog__lead">麻雀4へようこそ。</p>
          <p>オンライン麻雀をお楽しみください。</p>
          <div className="majak-welcome-dialog__notice">
            <strong>対局を始める準備ができました。</strong>
            <span>チャンネルを選択して、参加する卓をお選びください。</span>
          </div>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" className="is-primary" onClick={onClose}>閉じる</button>
        </footer>
      </section>
    </div>
  )
}
