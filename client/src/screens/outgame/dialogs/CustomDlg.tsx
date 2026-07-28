/**
 * CMajakCustomDlg 相当 — カスタムショップ (AP-09 §3-2-3)
 * レガシー: legacy/client/HgMajak2/MajakCustomDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,662,514) → 662×514px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 31 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ──────────────
 *
 * 背景 (1フレーム単一画像):
 *   mj_custom_window.png   662×514   at (0, 0)
 *
 * タブ (COwnerCheckBox, 4フレーム 145×33, 選択=frame3 非選択=frame0):
 *   mj_shp_tab_06.png  at (14,  36)  IDC_BTN_TAB1 = キャラ
 *   mj_shp_tab_07.png  at (159, 36)  IDC_BTN_TAB2 = 牌
 *   mj_shp_tab_08.png  at (304, 36)  IDC_BTN_TAB3 = 背景  (159+145)
 *   mj_shp_tab_09.png  at (449, 36)  IDC_BTN_TAB4 = その他 (304+145)
 *
 * プレート (1フレーム 148×163):
 *   mj_shp_window_plate_05.png
 *   上段: (24+155*i, 91)  for i=0..3
 *   下段: (24+155*i, 266) for i=0..3 (if i+4 < nItemNumOfPage)
 *
 * アイテム画像 (1フレーム 130×73):
 *   items/custom/mj_custom_{id}.png  at (33+155*(i%4), 138+175*(i/4))
 *
 * アイテム名テキスト (12px bold黒, DT_CENTER):
 *   CRect(32+ox, 100+oy, 32+132+ox, 100+24+oy)  ox=155*(i%4), oy=175*(i/4)
 *
 * 装備ボタン (4フレーム 120×27, IDC_BTN_ITEM1SET+i):
 *   mj_btn_wear.png  at (36+155*(i%4), 219+175*(i/4))
 *   初期 WS_DISABLED, SW_HIDE → アイテムが現在の装備と異なる場合に有効化
 *
 * ページ操作:
 *   mj_shp_pagedown.png  26×42 (4フレーム)  at (257, 451)  IDC_BTN_LARROW
 *   mj_shp_pageup.png    26×42 (4フレーム)  at (382, 451)  IDC_BTN_RARROW
 *
 * ページ数字 (10フレーム 22×28, frame = ページ番号 1-10):
 *   mj_shp_num_01.png  現在ページ: at (298, 457)  最大ページ: at (344, 457)
 *
 * ショップバナー (4フレーム 230×58):
 *   items/custom/customshop_banner_0430.png  at (14, 443)  IDC_BTN_CUSTOM_SHOP_OPEN
 *
 * 閉じるボタン (4フレーム 88×32):
 *   mj_shp_btn_close.png  at (560, 469)  IDOK
 *
 * テキスト (OnPaint):
 *   "カスタム設定"      CRect(218,7,445,22) DT_CENTER 15px bold 白
 *   m_strMessage[0]   CRect(150,453,235,464) DT_RIGHT 12px bold 黒
 *   m_strMessage[1]   CRect(150,480,235,491) DT_RIGHT 12px bold 黒
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError } from '../../../utils/msgbox'
import { useAuthStore } from '../../../store/authStore'

const IMG      = '/assets/images/game'
const IMG_ITEM = '/assets/images/game/items/custom'
const CUSTOM_W = 662
const CUSTOM_H = 514

// タブ定数 (CUSTOM_ITEM_TAB_*)
const TAB_CHARA = 0
const TAB_HAI   = 1
const TAB_BG    = 2
const TAB_OTHER = 3

/** アイテム情報 */
interface CustomItem {
  itemId: number
  itemType: number
  itemName: string
  imageFile: string  // "mj_custom_01.png" など items/custom 以下のファイル名
  equipped: boolean
}

interface Props {
  initialTab?: number
  hanCoin?: number
  hanCoupon?: number
  currentCharaId?: number
  currentHaiId?: number
  currentBgId?: number
  onEquipChange?: (item: { itemId: number; itemType: number }) => void
  onRequestShop?: () => void
  onClose: () => void
}

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
      onMouseLeave={() => !disabled && setFi(disabled ? 1 : 0)}
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
 * COwnerCheckBox 相当 — タブボタン (選択中=frame3, 非選択=frame0)
 * ==================================================================== */
function TabButton({
  src, frameW, frameH, x, y, active, onClick,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; active: boolean; onClick: () => void
}) {
  const [hover, setHover] = useState(false)
  const fi = active ? 3 : hover ? 2 : 0
  return (
    <button
      aria-disabled={active}
      onClick={active ? undefined : onClick}
      onMouseEnter={() => !active && setHover(true)}
      onMouseLeave={() => !active && setHover(false)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0, cursor: active ? 'default' : 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

/** ====================================================================
 * ページ数字 — mj_shp_num_01.png (220×28, 10フレーム 22×28)
 * frame = ページ番号 (1〜10), 表示位置: left, top
 * ==================================================================== */
function PageNum({ n, x, y }: { n: number; x: number; y: number }) {
  /* frame index = n (1-based) : frame 1 → offset -22px, frame 2 → -44px ... */
  const frame = Math.min(Math.max(n, 1), 10)
  return (
    <div style={{
      position: 'absolute', left: x, top: y,
      width: 22, height: 28,
      backgroundImage: `url(${IMG}/mj_shp_num_01.png)`,
      backgroundPosition: `${-frame * 22}px 0`,
      backgroundRepeat: 'no-repeat',
      imageRendering: 'pixelated',
      pointerEvents: 'none',
    }} />
  )
}

/** ====================================================================
 * CMajakCustomDlg 本体
 * ==================================================================== */
export default function CustomDlg({
  initialTab = TAB_CHARA,
  hanCoin = 0,
  hanCoupon = 0,
  currentCharaId = 0,
  currentHaiId   = 0,
  currentBgId    = 0,
  onEquipChange,
  onRequestShop,
  onClose,
}: Props) {
  const player = useAuthStore(state => state.player)
  const [tab,   setTab]   = useState(initialTab)
  const [items, setItems] = useState<CustomItem[]>([])
  const [page,  setPage]  = useState(1)
  const [dialogScale, setDialogScale] = useState(1)
  const ITEMS_PER_PAGE = 8

  useEffect(() => {
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / CUSTOM_W, (window.innerHeight - margin) / CUSTOM_H))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [])

  /* 現在の装備 ID をタブに応じて選択 */
  const currentId = tab === TAB_CHARA ? currentCharaId
                  : tab === TAB_HAI   ? currentHaiId
                  : tab === TAB_BG    ? currentBgId
                  : 0

  /* ドラッグ移動 (OnNcHitTest: pt.y < 31 → HTCAPTION 相当) */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })

  const onDragStart = (e: React.MouseEvent) => {
    /* タイトルバー領域 (y < 31) のみドラッグ許可 */
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

  /* アイテム取得 (commandCustomItem = mjkc39e) 応答 commandCustomItemResponse (mjkc40e)
   * 原典: ProcessCommandCustomItem() は G::keyPix(k3e) を付けて mjkc39e を送る
   * 応答フィールド: items[{customId, nKind, Name}]
   * items は配列なので JSON.parse 不要
   *
   * タブ → ItemType マッピング (MajakDef.h):
   *   BG    = 10/11/12
   *   HAI   = 20
   *   CHARA = 30/31/32
   *   OTHER = 上記以外
   *
   * 画像ファイル: MAJAK4_ITEM_LIST_CUSTOM_ITEM + ItemID + .him
   *   ItemID < 100 のみ %02d。デフォルト衣装(100011)だけ icon_cos.him。
   */
  useEffect(() => {
    setPage(1)
    setItems([])
    const handler = (data: Record<string, unknown>) => {
      const raw = Array.isArray(data.items)
        ? data.items as Array<{ customId: number; nKind?: number; Name?: string }>
        : []

      const isBg    = (itemType: number) => itemType >= 10 && itemType < 20
      const isHai   = (itemType: number) => itemType >= 20 && itemType < 30
      const isChara = (itemType: number) => itemType >= 30 && itemType < 40

      // 現在のタブの kind でフィルタリング
      const filtered = raw.filter(x => {
        const itemType = Number(x.nKind ?? 0)
        if (tab === TAB_BG) return isBg(itemType)
        if (tab === TAB_HAI) return isHai(itemType)
        if (tab === TAB_CHARA) return isChara(itemType)
        return !isBg(itemType) && !isHai(itemType) && !isChara(itemType)
      })

      setItems(filtered.map(x => {
        const itemId = Number(x.customId)
        const itemType = Number(x.nKind ?? 0)
        const fileId = itemId < 100 ? String(itemId).padStart(2, '0') : String(itemId)
        return {
          itemId,
          itemType,
          itemName:  x.Name ?? `アイテム #${itemId}`,
          imageFile: tab === TAB_CHARA && itemId === 100011 ? 'icon_cos.png' : `mj_custom_${fileId}.png`,
          equipped: itemId === currentId,
        }
      }))
    }
    // mjkc40e = Cmd.CustomItemResponse
    SignalR.on('mjkc40e', handler)
    SignalR.send('mjkc39e', { k3e: player?.pix ?? '' }).catch(() => {})
    return () => SignalR.off('mjkc40e', handler)
  }, [tab, player?.pix, currentId])

  const totalPages  = Math.max(1, Math.ceil(items.length / ITEMS_PER_PAGE))
  const pageItems   = items.slice((page - 1) * ITEMS_PER_PAGE, page * ITEMS_PER_PAGE)
  const numItems    = pageItems.length  // nItemNumOfPage

  /**
   * setItem(idx) — commandSetCustomItem (mjkc37e)
   * レガシー ProcessCommand_SetCustomItem:
   *   DB更新のみ。クライアントへの応答パケットはない。
   */
  const onSetItem = async (item: CustomItem) => {
    try {
      await SignalR.send('mjkc37e', { k3e: player?.pix ?? '', 'mjkk138e': item.itemId })
      setItems(prev => prev.map(x => ({ ...x, equipped: x.itemId === item.itemId })))
      onEquipChange?.({ itemId: item.itemId, itemType: item.itemType })
      SignalR.send('mjkc39e', { k3e: player?.pix ?? '' }).catch(() => {})
    } catch {
      showError('サーバーへの送信に失敗しました')
    }
  }

  /** OnBtnShopClicked — カスタムアイテムショップを開く */
  const handleOpenShop = () => {
    if (onRequestShop) {
      onRequestShop()
      return
    }
    SignalR.send('mjkc35e', { k3e: player?.pix ?? '' }).catch(() => {})
    onClose()
  }

  /* OFFSET_X(i) = 155 * (i % 4),  OFFSET_Y(i) = 175 * (i / 4) */
  const ox = (i: number) => 155 * (i % 4)
  const oy = (i: number) => 175 * Math.floor(i / 4)

  const tabDefs = [
    { src: `${IMG}/mj_shp_tab_06.png`, x: 14,  tab: TAB_CHARA, label: 'キャラ' },
    { src: `${IMG}/mj_shp_tab_07.png`, x: 159, tab: TAB_HAI,   label: '牌'   },
    { src: `${IMG}/mj_shp_tab_08.png`, x: 304, tab: TAB_BG,    label: '背景' },
    { src: `${IMG}/mj_shp_tab_09.png`, x: 449, tab: TAB_OTHER, label: 'その他' },
  ]

  return (
    /* モーダルオーバーレイ */
    <div
      style={{
        position: dialogScale < 1 ? 'fixed' : 'absolute', inset: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: 'rgba(0,0,0,0.45)',
        zIndex: 300,
      }}
    >
      <div style={{ width: CUSTOM_W * dialogScale, height: CUSTOM_H * dialogScale }}>
      {/* CMajakCustomDlg クライアント領域: 662×514px */}
      <div
        style={{
          position: 'relative',
          width: CUSTOM_W,
          height: CUSTOM_H,
          left: dialogScale < 1 ? 0 : pos.x,
          top: dialogScale < 1 ? 0 : pos.y,
          transform: `scale(${dialogScale})`,
          transformOrigin: 'top left',
        }}
        onMouseDown={dialogScale < 1 ? undefined : onDragStart}
      >
        {/* ================================================================
            背景: mj_custom_window.png (662×514) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            DrawTransparent(&dc, 0, 0, 0) → 全体描画
            ================================================================ */}
        <img
          src={`${IMG}/mj_custom_window.png`}
          alt=""
          draggable={false}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: CUSTOM_W,
            height: CUSTOM_H,
            userSelect: 'none',
          }}
        />

        {/* ================================================================
            タイトル "カスタム設定" (OnPaint)
            DrawText CRect(218,7,445,22), DT_CENTER, 15px bold 白
            ================================================================ */}
        <div
          style={{
            position: 'absolute',
            left: 218, top: 7, width: 227, height: 15,
            fontFamily: "'MS PGothic', 'Noto Sans JP', 'Noto Sans JP', 'MS UI Gothic', sans-serif",
            fontSize: 15, fontWeight: 'bold', color: '#fff',
            textAlign: 'center', pointerEvents: 'none',
          }}
        >
          カスタム設定
        </div>

        {/* ================================================================
            タブボタン (COwnerCheckBox, 145×33 per frame, 4フレーム)
            CreateCheckBox(..., CPoint(x, 36), ...)
            ================================================================ */}
        {tabDefs.map(td => (
          <TabButton
            key={td.tab}
            src={td.src}
            frameW={145} frameH={33}
            x={td.x} y={36}
            active={tab === td.tab}
            onClick={() => setTab(td.tab)}
          />
        ))}

        {/* ================================================================
            プレート: mj_shp_window_plate_05.png (148×163, 1フレーム)
            OnPaint: DrawTransparent(&dc, 24+155*i, 91, 0) 上段
                     DrawTransparent(&dc, 24+155*i, 266, 0) 下段
            ================================================================ */}
        {Array.from({ length: 4 }).map((_, i) => (
          i < numItems && (
            <img
              key={`plate-top-${i}`}
              src={`${IMG}/mj_shp_window_plate_05.png`}
              alt=""
              draggable={false}
              style={{
                position: 'absolute',
                left: 24 + 155 * i,
                top: 91,
                width: 148,
                height: 163,
                pointerEvents: 'none',
              }}
            />
          )
        ))}
        {Array.from({ length: 4 }).map((_, i) => (
          (i + 4) < numItems && (
            <img
              key={`plate-bot-${i}`}
              src={`${IMG}/mj_shp_window_plate_05.png`}
              alt=""
              draggable={false}
              style={{
                position: 'absolute',
                left: 24 + 155 * i,
                top: 266,
                width: 148,
                height: 163,
                pointerEvents: 'none',
              }}
            />
          )
        ))}

        {/* ================================================================
            アイテム画像 + アイテム名 + 装備ボタン
            OnPaint: m_pItemImage[i]->Draw(&dc, 33+ox, 138+oy, 0)
                     DrawText(name, CRect(32+ox,100+oy, 32+132+ox,100+24+oy), DT_CENTER)
            btnItemSet[i].Create(..., 36+ox, 219+oy, ...)
            ================================================================ */}
        {pageItems.map((item, i) => {
          const isEquipped = tab === TAB_OTHER || item.equipped || item.itemId === currentId
          return (
            <div key={item.itemId} style={{ position: 'absolute', left: 0, top: 0 }}>
              {/* アイテム画像 — items/custom/ 以下の画像ファイル (130×73, 1フレーム) */}
              <img
                src={`${IMG_ITEM}/${item.imageFile}`}
                alt={item.itemName}
                draggable={false}
                style={{
                  position: 'absolute',
                  left: 33 + ox(i),
                  top: 138 + oy(i),
                  width: 130,
                  height: 73,
                  objectFit: 'contain',
                  pointerEvents: 'none',
                }}
                onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
              />

              {/* アイテム名テキスト (12px bold 黒, DT_CENTER) */}
              <div
                style={{
                  position: 'absolute',
                  left: 32 + ox(i),
                  top: 100 + oy(i),
                  width: 132,
                  height: 24,
                  fontFamily: "'MS PGothic', 'Noto Sans JP', 'Noto Sans JP', 'MS UI Gothic', sans-serif",
                  fontSize: 12, fontWeight: 'bold', color: '#000',
                  textAlign: 'center',
                  overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
                  pointerEvents: 'none',
                }}
              >
                {item.itemName}
              </div>

              {/* 装備ボタン: mj_btn_wear.png (480×27, 4フレーム 120×27)
                  btnItemSet[i].Create(WS_DISABLED → 現在装備と異なる場合のみ有効)
                  at (36+155*(i%4), 219+175*(i/4)) */}
              <SpriteButton
                src={`${IMG}/mj_btn_wear.png`}
                frameW={120} frameH={27}
                x={36 + ox(i)} y={219 + oy(i)}
                onClick={() => onSetItem(item)}
                disabled={isEquipped}
                title={isEquipped ? '装備中' : '装備する'}
              />
            </div>
          )
        })}

        {/* ================================================================
            ページ数字: mj_shp_num_01.png (220×28, 10フレーム 22×28)
            m_pNum01Image->DrawTransparent(&dc, 298, 457, m_nPageNo)
            m_pNum01Image->DrawTransparent(&dc, 344, 457, m_nPageMax)
            ================================================================ */}
        <PageNum n={page}       x={298} y={457} />
        <PageNum n={totalPages} x={344} y={457} />

        {/* ================================================================
            ページダウン: mj_shp_pagedown.png (104×42, 4フレーム 26×42) at (257,451)
            IDC_BTN_LARROW → 前ページへ
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_pagedown.png`}
          frameW={26} frameH={42}
          x={257} y={451}
          onClick={() => setPage(p => p <= 1 ? totalPages : p - 1)}
          disabled={totalPages <= 1}
          title="前のページ"
        />

        {/* ================================================================
            ページアップ: mj_shp_pageup.png (104×42, 4フレーム 26×42) at (382,451)
            IDC_BTN_RARROW → 次ページへ
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_pageup.png`}
          frameW={26} frameH={42}
          x={382} y={451}
          onClick={() => setPage(p => p >= totalPages ? 1 : p + 1)}
          disabled={totalPages <= 1}
          title="次のページ"
        />

        {/* ================================================================
            ショップバナー: customshop_banner_0430.png (920×58, 4フレーム 230×58)
            m_btnShop.Create(0, ..., 14, 443, ..., IDC_BTN_CUSTOM_SHOP_OPEN)
            ================================================================ */}
        <SpriteButton
          src={`${IMG_ITEM}/customshop_banner_0430.png`}
          frameW={230} frameH={58}
          x={14} y={443}
          onClick={handleOpenShop}
          title="カスタムショップ"
        />

        {/* ================================================================
            残高テキスト (OnPaint: 12px bold 黒, DT_RIGHT)
            m_strMessage[0]: CRect(150,453,235,464) ← GEM
            m_strMessage[1]: CRect(150,480,235,491) ← 商品券
            ================================================================ */}
        <div
          style={{
            position: 'absolute',
            left: 150, top: 453, width: 85, height: 11,
            fontFamily: "'MS PGothic', 'Noto Sans JP', 'Noto Sans JP', 'MS UI Gothic', sans-serif",
            fontSize: 12, fontWeight: 'bold', color: '#000',
            textAlign: 'right', pointerEvents: 'none',
          }}
        >
          {hanCoin.toLocaleString('ja-JP')}円
        </div>
        <div
          style={{
            position: 'absolute',
            left: 150, top: 480, width: 85, height: 11,
            fontFamily: "'MS PGothic', 'Noto Sans JP', 'Noto Sans JP', 'MS UI Gothic', sans-serif",
            fontSize: 12, fontWeight: 'bold', color: '#000',
            textAlign: 'right', pointerEvents: 'none',
          }}
        >
          {hanCoupon.toLocaleString('ja-JP')}円
        </div>

        {/* ================================================================
            閉じるボタン: mj_shp_btn_close.png (352×32, 4フレーム 88×32) at (560,469)
            m_btnClose.Create(0, ..., 560, 469, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_close.png`}
          frameW={88} frameH={32}
          x={560} y={469}
          onClick={onClose}
          title="閉じる"
        />
      </div>
      </div>
    </div>
  )
}
