/**
 * CMJLotResultDlg 相当 — 抽選結果ダイアログ (AP-09 §3-2-12)
 * レガシー: legacy/client/HgMajak2/MJLotResultDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,301,333) → 301×333px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 40 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 全画像は lot/ サブフォルダに格納
 *
 * 背景 (1フレーム 301×333):
 *   lot/lot_base2.png  at (0, 0)
 *
 * 再購入ボタン (4フレーム 72×42):
 *   lot/lot_t_btn_4.png  at (139, 274)  IDYES → OnBtnBuy
 *   ボタン上に次回抽選回数テキスト (SetTextString 相当)
 *
 * 閉じるボタン (4フレーム 72×42):
 *   lot/lot_t_btn_3.png  at (215, 274)  IDNO → OnBtnClose
 *
 * 左矢印ボタン (4フレーム 9×14):
 *   lot/lot_btn_mark_l.png  at (193, 254)  IDC_BTN_LARROW → OnBtnLArrowClicked
 *
 * 右矢印ボタン (4フレーム 9×14):
 *   lot/lot_btn_mark_r.png  at (277, 254)  IDC_BTN_RARROW → OnBtnRArrowClicked
 *
 * ── テキスト (Draw() — 13px bold MS Pゴシック, 透過背景) ──────────────────
 *   合計当選金額 (緑 RGB(6,65,2)):
 *     makeMoneyString(total)  CRect(80,54,222,67)  DT_CENTER
 *   タイトル (白):
 *     m_strGetTotalMoney      CRect(30,34,271,47)  DT_CENTER
 *       "{itemName}回の合計当選金額"
 *   再購入メッセージ (白):
 *     m_strBuyAgain           CRect(12,275,143,316) DT_CENTER
 *
 * ── データ表示エリア (DrawPageText — 1ページ10件) ─────────────────────────
 *   回数データ:
 *     CNTDATA_CORNER_POS_X=46, Y=94, WIDTH=50, HEIGHT=13, 16px間隔
 *     CRect(46, 94+16*i, 96, 107+16*i)  ← 1列目 (i=0..9)
 *   金額データ:
 *     MONEYDATA_CORNER_POS_X=130, WIDTH=142, HEIGHT=13
 *     CRect(130, 94+16*i, 272, 107+16*i)  ← 1列目 (i=0..9)
 *   ページ表示:
 *     s_rcPage CRect(190,250,290,270)  "{curPage}/{maxPage}"
 * ────────────────────────────────────────────────────────────────────────
 */
import { useState } from 'react'

/** 抽選1件の結果 */
export interface LotEntry {
  seq: number     // 回数 (1, 2, 3...)
  amount: number  // 当選金額
}

interface Props {
  itemName: string
  lotteryCount: number      // m_pShopItemData->m_nLotteryCount
  entries: LotEntry[]
  totalAmount: number       // CRandomDiv::Instance()->GetTotalValue() 相当
  nextLotteryCount: number  // 次の抽選回数 (m_pShopItemData->GetNextItemData()->GetLotteryCount())
  onBuyAgain: () => void
  onClose: () => void
}

const DATA_PER_PAGE = 10  // DATA_OF_EVERY_1PAGE

/** ====================================================================
 * CMJLotResultDlg 本体
 * ==================================================================== */
export default function LotResultDlg({
  itemName, lotteryCount, entries, totalAmount,
  nextLotteryCount, onBuyAgain, onClose,
}: Props) {
  const [curPage, setCurPage] = useState(1)
  const maxPage = Math.max(1, Math.ceil(lotteryCount / DATA_PER_PAGE))

  /* 再購入メッセージ (m_strBuyAgain 相当) */
  const buyAgainMsg = nextLotteryCount !== lotteryCount
    ? `※再購入すると抽選回数が${lotteryCount}回から${nextLotteryCount}回に増える!`
    : ''

  const makeMoneyString = (value: number, addCurrency = true) => {
    return `${Math.trunc(value).toLocaleString('ja-JP')}${addCurrency ? ' GP' : ''}`
  }

  /* 現在ページのデータ */
  const pageEntries = entries.slice((curPage - 1) * DATA_PER_PAGE, curPage * DATA_PER_PAGE)

  return (
    <div className="majak-popup-overlay majak-lottery-result-overlay" role="presentation">
      <section className="majak-lottery-result-panel" role="dialog" aria-modal="true" aria-label={`${itemName} 抽選結果`}>

        {/* ================================================================
            背景: lot/lot_base2.png (301×333) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            ================================================================ */}
        {/* ================================================================
            テキスト (Draw() 相当)
            ================================================================ */}

        <header className="majak-lottery-result-panel__header"><strong>{itemName} 抽選結果</strong><button type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <section className="majak-lottery-result-panel__total"><span>合計獲得金額</span><strong>{makeMoneyString(totalAmount)}</strong></section>

        {/* ================================================================
            データ表示エリア (DrawPageText 相当)
            1ページ10件:
              回数データ:  CRect(46, 94+16*i, 96, 107+16*i)
              金額データ:  CRect(130, 94+16*i, 272, 107+16*i)
            ================================================================ */}
        {pageEntries.map(e => (
          <div key={e.seq} className="majak-lottery-result-panel__entry">
            {/* 回数 CNTDATA_CORNER_POS_X=46, Y=94+16*i, WIDTH=50, HEIGHT=13 */}
            <div>
              {e.seq}回目
            </div>
            {/* 金額 MONEYDATA_CORNER_POS_X=130, Y=94+16*i, WIDTH=142, HEIGHT=13 */}
            <div>
              {makeMoneyString(e.amount)}
            </div>
          </div>
        ))}

        {/* ================================================================
            ページ表示 s_rcPage CRect(190,250,290,270) 中央
            ================================================================ */}
        <div className="majak-lottery-result-panel__page">
          {curPage} / {maxPage}
        </div>

        {/* 再購入メッセージ CRect(12,275,143,316) DT_CENTER 白 */}
        {buyAgainMsg && (
          <div className="majak-lottery-result-panel__note">
            {buyAgainMsg}
          </div>
        )}

        {/* ================================================================
            ← 矢印: lot/lot_btn_mark_l.png (36×14, 4フレーム 9×14) at (193,254)
            m_btnArrowL.Create(0, ..., 193, 254, ..., IDC_BTN_LARROW)
            ================================================================ */}
        <footer className="majak-lottery-result-panel__actions"><button type="button" onClick={() => setCurPage(p => Math.max(1, p - 1))} disabled={curPage === 1}>前へ</button><button type="button" onClick={() => setCurPage(p => Math.min(maxPage, p + 1))} disabled={curPage === maxPage}>次へ</button><button type="button" className="is-buy" onClick={onBuyAgain}>次回 {nextLotteryCount}回で再購入</button><button type="button" onClick={onClose}>閉じる</button></footer>
      </section>
      <style>{`
        .majak-lottery-result-panel { width: min(760px, calc(100dvw - 32px)); max-height: calc(100dvh - 32px); overflow: auto; border: 2px solid #d9bc62; border-radius: 7px; color: #f8f6e9; background: #123d31; box-shadow: 0 22px 55px rgba(0,0,0,.55); }.majak-lottery-result-panel__header { display: flex; align-items: center; gap: 14px; padding: 15px 20px; background: linear-gradient(90deg,#1b5a4b,#24705b 48%,#1b5a4b); border-bottom: 2px solid #d9bc62; }.majak-lottery-result-panel__header strong { font: 700 var(--majak-popup-font-title)/var(--majak-popup-leading-title) var(--majak-font-family-ui); }.majak-lottery-result-panel__header button { width:28px; height:28px; margin-left:auto; border:1px solid #d9bc62; border-radius:4px; color:#f8f6e9; background:#1c6b58; font-size:20px; cursor:pointer; }.majak-lottery-result-panel__header button:hover, .majak-lottery-result-panel__header button:focus-visible { background:#247c67; }.majak-lottery-result-panel__total { display: flex; align-items: baseline; gap: 20px; padding: 17px 24px; background: rgba(5,31,23,.42); }.majak-lottery-result-panel__total span { color: #d7e3d7; font-size: var(--majak-popup-font-body); line-height: var(--majak-popup-leading-body); }.majak-lottery-result-panel__total strong { color: #d9bc62; font: 700 var(--majak-popup-font-title)/var(--majak-popup-leading-title) var(--majak-font-family-ui); }.majak-lottery-result-panel__entry { display:grid; grid-template-columns: 100px 1fr; gap: 24px; padding: 11px 24px; border-bottom: 1px solid rgba(215,227,215,.15); color:#d7e3d7; font-size:var(--majak-popup-font-body); font-weight:700; line-height:var(--majak-popup-leading-body); }.majak-lottery-result-panel__entry div:last-child { color:#d9bc62; text-align:right; font-size:var(--majak-popup-font-emphasis); line-height:var(--majak-popup-leading-emphasis); }.majak-lottery-result-panel__page { padding: 13px; color:#d7e3d7; text-align:center; font-size:var(--majak-popup-font-body); line-height:var(--majak-popup-leading-body); }.majak-lottery-result-panel__note { overflow:hidden; padding: 0 24px 14px; color:#d9bc62; white-space:nowrap; text-overflow:ellipsis; text-align:center; font-size:var(--majak-popup-font-body); line-height:var(--majak-popup-leading-body); }.majak-lottery-result-panel__actions { display:flex; justify-content:flex-end; gap:10px; padding:13px 18px; border-top:1px solid rgba(217,188,98,.55); background:rgba(5,31,23,.62); }.majak-lottery-result-panel__actions button { min-width:78px; height:38px; border:1px solid #698674; border-radius:4px; color:#fff; background:#315f4d; font:700 var(--majak-popup-font-emphasis)/1 var(--majak-font-family-ui); }.majak-lottery-result-panel__actions .is-buy { min-width:166px; border-color:#255d4e; background:#1c6b58; }.majak-lottery-result-panel__actions button:disabled { opacity:.4; }
        @media (max-width:600px) { .majak-lottery-result-panel { max-height:calc(100dvh - 20px); overflow-y:auto; }.majak-lottery-result-panel__header { gap:8px; padding:12px; }.majak-lottery-result-panel__header strong { min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font-size:var(--majak-popup-font-title); }.majak-lottery-result-panel__total { gap:12px; padding:14px; }.majak-lottery-result-panel__total strong { margin-left:auto; font-size:var(--majak-popup-font-title); white-space:nowrap; }.majak-lottery-result-panel__entry { grid-template-columns:80px 1fr; padding:10px 14px; }.majak-lottery-result-panel__actions { position:sticky; bottom:0; display:grid; grid-template-columns:1fr 1fr; padding:10px; }.majak-lottery-result-panel__actions button, .majak-lottery-result-panel__actions .is-buy { min-width:0; width:100%; }.majak-lottery-result-panel__actions button:last-child { border-color:#255d4e; background:#1c6b58; } }
      `}</style>
    </div>
  )
}
