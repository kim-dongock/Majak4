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
import { useRef, useEffect, useState } from 'react'

const IMG_LOT = '/assets/images/game/lot'
const FONT = 'var(--majak-font-family-ui)'

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
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src, frameW, frameH, x, y, onClick, disabled = false, title,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void
  disabled?: boolean; title?: string
}) {
  const [fi, setFi] = useState(disabled ? 1 : 0)
  useEffect(() => { setFi(disabled ? 1 : 0) }, [disabled])
  return (
    <button
      title={title}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => !disabled && setFi(2)}
      onMouseLeave={() => setFi(disabled ? 1 : 0)}
      onMouseDown={() => !disabled && setFi(3)}
      onMouseUp={() => !disabled && setFi(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: disabled ? 'not-allowed' : 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

/** ====================================================================
 * CMJLotResultDlg 本体
 * ==================================================================== */
export default function LotResultDlg({
  itemName, lotteryCount, entries, totalAmount,
  nextLotteryCount, onBuyAgain, onClose,
}: Props) {
  const [curPage, setCurPage] = useState(1)
  const maxPage = Math.max(1, Math.ceil(lotteryCount / DATA_PER_PAGE))

  /* ドラッグ移動 (OnNcHitTest: pt.y < 40 → HTCAPTION) */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })

  const onDragStart = (e: React.MouseEvent) => {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    if (e.clientY - rect.top >= 40) return
    dragging.current   = true
    dragOffset.current = { dx: e.clientX - pos.x, dy: e.clientY - pos.y }
    e.preventDefault()
  }
  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragging.current) return
      setPos({ x: e.clientX - dragOffset.current.dx, y: e.clientY - dragOffset.current.dy })
    }
    const onUp = () => { dragging.current = false }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup',   onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup',   onUp)
    }
  }, [])

  /* 再購入メッセージ (m_strBuyAgain 相当) */
  const buyAgainMsg = nextLotteryCount !== lotteryCount
    ? `※再購入すると\n抽選回数が${lotteryCount}回から\n${nextLotteryCount}回に 増える!`
    : ''

  const makeMoneyString = (value: number, addCurrency = true) => {
    return `${Math.trunc(value).toLocaleString('ja-JP')}${addCurrency ? ' GP' : ''}`
  }

  /* 現在ページのデータ */
  const pageEntries = entries.slice((curPage - 1) * DATA_PER_PAGE, curPage * DATA_PER_PAGE)

  const txtBase = {
    position: 'absolute' as const,
    fontFamily: FONT,
    fontSize: 'calc(13px * var(--majak-type-scale))', fontWeight: 'bold' as const,
    lineHeight: '13px' as const,
    pointerEvents: 'none' as const,
    overflow: 'hidden' as const, whiteSpace: 'nowrap' as const,
  }

  return (
    /* モーダルオーバーレイ */
    <div style={{
      position: 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent', zIndex: 300,
    }}>
      {/* CMJLotResultDlg クライアント領域: 301×333px */}
      <div style={{
        position: 'relative',
        width: 301, height: 333,
        left: pos.x, top: pos.y,
      }}
        onMouseDown={onDragStart}
      >

        {/* ================================================================
            背景: lot/lot_base2.png (301×333) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            ================================================================ */}
        <img
          src={`${IMG_LOT}/lot_base2.png`}
          alt=""
          draggable={false}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 301, height: 333,
            userSelect: 'none',
          }}
        />

        {/* ================================================================
            テキスト (Draw() 相当)
            ================================================================ */}

        {/* タイトル "{itemName} 今回の獲得金額" CRect(30,34,271,47) DT_CENTER 白 */}
        <div style={{ ...txtBase, left: 30, top: 34, width: 241, height: 13,
          color: '#fff', textAlign: 'center' }}>
          {itemName} 今回の獲得金額
        </div>

        {/* 合計金額 CRect(80,54,222,67) DT_CENTER 緑 RGB(6,65,2) */}
        <div style={{ ...txtBase, left: 80, top: 54, width: 142, height: 13,
          color: 'rgb(6,65,2)', textAlign: 'center' }}>
          {makeMoneyString(totalAmount)}
        </div>

        {/* ================================================================
            データ表示エリア (DrawPageText 相当)
            1ページ10件:
              回数データ:  CRect(46, 94+16*i, 96, 107+16*i)
              金額データ:  CRect(130, 94+16*i, 272, 107+16*i)
            ================================================================ */}
        {pageEntries.map((e, i) => (
          <div key={e.seq} style={{ position: 'absolute', left: 0, top: 0 }}>
            {/* 回数 CNTDATA_CORNER_POS_X=46, Y=94+16*i, WIDTH=50, HEIGHT=13 */}
            <div style={{ ...txtBase, left: 46, top: 94 + 16 * i, width: 50, height: 13,
              color: 'rgb(6,65,2)', textAlign: 'right' }}>
              {e.seq}回目
            </div>
            {/* 金額 MONEYDATA_CORNER_POS_X=130, Y=94+16*i, WIDTH=142, HEIGHT=13 */}
            <div style={{ ...txtBase, left: 130, top: 94 + 16 * i, width: 142, height: 13,
              color: 'rgb(6,65,2)', textAlign: 'right' }}>
              {makeMoneyString(e.amount)}
            </div>
          </div>
        ))}

        {/* ================================================================
            ページ表示 s_rcPage CRect(190,250,290,270) 中央
            ================================================================ */}
        <div style={{ ...txtBase, left: 190, top: 250, width: 100, height: 20,
          color: 'rgb(6,65,2)', textAlign: 'center' }}>
          {curPage} / {maxPage}
        </div>

        {/* 再購入メッセージ CRect(12,275,143,316) DT_CENTER 白 */}
        {buyAgainMsg && (
          <div style={{
            position: 'absolute',
            left: 12, top: 275, width: 131, height: 41,
            fontFamily: FONT,
            fontSize: 'calc(13px * var(--majak-type-scale))', fontWeight: 'bold', color: '#fff',
            lineHeight: '13px',
            textAlign: 'center',
            whiteSpace: 'pre-line',
            pointerEvents: 'none',
            overflow: 'hidden',
          }}>
            {buyAgainMsg}
          </div>
        )}

        {/* ================================================================
            ← 矢印: lot/lot_btn_mark_l.png (36×14, 4フレーム 9×14) at (193,254)
            m_btnArrowL.Create(0, ..., 193, 254, ..., IDC_BTN_LARROW)
            ================================================================ */}
        <SpriteButton
          src={`${IMG_LOT}/lot_btn_mark_l.png`}
          frameW={9} frameH={14}
          x={193} y={254}
          onClick={() => setCurPage(p => Math.max(1, p - 1))}
          title="前のページ"
        />

        {/* ================================================================
            → 矢印: lot/lot_btn_mark_r.png (36×14, 4フレーム 9×14) at (277,254)
            m_btnArrowR.Create(0, ..., 277, 254, ..., IDC_BTN_RARROW)
            ================================================================ */}
        <SpriteButton
          src={`${IMG_LOT}/lot_btn_mark_r.png`}
          frameW={9} frameH={14}
          x={277} y={254}
          onClick={() => setCurPage(p => Math.min(maxPage, p + 1))}
          title="次のページ"
        />

        {/* ================================================================
            再購入: lot/lot_t_btn_4.png (288×42, 4フレーム 72×42) at (139,274)
            m_btnBuy.Create(0, ..., 139, 274, ..., IDYES)
            ================================================================ */}
        <SpriteButton
          src={`${IMG_LOT}/lot_t_btn_4.png`}
          frameW={72} frameH={42}
          x={139} y={274}
          onClick={onBuyAgain}
          title="再購入"
        />
        <div style={{ ...txtBase, left: 179, top: 297, width: 25, height: 13,
          color: 'rgb(6,65,2)', textAlign: 'right', zIndex: 1 }}>
          {nextLotteryCount}
        </div>

        {/* ================================================================
            閉じる: lot/lot_t_btn_3.png (288×42, 4フレーム 72×42) at (215,274)
            m_btnClose.Create(0, ..., 215, 274, ..., IDNO)
            ================================================================ */}
        <SpriteButton
          src={`${IMG_LOT}/lot_t_btn_3.png`}
          frameW={72} frameH={42}
          x={215} y={274}
          onClick={onClose}
          title="閉じる"
        />
      </div>
    </div>
  )
}
