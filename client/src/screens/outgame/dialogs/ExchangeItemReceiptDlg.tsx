/**
 * ExchangeItemReceiptDlg — CMJReceiptDlg2 相当の龍宝石/麻雀コイン交換レシート (AP-09 §3-2-9)
 * レガシー: legacy/client/HgMajak2/MJReceiptDlg2.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,389,500) → 389×500px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 41 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 389×500):
 *   mj_shp_window_exchange_04.png  at (0, 0)
 *   ラベル群 ("アイテム名" / "必要龍珠" / "保有龍珠" 等) は背景に焼き込み済み
 *
 * 確認 OK ボタン (4フレーム 85×29):
 *   mj_shp_btn_confirmation.png  at (152, 455)  IDOK
 *
 * アイテム画像:
 *   m_pItemImage->Draw(&dc, 162, 77, 0)
 *
 * ── テキスト (OnPaint — 12px bold MS Pゴシック, 透過背景) ─────────────────
 *   m_strMessage[0]  CRect(56,53,334,64)   DT_CENTER  購入者名
 *   itemName         CRect(144,155,351,166) DT_RIGHT   アイテム名
 *   itemKind         CRect(144,178,351,189) DT_RIGHT   アイテム種類
 *   itemGuid1        CRect(48,221,351,232)  DT_RIGHT   説明1
 *   itemGuid2        CRect(48,233,351,244)  DT_RIGHT   説明2
 *   m_strMessage[3]  CRect(260,261,346,272) DT_RIGHT   必要龍珠数
 *   m_strMessage[4]  CRect(260,291,346,302) DT_RIGHT   必要麻雀コイン
 *   m_strMessage[5]  CRect(260,325,346,336) DT_RIGHT   保有龍珠数
 *   m_strMessage[6]  CRect(260,355,346,366) DT_RIGHT   保有麻雀コイン
 *   m_strMessage[7]  CRect(144,383,351,394) DT_RIGHT   利用可能期間
 *   m_strMessage[8]  CRect(45,406,354,417)  DT_LEFT    注意書き1
 *   m_strMessage[9]  CRect(45,418,354,429)  DT_LEFT    注意書き2
 *   m_strMessage[10] CRect(45,430,354,441)  DT_LEFT    注意書き3
 * ────────────────────────────────────────────────────────────────────────
 */

/** アイテム種類 (SJIS デコード済み) */
export type ExchangeReceiptItemKind = 'アバター' | '麻雀称号' | 'リーチ棒' | 'other'

interface Props {
  pix: string
  memberName?: string
  itemName: string
  itemKind: string       // m_strItemKind
  itemGuid1: string
  itemGuid2: string
  costGem: number        // m_nCostGem
  costMoney: number      // m_llGameMoney
  userGem: number        // 保有龍珠
  userMoney: number      // 保有麻雀コイン
  limitDays: number      // m_nLimitDays (-1=永久)
  quantity: number       // m_nQuantity
  imageUrl?: string
  onClose: () => void
}

export default function ExchangeItemReceiptDlg({
  pix, memberName, itemName, itemKind,
  itemGuid1, itemGuid2,
  costGem, costMoney, userGem, userMoney,
  limitDays, quantity, imageUrl, onClose,
}: Props) {
  /* m_strMessage[] 生成 (コンストラクタ相当) */
  const msg0 = `"${memberName || pix}"さんが交換するアイテム`
  const msg3 = `${Math.trunc(costGem).toLocaleString('ja-JP')}個`
  const msg4 = makeMoneyString(costMoney)
  const msg5 = `${Math.trunc(userGem).toLocaleString('ja-JP')}個`
  const msg6 = makeMoneyString(userMoney)
  const msg7 = limitDays < 0
    ? (quantity <= 0 ? '永久' : `${quantity}回`)
    : `${limitDays}日間`

  function makeMoneyString(value: number): string {
    return `${Math.trunc(value).toLocaleString('ja-JP')} GP`
  }

  /* 注意書き (m_strMessage[8/9/10]) — アイテム種類別 */
  let msg8: string, msg9: string, msg10: string
  if (itemKind === 'アバター') {
    msg8  = '交換したアバターアイテムはハンゲームのマイページで'
    msg9  = '確認してください。'
    msg10 = ''
  } else if (itemKind === '麻雀称号') {
    msg8  = '交換した麻雀称号は麻雀４ページのコレクションで確認'
    msg9  = 'してください。'
    msg10 = ''
  } else if (itemKind === 'リーチ棒') {
    msg8  = '交換したリーチ棒はすぐ反映されます。'
    msg9  = '有効期間はショップの有効期間確認で確認してくださ'
    msg10 = 'い。'
  } else {
    msg8  = '交換したアナウンスはすぐ反映されます。'
    msg9  = '有効期間はショップの有効期間確認で確認してくださ'
    msg10 = 'い。'
  }

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-receipt-dialog" role="dialog" aria-modal="true" aria-labelledby="exchange-item-receipt-title">
        <header id="exchange-item-receipt-title" className="majak-popup-titlebar"><span>交換完了</span><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-receipt-dialog__body">
          <p className="majak-receipt-dialog__purchaser">{msg0}</p>
          <p className="majak-receipt-dialog__success">交換が完了しました。</p>
          <div className="majak-receipt-dialog__item">
            {imageUrl && <img src={imageUrl} alt={itemName} onError={event => { event.currentTarget.hidden = true }} />}
            <strong>{itemName}</strong>
            <span>{itemKind}</span>
          </div>
          <p>{itemGuid1}</p>
          <p>{itemGuid2}</p>
          <dl className="majak-receipt-dialog__details">
            <div><dt>必要龍珠</dt><dd>{msg3}</dd></div>
            <div><dt>必要GP</dt><dd>{msg4}</dd></div>
            <div><dt>保有龍珠</dt><dd>{msg5}</dd></div>
            <div><dt>保有GP</dt><dd>{msg6}</dd></div>
            <div><dt>利用可能期間</dt><dd>{msg7}</dd></div>
          </dl>
          <div className="majak-receipt-dialog__notices">
            <p>{msg8}</p>
            <p>{msg9}</p>
            {msg10 && <p>{msg10}</p>}
          </div>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" className="is-primary" onClick={onClose}>確認</button>
        </footer>
      </section>
    </div>
  )
}
