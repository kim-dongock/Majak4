/**
 * CMJLevelupDlg 相当 — 称号上昇ダイアログ (AP-09 §3-3-2)
 * レガシー: legacy/client/HgMajak2/MJLevelupDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,383,321) → 383×321px, CenterWindow(GetParent())
 * OnNcHitTest: 常に HTCAPTION (全体ドラッグ可能)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 383×321):
 *   mj_sho_window.png  at (0, 0)
 *   以下のテキストは背景に焼き込み済み:
 *     "称号上昇！", "おめでとうございます！", "に上昇しました。",
 *     "以下のマネーが保険金として積み立てられました。"
 *     "麻雀マネー：" / "円"
 *     "積み立てられた保険金は、麻雀マネーが0円になった時、自動補充されます。"
 *
 * 称号文字 (12フレーム 90×30):
 *   mj_sho_moji.png  at (147, 112)  frame = m_nLevel (0〜11)
 *   m_p_imgStrShogo->Draw(&dc, 147, 112, m_nLevel)
 *   フレーム対応: 0=無一文 / 1=ぴよぴよ / 2=金欠 / 3=庶民 / 4=中流 /
 *                5=上流   / 6=富豪     / 7=大富豪 / 8=貴族 / 9=大臣 /
 *                10=王様  / 11=大王様
 *
 * OK ボタン (4フレーム 88×32):
 *   mj_shp_btn_ok.png  at (148, 277)  IDOK
 *
 * ── テキスト (OnPaint — 14px bold MS Pゴシック) ─────────────────────────
 *   m_strLentMoney (積立保険金額):
 *     CRect(161, 205, 309, 219) DT_RIGHT|DT_SINGLELINE|DT_VCENTER
 *     "{amount}円" 相当
 * ────────────────────────────────────────────────────────────────────────
 */
import { useEffect } from 'react'
import { playMajakSfx } from '../../../utils/majakSound'

/** RatingService と同じ GP 資産レベル表示名 */
const LEVEL_NAMES = [
  '無一文',
  '金欠',
  '庶民',
  '平民',
  '一般人',
  '中流',
  '上流',
  '金持ち',
  '富豪',
  '大富豪',
  '財閥',
] as const

interface Props {
  /** RatingService の NLevel: 0〜10 */
  level: number
  /** m_strLentMoney: 積立保険金額 (円) */
  lentMoney: number
  onClose: () => void
}

function formatLentMoney(value: number): string {
  return new Intl.NumberFormat('ja-JP', {
    useGrouping: true,
    maximumFractionDigits: 0,
  }).format(Math.trunc(value))
}

export default function LevelupDlg({ level, lentMoney, onClose }: Props) {
  /** CMJSound::LoadSFX + PlaySFX(SID_ALLCNT) 相当 — レベルアップ SE 再生 */
  useEffect(() => {
    const audio = playMajakSfx('mjklevelup1')
    return () => { audio?.pause() }
  }, [])

  /** NLevel クランプ (0〜10) */
  const nLevel = Math.min(Math.max(0, level), LEVEL_NAMES.length - 1)

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-levelup-dialog" role="dialog" aria-modal="true" aria-labelledby="levelup-dialog-title">
        <header id="levelup-dialog-title" className="majak-popup-titlebar"><span>称号上昇！</span><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-levelup-dialog__body">
          <p>おめでとうございます！</p>
          <strong className="majak-levelup-dialog__level">{LEVEL_NAMES[nLevel]}</strong>
          <p>に上昇しました。</p>
          <dl className="majak-receipt-dialog__details">
            <div><dt>積み立て保険金</dt><dd>{formatLentMoney(lentMoney)} GP</dd></div>
          </dl>
          <p className="majak-levelup-dialog__note">積み立てられた保険金は、麻雀マネーが0円になった時、自動補充されます。</p>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" className="is-primary" onClick={onClose}>OK</button>
        </footer>
      </section>
    </div>
  )
}
