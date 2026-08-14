/**
 * HanCoinReceiptDlg — CMJReceiptDlg 相当のMP購入レシート (AP-09 §3-2-8)
 * レガシー: legacy/client/HgMajak2/MJReceiptDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,390,470) → 390×470px, CenterWindow(GetParent())
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 390×470):
 *   コインアイテム (m_strSellCode == ""):
 *     _ShopReceiptMain2.png   at (0, 0)
 *   便利アイテム (m_strSellCode != ""):
 *     _ShopReceiptMain2b.png  at (0, 0)
 *
 * Exit ✕ ボタン (4フレーム 18×18):
 *   _ShopReceiptExitBtn.png  at (358, 12)  IDOK
 *
 * OK 閉じるボタン (4フレーム 112×20):
 *   _ShopReceiptOkBtn.png   at (139, 427)  IDOK
 *
 * アイテム画像 (1フレーム):
 *   m_pItemImage->Draw(&dc, 162, 74, 0)
 *
 * ── テキスト (OnPaint — 13px bold MS Pゴシック, 透過背景) ─────────────────
 *   m_strMessage[0]  TextOut(15, 50)       購入者名
 *   itemName         CRect(198,165,372,182) DT_CENTER  アイテム名
 *   m_strMessage[1]  CRect(198,185,372,202) DT_RIGHT   購入前キャッシュ
 *   m_strMessage[2]  CRect(198,205,372,222) DT_RIGHT   購入前商品券
 *   m_strMessage[3]  CRect(198,225,372,242) DT_RIGHT   価格×数量
 *   m_strMessage[4]  CRect(198,245,372,262) DT_RIGHT   購入後キャッシュ
 *   m_strMessage[5]  CRect(198,265,372,282) DT_RIGHT   購入後商品券
 *   m_strMessage[6]  TextOut(17, 288)       購入完了メッセージ
 *   m_strMessage[7]  TextOut(17, 302)       コイン補充メッセージ
 *   m_strMessage[8]  TextOut(17, 330)       緑文字 RGB(40,160,100)
 *   m_strMessage[9]  TextOut(17, 344)       緑文字 RGB(40,160,100)
 * ────────────────────────────────────────────────────────────────────────
 */

interface Props {
  pix: string
  memberName?: string
  itemName: string
  sellCode: string       // "" = コインアイテム, その他 = 便利アイテム
  price: number          // m_nHancoinPrice
  count: number
  coinBefore: number     // nHanCoinBefore
  coinAfter: number      // nHanCoinAfter
  gameMoney: number      // m_llGameMoney (コイン補充額)
  imageUrl?: string
  onClose: () => void
}

export default function HanCoinReceiptDlg({
  pix, memberName, itemName, sellCode, price, count,
  coinBefore, coinAfter,
  gameMoney, imageUrl, onClose,
}: Props) {
  const yen = (n: number) => `${Math.trunc(n).toLocaleString('ja-JP')} MP`
  const moneyString = (value: number) => `${Math.trunc(value).toLocaleString('ja-JP')} GP`
  const isConvenience = sellCode !== ''

  /* メッセージ生成 (コンストラクタの m_strMessage[] 相当) */
  const msg0 = `"${memberName || pix}"さんが購入したアイテム`
  const msg1 = yen(coinBefore)
  const msg3 = `(${yen(price)}×${count})${Math.trunc(price * count).toLocaleString('ja-JP')} MP`
  const msg4 = yen(coinAfter)
  const msg6 = isConvenience
    ? `${itemName}を${count}個購入しました`
    : `${itemName}を${count}個購入して`
  const msg7 = `${moneyString(gameMoney * count)}が補充されました。`

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-receipt-dialog" role="dialog" aria-modal="true" aria-labelledby="han-coin-receipt-title">
        <header id="han-coin-receipt-title" className="majak-popup-titlebar"><span>購入完了</span><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-receipt-dialog__body">
          <p className="majak-receipt-dialog__purchaser">{msg0}</p>
          <div className="majak-receipt-dialog__item">
            {imageUrl && <img src={imageUrl} alt={itemName} onError={event => { event.currentTarget.hidden = true }} />}
            <strong>{itemName}</strong>
          </div>
          <dl className="majak-receipt-dialog__details">
            <div><dt>購入前MP</dt><dd>{msg1}</dd></div>
            <div><dt>購入内容</dt><dd>{msg3}</dd></div>
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
