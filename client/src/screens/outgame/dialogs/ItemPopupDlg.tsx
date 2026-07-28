/**
 * CItemPopupDlg 相当 — アイテムポップアップ (AP-09 §3-2-2)
 * レガシー: legacy/client/HgMajak2/ItemPopupDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,510,410) → 510×410px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 31 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 510×410):
 *   mj_shp_window_minishop_01.png  at (0, 0)
 *   DrawTransparent(&dc, 0, 0, 0)
 *
 * OKボタン (4フレーム 88×32):
 *   mj_shp_btn_ok.png  at (211, 358)  IDOK
 *
 * 購入ボタン×3 (4フレーム 51×28):
 *   mj_shp_btn_buy.png  at (119+150*i, 303)  IDC_BTN_ITEM1BUY+i  (i=0,1,2)
 *
 * おすすめ！アイコン (1フレーム 83×32):
 *   mj_shp_icon.png  at (64+150*m_nEncourageMarkPos, 141)
 *   m_nEncourageMarkPos = 2 (デフォルト)
 *   DrawTransparent(&dc, 64+150*pos, 141, 0)
 *
 * アイテム画像 (1フレーム 65×65):
 *   m_pItemImage[i]->Draw(&dc, 73+150*i, 226, 0)
 *
 * ── テキスト (OnPaint — レガシー準拠) ─────────────────────────────────────
 *  タイトル: CRect(148,7,364,22) DT_CENTER 15px bold 白
 *  メッセージ: m_rcMessage DT_CENTER 12px bold 黒
 *    2行ケース: CRect(12,58,497,81)  ← FREE / USEDUP / EVENTENTRY2 / NOTHING
 *    1行ケース: CRect(12,64,497,75)  ← その他
 *  アイテム名: CRect(40+150*i,187, 170+150*i,198) DT_CENTER 黒
 *  おすすめ文: CRect(40+150*i,199, 170+150*i,210) DT_CENTER RGB(40,160,100)
 *  価格:       CRect(46+150*i,312, 108+150*i,323) DT_RIGHT  黒
 * ────────────────────────────────────────────────────────────────────────
 *
 * 表示ケース (enum itemPopupReason):
 *   ID_REASON_FREE=2      "無料補充完了"
 *   ID_REASON_USEDUP=3    "無料補充回数使用済み"
 *   ID_REASON_CANTSTAY1-3 "対局できません"
 *   ID_REASON_EVENTENTRY  "対局できません"
 *   ID_REASON_EVENTENTRY2 "参加できません"
 */
import { useRef, useEffect, useState } from 'react'
import BuyHanCoinItemDlg from './BuyHanCoinItemDlg'
import { SHOP_ITEM_DATA_BUY } from './shopItemData'
import { playMajakSfx } from '../../../utils/majakSound'

const IMG = '/assets/images/game'
const FONT = "'MS PGothic', 'MS UI Gothic', sans-serif"

/** itemPopupReason 相当 */
export const POPUP_REASON = {
  NOTHING:      0,
  INSURE:       1,
  FREE:         2,
  USEDUP:       3,
  CANTSTAY1:    4,
  CANTSTAY2:    5,
  CANTSTAY3:    6,
  EVENTENTRY:   7,
  EVENTENTRY2:  8,
} as const
export type PopupReason = typeof POPUP_REASON[keyof typeof POPUP_REASON]

/** 販売アイテム情報 (CMajakShopItemData 相当) */
export interface PopupItemData {
  itemCode: string
  sellCode: string
  itemName: string
  itemNameSub: string   // "おすすめ{itemNameSub}" で表示
  itemNameSub2?: string  // 龍珠倍率表示 (CMajakShopItemData::m_strItemNameSub2)
  price: number
  gameMoney?: number
  imageUrl?: string
}

interface Props {
  reason: PopupReason
  gamMoney?: number                 // llGamMoney — 現在のコイン残高
  message?: string                  // szMessage — CANTSTAY 系でサーバ/呼び出し元から渡される文言
  items?: [PopupItemData, PopupItemData, PopupItemData]  // 省略時は m_ShopItemData2[0,3,7]
  encouragePos?: number             // m_nEncourageMarkPos (0,1,2) デフォルト=2
  pix?: string
  memberName?: string
  hanCoin?: number
  hanCoinCoupon?: number
  onClose: () => void
  onBuyItem?: (item: PopupItemData) => void
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src, frameW, frameH, x, y, onClick, title,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void; title?: string
}) {
  const [fi, setFi] = useState(0)
  return (
    <button
      title={title}
      onClick={onClick}
      onMouseEnter={() => setFi(2)}
      onMouseLeave={() => setFi(0)}
      onMouseDown={() => setFi(3)}
      onMouseUp={() => setFi(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: 'pointer', outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

const DEFAULT_POPUP_ITEMS: [PopupItemData, PopupItemData, PopupItemData] = [
  SHOP_ITEM_DATA_BUY[0],
  SHOP_ITEM_DATA_BUY[3],
  SHOP_ITEM_DATA_BUY[7],
].map(item => ({
  itemCode: item.avCode,
  sellCode: item.sellCode,
  itemName: item.name,
  itemNameSub: item.nameSub,
  itemNameSub2: item.nameSub2,
  price: item.hancoinPrice,
  gameMoney: item.gameMoney,
  imageUrl: item.imagePath,
})) as [PopupItemData, PopupItemData, PopupItemData]

/** タイトル文字列 (m_strTitle.Format 相当) */
function getTitle(reason: PopupReason): string {
  switch (reason) {
    case POPUP_REASON.FREE:        return '無料補充完了'
    case POPUP_REASON.USEDUP:      return '無料補充回数使用済み'
    case POPUP_REASON.CANTSTAY1:
    case POPUP_REASON.CANTSTAY2:
    case POPUP_REASON.CANTSTAY3:   return '対局できません'
    case POPUP_REASON.EVENTENTRY:  return '対局できません'
    case POPUP_REASON.EVENTENTRY2: return '参加できません'
    default:                       return ''
  }
}

/** メッセージ文字列 (m_strMessage.Format 相当) */
function getMessage(reason: PopupReason, gamMoney = 0, callerMessage = ''): string {
  switch (reason) {
    case POPUP_REASON.FREE:
      return `無料補充で${Math.trunc(gamMoney)}円になりました。\n1日の最大 自動補充回数を全部使いました。`
    case POPUP_REASON.USEDUP:
      return '1日の最大自動補充回数を全部使いました。\n（自動補充回数の回復は朝6時ごろです）'
    case POPUP_REASON.CANTSTAY1:
    case POPUP_REASON.CANTSTAY2:
    case POPUP_REASON.CANTSTAY3:
      return callerMessage
    case POPUP_REASON.EVENTENTRY:
      return '大会に対局するためにはコインアイテムの購入が必要になります。'
    case POPUP_REASON.EVENTENTRY2:
      return '予選（無料）の予選通過条件を満たしていない場合は、\nGEMでコインアイテムを購入する必要があります'
    default:
      return ''
  }
}

/** メッセージ矩形 (m_rcMessage 相当) */
function getMsgRect(reason: PopupReason): { left: number; top: number; width: number; height: number } {
  /** 2行ケース: CRect(12,58,497,81) */
  const twoLine = [
    POPUP_REASON.FREE,
    POPUP_REASON.USEDUP,
    POPUP_REASON.NOTHING,
    POPUP_REASON.EVENTENTRY2,
  ] as PopupReason[]
  if (twoLine.includes(reason)) {
    return { left: 12, top: 58, width: 485, height: 23 }   /* 497-12=485, 81-58=23 */
  }
  /** 1行ケース: CRect(12,64,497,75) */
  return { left: 12, top: 64, width: 485, height: 11 }     /* 75-64=11 */
}

function makeMoneyString(value: number): string {
  const digits = String(Math.abs(Math.trunc(value)))
  const units = ['', '万', '億', '兆', '京']
  const parts: string[] = []
  for (let end = digits.length, unit = 0; end > 0; end -= 4, unit++) {
    const start = Math.max(0, end - 4)
    const part = Number(digits.slice(start, end))
    if (part > 0) parts.unshift(`${part}${units[unit] ?? ''}`)
  }
  return `${value < 0 ? '-' : ''}${parts.length > 0 ? parts.join('') : '0'}円`
}

function getBuyDialogDescription(item: PopupItemData): string[] {
  if (item.itemNameSub2) {
    return [
      `${item.itemNameSub}の間、獲得できる龍珠が${item.itemNameSub2}になります。`,
      '※対局終了時にアイテムの効果が有効である必要があります。',
      '※龍珠2倍と龍珠3倍が同時に有効な場合は龍珠4倍となります。',
      `※オマケとして麻雀コイン${makeMoneyString(item.gameMoney ?? 0)}が付いてきます。`,
    ]
  }
  return [
    '残っている回数量によって交流広場及び段位戦場代が',
    '無料になります。',
    '※ハイ卓は対象外となります。',
    '※対局終了時に効果が有効である必要があります。',
    `※オマケとして麻雀コイン${makeMoneyString(item.gameMoney ?? 0)}が付いてきます。`,
  ]
}

/** ====================================================================
 * CItemPopupDlg 本体
 * ==================================================================== */
export default function ItemPopupDlg({
  reason,
  gamMoney = 0,
  message: callerMessage = '',
  items = DEFAULT_POPUP_ITEMS,
  encouragePos = 2,
  pix = '',
  memberName = '',
  hanCoin = -1,
  hanCoinCoupon = -1,
  onClose,
  onBuyItem,
}: Props) {
  const [buyTarget, setBuyTarget] = useState<PopupItemData | null>(null)
  /* ドラッグ移動 (OnNcHitTest: pt.y < 31 → HTCAPTION) */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })

  const onDragStart = (e: React.MouseEvent) => {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    if (e.clientY - rect.top >= 31) return
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

  useEffect(() => {
    if (reason === POPUP_REASON.FREE) playMajakSfx('mjkhojyu')
  }, [reason])

  /** OnBtnItem1/2/3BuyClicked → buyItem(idx) → CMJBuyItemDlg.DoModal() */
  const openBuyDialog = (item: PopupItemData) => setBuyTarget(item)

  const title   = getTitle(reason)
  const message = getMessage(reason, gamMoney, callerMessage)
  const msgRect = getMsgRect(reason)

  /* おすすめアイコン: ID_REASON_EVENTENTRY / EVENTENTRY2 では非表示 */
  const showEncourage = reason !== POPUP_REASON.EVENTENTRY && reason !== POPUP_REASON.EVENTENTRY2

  return (
    /* モーダルオーバーレイ */
    <div
      style={{
        position: 'absolute', inset: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: 'rgba(0,0,0,0.45)',
        zIndex: 300,
      }}
    >
      {/* CItemPopupDlg クライアント領域: 510×410px */}
      <div
        style={{
          position: 'relative',
          width: 510, height: 410,
          left: pos.x, top: pos.y,
        }}
        onMouseDown={onDragStart}
      >
        {/* ================================================================
            背景: mj_shp_window_minishop_01.png (510×410) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            DrawTransparent(&dc, 0, 0, 0)
            ================================================================ */}
        <img
          src={`${IMG}/mj_shp_window_minishop_01.png`}
          alt=""
          draggable={false}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 510, height: 410,
            userSelect: 'none',
          }}
        />

        {/* ================================================================
            タイトル (OnPaint)
            DrawText(m_strTitle, CRect(148,7,364,22), DT_CENTER)
            15px bold 白
            ================================================================ */}
        <div
          style={{
            position: 'absolute',
            left: 148, top: 7, width: 216, height: 15,  /* 364-148=216, 22-7=15 */
            fontFamily: FONT,
            fontSize: 15, fontWeight: 'bold', color: '#fff',
            textAlign: 'center', pointerEvents: 'none',
          }}
        >
          {title}
        </div>

        {/* ================================================================
            メッセージ (OnPaint)
            DrawText(m_strMessage, m_rcMessage, DT_CENTER)
            12px bold 黒
            2行ケース: CRect(12,58,497,81)
            1行ケース: CRect(12,64,497,75)
            ================================================================ */}
        <div
          style={{
            position: 'absolute',
            left: msgRect.left,
            top: msgRect.top,
            width: msgRect.width,
            height: msgRect.height,
            fontFamily: FONT,
            fontSize: 12, fontWeight: 'bold', color: '#000',
            textAlign: 'center',
            whiteSpace: 'pre-line',
            pointerEvents: 'none',
          }}
        >
          {message}
        </div>

        {/* ================================================================
            おすすめ！アイコン: mj_shp_icon.png (83×32, 1フレーム)
            DrawTransparent(&dc, 64+150*m_nEncourageMarkPos, 141, 0)
            ID_REASON_EVENTENTRY / EVENTENTRY2 では非表示 (SAFE_DELETE)
            ================================================================ */}
        {showEncourage && (
          <img
            src={`${IMG}/mj_shp_icon.png`}
            alt="おすすめ"
            draggable={false}
            style={{
              position: 'absolute',
              left: 64 + 150 * encouragePos,
              top: 141,
              width: 83,
              height: 32,
              pointerEvents: 'none',
            }}
          />
        )}

        {/* ================================================================
            アイテム × 3 (i=0,1,2):
              アイテム画像 m_pItemImage[i]->Draw(&dc, 73+150*i, 226, 0)
              アイテム名   CRect(40+150*i,187, 170+150*i,198) DT_CENTER 黒
              おすすめ文   CRect(40+150*i,199, 170+150*i,210) DT_CENTER RGB(40,160,100)
              価格         CRect(46+150*i,312, 108+150*i,323) DT_RIGHT  黒
            購入ボタン  mj_shp_btn_buy.png (204×28 → 4フレーム 51×28)
              at (119+150*i, 303)  IDC_BTN_ITEM1BUY+i
            ================================================================ */}
        {items.map((item, i) => (
          <div key={item.itemCode} style={{ position: 'absolute', left: 0, top: 0 }}>

            {/* アイテム画像 (65×65, 1フレーム) at (73+150*i, 226) */}
            {item.imageUrl && (
              <img
                src={item.imageUrl}
                alt={item.itemName}
                draggable={false}
                style={{
                  position: 'absolute',
                  left: 73 + 150 * i,
                  top: 226,
                  width: 65,
                  height: 65,
                  pointerEvents: 'none',
                }}
                onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
              />
            )}

            {/* アイテム名 CRect(40+150*i,187, 170+150*i,198) DT_CENTER 黒 */}
            <div
              style={{
                position: 'absolute',
                left: 40 + 150 * i,
                top: 187,
                width: 130,  /* 170-40=130 */
                height: 11,  /* 198-187=11 */
                fontFamily: FONT,
                fontSize: 12, fontWeight: 'bold', color: '#000',
                textAlign: 'center',
                overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
                pointerEvents: 'none',
              }}
            >
              {item.itemName}
            </div>

            {/* おすすめ文 CRect(40+150*i,199, 170+150*i,210) DT_CENTER RGB(40,160,100) */}
            <div
              style={{
                position: 'absolute',
                left: 40 + 150 * i,
                top: 199,
                width: 130,
                height: 11,  /* 210-199=11 */
                fontFamily: FONT,
                fontSize: 12, fontWeight: 'bold', color: 'rgb(40,160,100)',
                textAlign: 'center',
                overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
                pointerEvents: 'none',
              }}
            >
              おすすめ{item.itemNameSub}
            </div>

            {/* 価格 CRect(46+150*i,312, 108+150*i,323) DT_RIGHT 黒 */}
            <div
              style={{
                position: 'absolute',
                left: 46 + 150 * i,
                top: 312,
                width: 62,   /* 108-46=62 */
                height: 11,  /* 323-312=11 */
                fontFamily: FONT,
                fontSize: 12, fontWeight: 'bold', color: '#000',
                textAlign: 'right',
                pointerEvents: 'none',
              }}
            >
              {makeMoneyString(item.price)}
            </div>

            {/* 購入ボタン: mj_shp_btn_buy.png (204×28, 4フレーム 51×28) at (119+150*i, 303) */}
            <SpriteButton
              src={`${IMG}/mj_shp_btn_buy.png`}
              frameW={51} frameH={28}
              x={119 + 150 * i} y={303}
              onClick={() => openBuyDialog(item)}
              title="購入"
            />
          </div>
        ))}

        {/* ================================================================
            OKボタン: mj_shp_btn_ok.png (352×32, 4フレーム 88×32) at (211,358)
            m_btnClose.Create(0, ..., 211, 358, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_ok.png`}
          frameW={88} frameH={32}
          x={211} y={358}
          onClick={onClose}
          title="OK"
        />
      </div>

      {buyTarget && (
        <BuyHanCoinItemDlg
          item={{
            itemCode: buyTarget.itemCode,
            sellCode: buyTarget.sellCode,
            itemName: buyTarget.itemName,
            price: buyTarget.price,
            gameMoney: buyTarget.gameMoney ?? 0,
            description: getBuyDialogDescription(buyTarget),
            imageUrl: buyTarget.imageUrl,
            isLottery: false,
          }}
          pix={pix}
          memberName={memberName || pix}
          hanCoin={hanCoin}
          hanCoupon={hanCoinCoupon}
          onClose={() => setBuyTarget(null)}
          onBuyOK={() => {
            onBuyItem?.(buyTarget)
            onClose()
          }}
        />
      )}
    </div>
  )
}
