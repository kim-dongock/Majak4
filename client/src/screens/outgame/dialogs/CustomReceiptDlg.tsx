/**
 * CMJCustomReceiptDlg 相当 — カスタムアイテム購入レシート (AP-09 §3-2-10)
 * レガシー: legacy/client/HgMajak2/MJCustomReceiptDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,390,470) → 390×470px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 41 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 390×470):
 *   _ShopReceiptMainCustom.png  at (0, 0)
 *   ラベル群は背景に焼き込み済み
 *
 * Exit ✕ ボタン (4フレーム 18×18):
 *   _ShopReceiptExitBtn.png  at (358, 12)  IDOK
 *
 * OK 閉じるボタン (4フレーム 112×20):
 *   _ShopReceiptOkBtn.png   at (139, 427)  IDOK
 *
 * アイテム画像 (1フレーム):
 *   items/custom/mj_custom_{ItemID:02d}.png  at (162, 74)
 *   MAJAK4_ITEM_LIST_CUSTOM_ITEM + ItemID + MAJAK4_EXTENSION_HIM 相当
 *
 * ── テキスト (OnPaint — 13px bold MS Pゴシック, 透過背景) ─────────────────
 *   m_strMessage[0]  TextOut(15, 50)        購入者名
 *   itemName         CRect(198,170,372,207) DT_CENTER  アイテム名
 *   m_strMessage[1]  CRect(198,205,372,222) DT_RIGHT   購入前キャッシュ
 *   m_strMessage[2]  CRect(198,225,372,242) DT_RIGHT   購入前商品券
 *   m_strMessage[3]  CRect(198,245,372,262) DT_RIGHT   購入価格
 *   m_strMessage[4]  CRect(198,265,372,282) DT_RIGHT   購入後キャッシュ
 *   m_strMessage[5]  CRect(198,285,372,302) DT_RIGHT   購入後商品券
 *   m_strMessage[6]  TextOut(17, 308)       購入完了
 *   m_strMessage[7]  TextOut(17, 322)       コイン補充
 *   m_strMessage[8]  TextOut(17, 350)       緑文字 RGB(40,160,100) — 常に Empty()
 *   m_strMessage[9]  TextOut(17, 364)       緑文字 RGB(40,160,100) — 常に Empty()
 * ────────────────────────────────────────────────────────────────────────
 */
const IMG_ITEM = '/assets/images/game/items/custom'

interface Props {
  pix: string
  memberName?: string
  itemId: number           // CMajakShopCustomItem.Item.ItemID
  itemName: string         // CMajakShopCustomItem.Item.ItemName
  price: number            // CMajakShopCustomItem.Price
  gameMoney: number        // CMajakShopCustomItem.GameMoney (コイン補充額)
  coinBefore: number       // nHanCoinBefore
  coinAfter: number        // nHanCoinAfter
  onClose: () => void
}

export default function CustomReceiptDlg({
  pix, memberName, itemId, itemName, price, gameMoney,
  coinBefore, coinAfter,
  onClose,
}: Props) {
  /* アイテム画像パス: MAJAK4_ITEM_LIST_CUSTOM_ITEM + ItemID:02d + MAJAK4_EXTENSION_HIM 相当 */
  const itemImageSrc = `${IMG_ITEM}/mj_custom_${String(itemId).padStart(2, '0')}.png`

  /* メッセージ生成 (コンストラクタ相当) */
  const yen  = (n: number) => `${Math.trunc(n).toLocaleString('ja-JP')} MP`
  const moneyString = (value: number) => `${Math.trunc(value).toLocaleString('ja-JP')} GP`
  const msg0 = `"${memberName || pix}"さんが購入したアイテム`
  const msg1 = yen(coinBefore)
  const msg3 = yen(price)
  const msg4 = yen(coinAfter)
  const msg6 = `${itemName}を購入しました`
  const msg7 = `${moneyString(gameMoney)}が補充されました。`

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-receipt-dialog" role="dialog" aria-modal="true" aria-labelledby="custom-receipt-title">
        <header id="custom-receipt-title" className="majak-popup-titlebar"><span>購入完了</span><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-receipt-dialog__body">
          <p className="majak-receipt-dialog__purchaser">{msg0}</p>
          <div className="majak-receipt-dialog__item">
            <img src={itemImageSrc} alt={itemName} onError={event => { event.currentTarget.hidden = true }} />
            <strong>{itemName}</strong>
          </div>
          <dl className="majak-receipt-dialog__details">
            <div><dt>購入前MP</dt><dd>{msg1}</dd></div>
            <div><dt>購入価格</dt><dd>{msg3}</dd></div>
            <div><dt>購入後MP</dt><dd>{msg4}</dd></div>
          </dl>
          <p>{msg6}</p>
          <p>{msg7}</p>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" className="is-primary" onClick={onClose}>OK</button>
        </footer>
      </section>
    </div>
  )
}
