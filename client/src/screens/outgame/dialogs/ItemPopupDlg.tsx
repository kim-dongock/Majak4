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
import { useEffect, useState } from 'react'
import BuyHanCoinItemDlg from './BuyHanCoinItemDlg'
import { SHOP_ITEM_DATA_BUY } from './shopItemData'
import { playMajakSfx } from '../../../utils/majakSound'

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
      return `無料補充で${Math.trunc(gamMoney).toLocaleString('ja-JP')} GPになりました。\n1日の最大 自動補充回数を全部使いました。`
    case POPUP_REASON.USEDUP:
      return '1日の最大自動補充回数を全部使いました。\n（自動補充回数の回復は朝6時ごろです）'
    case POPUP_REASON.CANTSTAY1:
    case POPUP_REASON.CANTSTAY2:
    case POPUP_REASON.CANTSTAY3:
      return callerMessage
    case POPUP_REASON.EVENTENTRY:
      return '大会に対局するためにはGPアイテムの購入が必要になります。'
    case POPUP_REASON.EVENTENTRY2:
      return '予選（無料）の予選通過条件を満たしていない場合は、\nMPでGPアイテムを購入する必要があります'
    default:
      return ''
  }
}

function makeMoneyString(value: number): string {
  return `${Math.trunc(value).toLocaleString('ja-JP')} MP`
}

function getBuyDialogDescription(item: PopupItemData): string[] {
  if (item.itemNameSub2) {
    return [
      `${item.itemNameSub}の間、獲得できる龍珠が${item.itemNameSub2}になります。`,
      '※対局終了時にアイテムの効果が有効である必要があります。',
      '※龍珠2倍と龍珠3倍が同時に有効な場合は龍珠4倍となります。',
      `※オマケとして${makeMoneyString(item.gameMoney ?? 0)}が付いてきます。`,
    ]
  }
  return [
    '残っている回数量によって交流広場及び段位戦場代が',
    '無料になります。',
    '※ハイ卓は対象外となります。',
    '※対局終了時に効果が有効である必要があります。',
    `※オマケとして${makeMoneyString(item.gameMoney ?? 0)}が付いてきます。`,
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
  hanCoinCoupon: _hanCoinCoupon = -1,
  onClose,
  onBuyItem,
}: Props) {
  const [buyTarget, setBuyTarget] = useState<PopupItemData | null>(null)

  useEffect(() => {
    if (reason === POPUP_REASON.FREE) playMajakSfx('mjkhojyu')
  }, [reason])

  /** OnBtnItem1/2/3BuyClicked → buyItem(idx) → CMJBuyItemDlg.DoModal() */
  const openBuyDialog = (item: PopupItemData) => setBuyTarget(item)

  const title   = getTitle(reason)
  const message = getMessage(reason, gamMoney, callerMessage)

  /* おすすめアイコン: ID_REASON_EVENTENTRY / EVENTENTRY2 では非表示 */
  const showEncourage = reason !== POPUP_REASON.EVENTENTRY && reason !== POPUP_REASON.EVENTENTRY2

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-item-popup-dialog" role="dialog" aria-modal="true" aria-labelledby="item-popup-dialog-title">
        <header id="item-popup-dialog-title" className="majak-popup-titlebar"><span>{title}</span><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-item-popup-dialog__body">
          {message && <p className="majak-item-popup-dialog__message">{message}</p>}
          <div className="majak-item-popup-dialog__items">
            {items.map((item, index) => (
              <article key={item.itemCode} className={`majak-item-popup-dialog__item${showEncourage && index === encouragePos ? ' is-recommended' : ''}`}>
                {showEncourage && index === encouragePos && <span className="majak-item-popup-dialog__recommendation">おすすめ</span>}
                {item.imageUrl && <img src={item.imageUrl} alt={item.itemName} onError={event => { event.currentTarget.hidden = true }} />}
                <strong>{item.itemName}</strong>
                <em>{makeMoneyString(item.price)}</em>
                <button type="button" onClick={() => openBuyDialog(item)}>購入</button>
              </article>
            ))}
          </div>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" className="is-primary" onClick={onClose}>OK</button>
        </footer>
      </section>

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
