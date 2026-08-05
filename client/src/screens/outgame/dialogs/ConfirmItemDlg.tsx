/**
 * CMJConfirmItemDlg 相当 — アイテム有効期間確認 (소지품 화면 / AP-09 §3-2-7)
 * 原典: legacy/client/HgMajak2/MJConfirmItemDlg.h/cpp
 *
 * ── ウィンドウ ───────────────────────────────────────────────────────────────
 *  MoveWindow(0, 0, 642, 522)                                       [.cpp L93]
 *  CenterWindow(GetParent())                                        [.cpp L94]
 *  OnNcHitTest: pt.y < 41 → HTCAPTION (ドラッグ可能)                 [.cpp L162]
 *
 * ── 画像配置 (.cpp L101-153) ───────────────────────────────────────────────
 *  背景 mj_shp_window_exchange.png (642×522, 1f) at (0,0)
 *  プレート (613×75 1f) at (14, 83 + 79*i)  i = 0..4
 *    pItemData (便利アイテム):
 *      bUseFlag && avcode == "MJ20" → plate02   (.cpp L125)
 *      bUseFlag && avcode != "MJ20" → plate02b  (.cpp L125)
 *      !bUseFlag                    → plate01
 *    pItemData2 (装備アイテム):
 *      bUseFlag  → plate02                       (.cpp L135)
 *      !bUseFlag → plate01
 *  アイテム画像 at (20, 88 + 79*i)
 *  装備ボタン mj_shp_btn_equip.png (67×21, 4f) at (177, 126 + 79*i)
 *    pItemData    : 常に SW_HIDE (消費型のため装備不可)           (.cpp L362)
 *    pItemData2 && bUseFlag  : SW_HIDE  (装備中は非表示)          (.cpp L379)
 *    pItemData2 && !bUseFlag : SW_SHOW                            (.cpp L383)
 *    EnableWindow: 現在時刻 < tmValidTo && nQuantity > 0
 *
 *  ページ操作 (.cpp L88-90):
 *    pagedown mj_shp_pagedown_b.png (42×26, 4f) at (225, 479)
 *      OnBtnPageDownClicked: m_nTopIndex++                          (.cpp L218)
 *      disable: m_nTopIndex + DRAW_MAX(5) >= 全件数
 *    pageup   mj_shp_pageup_b.png   (42×26, 4f) at (375, 479)
 *      OnBtnPageUpClicked: m_nTopIndex--                            (.cpp L228)
 *      disable: m_nTopIndex <= 0
 *    両ボタンとも全件数が DRAW_MAX(5) 以下なら共に EnableWindow(FALSE) (.cpp L407)
 *
 *  閉じる mj_shp_btn_close.png (88×32, 4f) at (277, 479)              (.cpp L91)
 *
 * ── テキスト (.cpp L121-148) ───────────────────────────────────────────────
 *  アイテム名   CRect(97,  109+79*i, 244, 120+79*i)  DT_LEFT 12px bold 黒
 *    pItemData : 名前から "(1個)" "(6個)" を Replace                 (.cpp L130-131)
 *    pItemData2: 名前から "(1個)" を Replace                          (.cpp L140)
 *  有効期間     CRect(257, 109+79*i, 620, 120+79*i)  DT_LEFT 12px bold 黒
 *    EIL_TIMELIMITED  → "yyyy年MM月dd日HH時mm分 から yyyy年MM月dd日HH時mm分 まで"
 *    その他           → "残り：N個"                                  (.cpp L303-306)
 *
 * ── データ取得 (mjkc43e GetMajItemList) ────────────────────────────────────
 *  原典: theApp.m_UserInfo.m_listMajItem を直接参照 (channel join 時の cache)
 *  Web 版では SignalR で都度問い合わせる (server: GetMajItemListCommand)
 *  応答: k25e=count, mjkk58e{i}=itemCode, mjkk59e{i}=buyDt, mjkk60e{i}=endDt,
 *        mjkk140e{i}=qty, mjkk61e{i}=useFlag
 *
 *  アイテム分類: shopItemData.ts の静的配列で照合 (CMJItemManager 相当)
 *    SHOP_ITEM_DATA_BUY.find(d => d.avCode === itemCode) → pItemData
 *    SHOP_ITEM_DATA_EXC.find(d => d.itemCode === itemCode) → pItemData2
 *  どちらにも該当しないアイテムはレガシー同様スキップする (.cpp L260-345)
 *
 * ── 装備処理 (.cpp L242-258) ────────────────────────────────────────────────
 *  OnBtnUtenExcClicked(i):
 *    EnableWindow(FALSE) で多重送信を防止
 *    SendSelectItem(pItemData->m_strAvCode[0] | pItemData2->m_strItemCode)
 *    成功時 ProcessSelectItemCommand → updateOwnItem + updateEquipButton + Invalidate
 *    失敗時 ErrorSelectItemCommand(message) → MessageBox + updateEquipButton
 */
import { useRef, useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError, selectMajItemErrorMessage } from '../../../utils/msgbox'
import { useAuthStore } from '../../../store/authStore'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'
import {
  SHOP_ITEM_DATA_BUY, SHOP_ITEM_DATA_EXC,
  type BuyItemData, type ExcItemData,
} from './shopItemData'

const IMG     = '/assets/images/game'
const IMG_ITM = `${IMG}/items`
const DEBUG_LABEL = '[ConfirmItemDlg]'

// レガシー定数
const DRAW_MAX = 5  // DRAW_ITEM_COUNT_MAX
const LIST_MAX = 7  // LIST_ITEM_COUNT_MAX
const DIALOG_W = 642
const DIALOG_H = 522

// EItemLifeType (HgMajak2.cpp L24)
//   tmValidTo >= 2038-01-01 → EIL_PERMANENT
//   tmValidTo >= 2037-01-01 → EIL_QUANTITYLIMITED
//   else                    → EIL_TIMELIMITED
const TM_PERMANENT_BASE     = new Date(2038, 0, 1).getTime() / 1000
const TM_QUANTITYLIMIT_BASE = new Date(2037, 0, 1).getTime() / 1000

/** サーバ応答の生アイテム情報 */
export interface RawMajItem {
  itemCode: string
  buyDt:    number  // epoch sec
  endDt:    number  // epoch sec
  qty:      number
  useFlag:  number  // 0 or 1
}

/** 分類済みアイテムスロット (CMJConfirmItemDlg::SLOT 相当) */
interface SlotInfo {
  raw:      RawMajItem
  buy?:     BuyItemData   // pItemData (便利アイテム)
  exc?:     ExcItemData   // pItemData2 (装備アイテム)
  useFlag:  boolean
}

const BUY_ITEM_BY_USE_CODE: Record<string, BuyItemData | undefined> = {
  MJ20: SHOP_ITEM_DATA_BUY.find(d => d.itemIndex === 0),
  MJ21: SHOP_ITEM_DATA_BUY.find(d => d.itemIndex === 3),
  MJ22: SHOP_ITEM_DATA_BUY.find(d => d.itemIndex === 7),
}

interface Props {
  onClose: () => void
  majItems: RawMajItem[]
  onMajItemsChange?: (items: RawMajItem[]) => void
}

/** ====================================================================
 * CMJBmpButton 相当 — 4 フレームスプライト
 * normal=0, disabled=1, hover=2, pressed=3
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

// ─────────────────────────────────────────────────────────────────────────
// ヘルパー: 性別取得 (theApp.GetFrame()->m_Member.m_szSex 相当)
// ─────────────────────────────────────────────────────────────────────────
function useSexIndex(): 0 | 1 {
  const sex = useAuthStore(s => s.player?.sex ?? 'M')
  return sex === 'M' ? 0 : 1
}

// ─────────────────────────────────────────────────────────────────────────
// 生アイテム → スロット (分類 + フィルタ)
//   レガシー: CMJItemManager::GetItemData / GetItemData2 の存在チェック
//   どちらにも該当しない (廃止コード等) はリストに含めない
// ─────────────────────────────────────────────────────────────────────────
function classifyItems(items: RawMajItem[]): SlotInfo[] {
  const out: SlotInfo[] = []
  for (const r of items) {
    // .cpp L267: pItemData = CMJItemManager::GetItemData(item.strItemCode)
    const buy = BUY_ITEM_BY_USE_CODE[r.itemCode] ?? SHOP_ITEM_DATA_BUY.find(d => d.avCode === r.itemCode)
    if (buy) {
      out.push({ raw: r, buy, useFlag: r.useFlag !== 0 })
      continue
    }
    // .cpp L312: pItemData2 = CMJItemManager::GetItemData2(item.strItemCode)
    const exc = SHOP_ITEM_DATA_EXC.find(d => d.itemCode === r.itemCode)
    if (exc) {
      out.push({ raw: r, exc, useFlag: r.useFlag !== 0 })
      continue
    }
    // どちらにも該当しないアイテムは表示しない (レガシーと同じ)
  }
  // LIST_ITEM_COUNT_MAX で打ち切り (.cpp L274)
  return out.slice(0, LIST_MAX)
}

// ─────────────────────────────────────────────────────────────────────────
// プレート画像 (.cpp L125-135)
// ─────────────────────────────────────────────────────────────────────────
function plateSrc(slot: SlotInfo | null): string {
  if (!slot) return `${IMG}/mj_shp_window_exchange_plate_01.png`
  if (slot.buy) {
    if (slot.useFlag) {
      // avCode == "MJ20" → plate02, それ以外 → plate02b
      return slot.raw.itemCode === 'MJ20'
        ? `${IMG}/mj_shp_window_exchange_plate_02.png`
        : `${IMG}/mj_shp_window_exchange_plate_02b.png`
    }
    return `${IMG}/mj_shp_window_exchange_plate_01.png`
  }
  // pItemData2
  return slot.useFlag
    ? `${IMG}/mj_shp_window_exchange_plate_02.png`
    : `${IMG}/mj_shp_window_exchange_plate_01.png`
}

// ─────────────────────────────────────────────────────────────────────────
// アイテム画像 (.cpp L282-326)
//   pItemData :
//     avcode == "MJ20" → mj_shop_item_sell_coin_b01
//     avcode == "MJ21" → mj_shop_item_sell_ryu_b01
//     avcode == "MJ22" → mj_shop_item_sell_ryu_b02
//     else            → 画像なし
//   pItemData2:
//     itemCode == "MJ23" → mj_shop_item_sell_coin_b02 (ハイ卓場代無料)
//     else              → m_strImagePath[nSex] (= SHOP_ITEM_DATA_EXC.imagePath)
// ─────────────────────────────────────────────────────────────────────────
function itemImageSrc(slot: SlotInfo, sexIndex: 0 | 1): string | null {
  if (slot.buy) {
    switch (slot.raw.itemCode) {
      case 'MJ20': return `${IMG_ITM}/mj_shop_item_sell_coin_b01.png`
      case 'MJ21': return `${IMG_ITM}/mj_shop_item_sell_ryu_b01.png`
      case 'MJ22': return `${IMG_ITM}/mj_shop_item_sell_ryu_b02.png`
      default:     return null   // レガシーと同じく非表示
    }
  }
  if (slot.exc) {
    if (slot.exc.itemCode === 'MJ23')
      return `${IMG_ITM}/mj_shop_item_sell_coin_b02.png`
    if (sexIndex === 1 && slot.exc.imagePathFemale) return slot.exc.imagePathFemale
    return slot.exc.imagePath
  }
  return null
}

// ─────────────────────────────────────────────────────────────────────────
// アイテム名 (.cpp L130-141)
//   pItemData : "(1個)" "(6個)" を Replace
//   pItemData2: "(1個)" を Replace
// ─────────────────────────────────────────────────────────────────────────
function itemDisplayName(slot: SlotInfo): string {
  if (slot.buy) {
    return slot.buy.name.replace(/\(1個\)/g, '').replace(/\(6個\)/g, '')
  }
  if (slot.exc) {
    return slot.exc.name.replace(/\(1個\)/g, '')
  }
  return ''
}

// ─────────────────────────────────────────────────────────────────────────
// 有効期間テキスト (.cpp L303-336)
// ─────────────────────────────────────────────────────────────────────────
function formatTerm(raw: RawMajItem): string {
  // GetItemLifeType (HgMajak2.cpp L24)
  if (raw.endDt >= TM_PERMANENT_BASE || raw.endDt >= TM_QUANTITYLIMIT_BASE) {
    // EIL_PERMANENT / EIL_QUANTITYLIMITED → "残り：N個"
    return `残り：${raw.qty}個`
  }
  // EIL_TIMELIMITED → "yyyy年MM月dd日HH時mm分 から ... まで"
  const fmt = (sec: number) => {
    const d = new Date(sec * 1000)
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}年${pad(d.getMonth() + 1)}月${pad(d.getDate())}日`
         + `${pad(d.getHours())}時${pad(d.getMinutes())}分`
  }
  return `${fmt(raw.buyDt)} から ${fmt(raw.endDt)} まで`
}

function packetValue(data: Record<string, unknown>, key: string): unknown {
  if (key in data) return data[key]
  const lowerKey = key.toLowerCase()
  const foundKey = Object.keys(data).find(k => k.toLowerCase() === lowerKey)
  return foundKey ? data[foundKey] : undefined
}

function resultValue(data: Record<string, unknown>): unknown {
  return packetValue(data, 'k1e') ?? packetValue(data, 'result')
}

function isFailureResult(result: unknown): boolean {
  return result === 0 || result === 'failure' || result === 'v2e'
}

function readCount(data: Record<string, unknown>): number {
  return Number(packetValue(data, 'k25e') ?? data.count ?? 0)
}

function readIndexed(data: Record<string, unknown>, key: string, index: number): unknown {
  const indexed = packetValue(data, `${key}${index}`)
    ?? packetValue(data, `${key}_${index}`)
  if (indexed !== undefined) return indexed

  const value = packetValue(data, key)
  return Array.isArray(value) ? value[index] : value
}

function readAny(data: Record<string, unknown>, keys: string[]): unknown {
  for (const key of keys) {
    const value = packetValue(data, key)
    if (value !== undefined) return value
  }
  return undefined
}

function parseUseFlag(value: unknown): number {
  if (typeof value === 'boolean') return value ? 1 : 0
  if (typeof value === 'number') return value !== 0 ? 1 : 0
  if (typeof value === 'string') {
    const normalized = value.trim().toUpperCase()
    return normalized === '1' || normalized === 'Y' || normalized === 'TRUE' || normalized === 'V1E' ? 1 : 0
  }
  return 0
}

export function normalizeRawMajItem(item: Record<string, unknown>): RawMajItem {
  return {
    itemCode: String(readAny(item, ['itemCode', 'ItemCode', 'mjkk58e']) ?? ''),
    buyDt: Number(readAny(item, ['buyDt', 'buyDate', 'BuyDt', 'BuyDate', 'mjkk59e']) ?? 0),
    endDt: Number(readAny(item, ['endDt', 'endDate', 'EndDt', 'EndDate', 'mjkk60e']) ?? 0),
    qty: Number(readAny(item, ['qty', 'quantity', 'Qty', 'Quantity', 'mjkk140e']) ?? 0),
    useFlag: parseUseFlag(readAny(item, ['useFlag', 'UseFlag', 'mjkk61e'])),
  }
}

function logConfirmItemDebug(message: string, payload: Record<string, unknown>) {
  if (import.meta.env.DEV) console.info(DEBUG_LABEL, message, payload)
}

// ─────────────────────────────────────────────────────────────────────────
// 装備ボタン表示判定 (.cpp L362-396)
// ─────────────────────────────────────────────────────────────────────────
function equipBtnState(slot: SlotInfo): { visible: boolean; enabled: boolean } {
  // 共通: 期限切れ or 数量 0 なら EnableWindow(FALSE)
  // レガシー: CTime::GetCurrentTime() < item.tmValidTo && item.nQuantity > 0
  const nowSec  = Date.now() / 1000
  const enabled = nowSec < slot.raw.endDt && slot.raw.qty > 0

  if (slot.buy) {
    // pItemData は常に SW_HIDE
    return { visible: false, enabled }
  }
  // pItemData2: bUseFlag で表示切替
  return { visible: !slot.useFlag, enabled }
}

// ─────────────────────────────────────────────────────────────────────────
// CMJConfirmItemDlg 本体
// ─────────────────────────────────────────────────────────────────────────
export default function ConfirmItemDlg({ onClose, majItems, onMajItemsChange }: Props) {
  const sexIdx = useSexIndex()  // 装備画像/名称の性別差異用 (レガシー互換)
  const [slots, setSlots]     = useState<SlotInfo[]>([])
  const [topIdx, setTopIdx]   = useState(0)            // m_nTopIndex
  const [pending, setPending] = useState<number | null>(null)  // 装備リクエスト中 index
  const layoutMode = useOutgameLayoutMode()
  const isMobile = layoutMode !== 'desktop'
  const useResponsiveOwnedItemsDialog = ['desktop', 'mobileLandscape', 'mobilePortrait'].includes(layoutMode)
  const [dialogScale, setDialogScale] = useState(1)

  /* ─── ドラッグ移動 (OnNcHitTest: pt.y < 41 → HTCAPTION) ─── */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })
  const onDragStart = (e: React.MouseEvent) => {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    if (e.clientY - rect.top >= 41) return
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
    if (!isMobile) {
      setDialogScale(1)
      return
    }
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / DIALOG_W, (window.innerHeight - margin) / DIALOG_H))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [isMobile])

  /* ─── GetMajItemList (mjkc43e): モーダルを開くたびに最新の所持品を取得 ─── */
  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      if (isFailureResult(resultValue(data))) {
        logConfirmItemDebug('mjkc43e failed', { keys: Object.keys(data), data })
        return
      }

      const count = readCount(data)
      const nextItems = Array.from({ length: count }, (_, index) => normalizeRawMajItem({
        itemCode: readIndexed(data, 'mjkk58e', index),
        buyDt: readIndexed(data, 'mjkk59e', index),
        endDt: readIndexed(data, 'mjkk60e', index),
        qty: readIndexed(data, 'mjkk140e', index),
        useFlag: readIndexed(data, 'mjkk61e', index),
      })).filter(item => item.itemCode !== '')

      logConfirmItemDebug('mjkc43e received', { count, items: nextItems })
      onMajItemsChange?.(nextItems)
    }

    SignalR.on('mjkc43e', handler)
    SignalR.send('mjkc43e', {}).catch(() => {
      logConfirmItemDebug('mjkc43e send failed', {})
    })
    return () => SignalR.off('mjkc43e', handler)
  }, [onMajItemsChange])

  useEffect(() => {
    const nextSlots = classifyItems(majItems)
    logConfirmItemDebug('c1e cache parsed', {
      list: majItems,
      slots: nextSlots.map(slot => ({
        itemCode: slot.raw.itemCode,
        rawUseFlag: slot.raw.useFlag,
        useFlag: slot.useFlag,
        type: slot.buy ? 'pItemData' : 'pItemData2',
        plate: plateSrc(slot).split('/').pop(),
        endDt: slot.raw.endDt,
        qty: slot.raw.qty,
        name: itemDisplayName(slot),
      })),
    })
    setSlots(nextSlots)
    setTopIdx(current => Math.min(current, Math.max(0, nextSlots.length - DRAW_MAX)))
  }, [majItems])

  /* ─── ProcessSelectItemCommand 応答ハンドラ (mjkc21e) ─── */
  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      const result = resultValue(data)
      logConfirmItemDebug('mjkc21e received', { result, count: readCount(data), keys: Object.keys(data), data })
      if (isFailureResult(result)) {
        // ErrorSelectItemCommand → MessageBox + updateEquipButton(.cpp L185)
        const failCode = String(data.failCode ?? data.message ?? data.k2e ?? data['mjkk95e'] ?? '')
        showError(failCode !== ''
          ? selectMajItemErrorMessage(failCode)
          : 'アイテムの選択に失敗しました')
        setPending(null)
        return
      }
      // 成功: ProcessSelectItemCommand → updateOwnItem + updateEquipButton(.cpp L177)
      //   レガシー ProcessSelectItemCommand は mjkk58e{i}/mjkk61e{i} で UseFlag だけを更新する。
      const count = readCount(data)
      if (count > 0) {
        onMajItemsChange?.(majItems.map(raw => {
          for (let i = 0; i < count; i++) {
            const itemCode = String(readIndexed(data, 'mjkk58e', i) ?? '')
            if (itemCode !== raw.itemCode) continue
            return { ...raw, useFlag: parseUseFlag(readIndexed(data, 'mjkk61e', i)) }
          }
          return raw
        }))
        setSlots(prev => prev.map(slot => {
          for (let i = 0; i < count; i++) {
            const itemCode = String(readIndexed(data, 'mjkk58e', i) ?? '')
            if (itemCode !== slot.raw.itemCode) continue
            const useFlag = readIndexed(data, 'mjkk61e', i)
            const nextUseFlag = parseUseFlag(useFlag)
            return {
              ...slot,
              raw: { ...slot.raw, useFlag: nextUseFlag },
              useFlag: nextUseFlag !== 0,
            }
          }
          return slot
        }))
      }
      setPending(null)
    }
    SignalR.on('mjkc21e', handler)
    return () => SignalR.off('mjkc21e', handler)
  }, [majItems, onMajItemsChange])

  /* ─── OnBtnUtenExcClicked(i) (.cpp L242) ─── */
  const onEquip = (i: number, slot: SlotInfo) => {
    // EnableWindow(FALSE) 相当
    setPending(i)
    const itemCode = slot.buy ? slot.raw.itemCode : slot.exc?.itemCode
    if (!itemCode) { setPending(null); return }
    // SendSelectItem: mjkc21e, mjkk58e = itemCode
    SignalR.send('mjkc21e', { 'mjkk58e': itemCode }).catch(() => {
      setPending(null)
      showError('サーバーへの送信に失敗しました')
    })
  }

  /* ─── 表示スロット (topIdx 〜 topIdx + DRAW_MAX) ─── */
  const visible: (SlotInfo | null)[] = Array.from(
    { length: DRAW_MAX },
    (_, i) => slots[topIdx + i] ?? null,
  )

  /* ─── ページボタン有効/無効 (.cpp L397-417) ─── */
  // 全件 > DRAW_MAX(5) の時のみ pagedown/pageup が有効化される
  const hasOverflow   = slots.length > DRAW_MAX
  const canPageDown   = hasOverflow && topIdx + DRAW_MAX < slots.length  // 下方向にまだある
  const canPageUp     = hasOverflow && topIdx > 0                        // 上方向にまだある

  if (useResponsiveOwnedItemsDialog) {
    return (
      <div className="responsive-owned-items-overlay" role="dialog" aria-modal="true" aria-label="所持アイテム">
        <section className="responsive-owned-items">
          <header className="responsive-owned-items__header">
            <div>
              <p>MAJAK4 STORE</p>
              <h2>所持アイテム</h2>
            </div>
            <button type="button" onClick={onClose} aria-label="閉じる">x</button>
          </header>
          <main className="responsive-owned-items__content">
            {slots.length === 0 ? (
              <p className="responsive-owned-items__empty">所持アイテムはありません。</p>
            ) : slots.map((slot, index) => {
              const imageUrl = itemImageSrc(slot, sexIdx)
              const equip = equipBtnState(slot)
              return (
                <article className="responsive-owned-items__row" key={`${slot.raw.itemCode}-${index}`}>
                  <div className="responsive-owned-items__image">
                    {imageUrl && <img src={imageUrl} alt="" />}
                  </div>
                  <div className="responsive-owned-items__detail">
                    <div className="responsive-owned-items__title">
                      <h3>{itemDisplayName(slot)}</h3>
                      <span className={slot.useFlag ? 'is-equipped' : ''}>{slot.useFlag ? '使用中' : '所持中'}</span>
                    </div>
                    <p>{formatTerm(slot.raw)}</p>
                  </div>
                  {equip.visible && (
                    <button
                      className="responsive-owned-items__equip"
                      type="button"
                      disabled={!equip.enabled || pending === index}
                      onClick={() => onEquip(index, slot)}
                    >
                      装備
                    </button>
                  )}
                </article>
              )
            })}
          </main>
          <footer className="responsive-owned-items__footer">
            <button type="button" onClick={onClose}>閉じる</button>
          </footer>
        </section>
        <style>{`
          .responsive-owned-items-overlay { position: absolute; inset: 0; z-index: 400; display: grid; place-items: center; padding: 12px; overflow: hidden; background: rgba(8,16,20,.76); box-sizing: border-box; font-family: var(--majak-font-family-ui); }
          .responsive-owned-items { width: min(720px, 100%); height: min(100%, 560px); min-height: 0; display: flex; flex-direction: column; overflow: hidden; color: #18312b; background: #f8f5ec; border: 1px solid #829287; box-shadow: 0 18px 54px rgba(0,0,0,.42); }
          .responsive-owned-items__header { display: flex; align-items: center; justify-content: space-between; padding: 10px 14px; color: #fff; background: #174b43; }
          .responsive-owned-items__header p { display: none; }
          .responsive-owned-items__header h2 { margin: 0; font-size: calc(18px * var(--majak-type-scale)); line-height: 1; }
          .responsive-owned-items__header button { width: 29px; height: 29px; border: 1px solid rgba(255,255,255,.75); color: #fff; background: transparent; font-size: calc(18px * var(--majak-type-scale)); cursor: pointer; }
          .responsive-owned-items__content { min-height: 0; flex: 1; padding: 10px; overflow: auto; overscroll-behavior: contain; }
          .responsive-owned-items__row { display: grid; grid-template-columns: 52px minmax(0,1fr) auto; gap: 10px; align-items: center; min-height: 62px; padding: 7px; border-bottom: 1px solid #d5ddd2; background: #fffdf8; }
          .responsive-owned-items__row:nth-child(even) { background: #f0f4ea; }
          .responsive-owned-items__image { width: 52px; height: 52px; display: grid; place-items: center; background: #e6ece1; }
          .responsive-owned-items__image img { max-width: 100%; max-height: 100%; object-fit: contain; }
          .responsive-owned-items__detail { min-width: 0; }
          .responsive-owned-items__title { display: flex; gap: 7px; align-items: center; }
          .responsive-owned-items__title h3 { min-width: 0; margin: 0; overflow: hidden; color: #1f302b; font: 700 calc(14px * var(--majak-type-scale))/1.2 var(--majak-font-family-ui); text-overflow: ellipsis; white-space: nowrap; }
          .responsive-owned-items__title span { flex: none; padding: 3px 5px; color: #607069; background: #e3e9df; font: 700 calc(10px * var(--majak-type-scale))/1 var(--majak-font-family-ui); }
          .responsive-owned-items__title span.is-equipped { color: #fff; background: #b84228; }
          .responsive-owned-items__detail p { margin: 5px 0 0; overflow: hidden; color: #5b6d66; font: calc(11px * var(--majak-type-scale))/1.25 var(--majak-font-family-ui); text-overflow: ellipsis; white-space: nowrap; }
          .responsive-owned-items__equip, .responsive-owned-items__footer button { border: 0; border-radius: 3px; padding: 8px 11px; color: #fff; background: #1b5b4d; font: 700 calc(12px * var(--majak-type-scale))/1 var(--majak-font-family-ui); cursor: pointer; }
          .responsive-owned-items__equip:disabled { color: #84908a; background: #d6ddd5; cursor: not-allowed; }
          .responsive-owned-items__empty { margin: 0; padding: 34px 10px; color: #647069; text-align: center; font: calc(13px * var(--majak-type-scale)) var(--majak-font-family-ui); }
          .responsive-owned-items__footer { display: flex; justify-content: flex-end; padding: 9px 14px; border-top: 1px solid #c8d0c2; background: #e7ede4; }
          @media (max-width: 420px) { .responsive-owned-items-overlay { padding: 0; } .responsive-owned-items { width: 100%; height: 100%; } .responsive-owned-items__content { padding: 8px; } .responsive-owned-items__row { grid-template-columns: 44px minmax(0,1fr) auto; gap: 7px; min-height: 54px; padding: 6px; } .responsive-owned-items__image { width: 44px; height: 44px; } .responsive-owned-items__title h3 { font-size: calc(12px * var(--majak-type-scale)); } .responsive-owned-items__detail p { font-size: calc(10px * var(--majak-type-scale)); } .responsive-owned-items__equip { padding: 7px 8px; font-size: calc(11px * var(--majak-type-scale)); } }
        `}</style>
      </div>
    )
  }

  return (
    /* モーダルオーバーレイ */
    <div style={{
      position: isMobile ? 'fixed' : 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.45)', zIndex: 300,
    }}>
      <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
      {/* CMJConfirmItemDlg クライアント領域: 642×522px */}
      <div style={{
        position: 'relative',
        width: DIALOG_W, height: DIALOG_H,
        left: isMobile ? 0 : pos.x, top: isMobile ? 0 : pos.y,
        transform: `scale(${dialogScale})`,
        transformOrigin: 'top left',
      }}>

        {/* 背景: mj_shp_window_exchange.png (タイトル/列ヘッダー焼き込み済み) */}
        <img
          src={`${IMG}/mj_shp_window_exchange.png`}
          alt=""
          draggable={false}
          onMouseDown={onDragStart}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 642, height: 522,
            cursor: 'move', userSelect: 'none',
          }}
        />

        {/* アイテムスロット × DRAW_ITEM_COUNT_MAX(5)  79px 間隔 */}
        {visible.map((slot, i) => {
          const equip = slot ? equipBtnState(slot) : null
          const img   = slot ? itemImageSrc(slot, sexIdx)   : null
          return (
            <div key={i} style={{ position: 'absolute', left: 0, top: 0 }}>
              {/* プレート at (14, 83+79*i) */}
              <img
                src={plateSrc(slot)}
                alt=""
                draggable={false}
                style={{
                  position: 'absolute',
                  left: 14, top: 83 + 79 * i,
                  width: 613, height: 75,
                  pointerEvents: 'none',
                }}
              />

              {slot && (
                <>
                  {/* アイテム画像 at (20, 88+79*i) */}
                  {img && (
                    <img
                      src={img}
                      alt={itemDisplayName(slot)}
                      draggable={false}
                      style={{
                        position: 'absolute',
                        left: 20, top: 88 + 79 * i,
                        objectFit: 'contain',
                        pointerEvents: 'none',
                      }}
                      onError={e => {
                        (e.currentTarget as HTMLImageElement).style.visibility = 'hidden'
                      }}
                    />
                  )}

                  {/* アイテム名 CRect(97, 109+79*i, 244, 120+79*i) DT_LEFT */}
                  <div style={{
                    position: 'absolute',
                    left: 97, top: 109 + 79 * i,
                    width: 147, height: 11,
                    fontFamily: 'var(--majak-font-family-ui)',
                    fontSize: 'calc(12px * var(--majak-type-scale))', fontWeight: 'bold', color: '#000',
                    lineHeight: '11px',
                    textAlign: 'left',
                    overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
                    pointerEvents: 'none',
                  }}>
                    {itemDisplayName(slot)}
                  </div>

                  {/* 有効期間 CRect(257, 109+79*i, 620, 120+79*i) DT_LEFT */}
                  <div style={{
                    position: 'absolute',
                    left: 257, top: 109 + 79 * i,
                    width: 363, height: 11,
                    fontFamily: 'var(--majak-font-family-ui)',
                    fontSize: 'calc(12px * var(--majak-type-scale))', fontWeight: 'bold', color: '#000',
                    lineHeight: '11px',
                    textAlign: 'left',
                    overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
                    pointerEvents: 'none',
                  }}>
                    {formatTerm(slot.raw)}
                  </div>

                  {/* 装備ボタン (.cpp L362-396 — visible/enable をレガシー準拠で判定) */}
                  {equip && equip.visible && (
                    <SpriteButton
                      src={`${IMG}/mj_shp_btn_equip.png`}
                      frameW={67} frameH={21}
                      x={177} y={126 + 79 * i}
                      onClick={() => onEquip(i, slot)}
                      disabled={!equip.enabled || pending === i}
                      title="装備する"
                    />
                  )}
                </>
              )}
            </div>
          )
        })}

        {/* pagedown (LEFT) at (225, 479) — topIdx++ (.cpp L88, L218) */}
        <SpriteButton
          src={`${IMG}/mj_shp_pagedown_b.png`}
          frameW={42} frameH={26}
          x={225} y={479}
          onClick={() => setTopIdx(t => Math.min(slots.length - DRAW_MAX, t + 1))}
          disabled={!canPageDown}
          title="次へ"
        />

        {/* pageup (RIGHT) at (375, 479) — topIdx-- (.cpp L89, L228) */}
        <SpriteButton
          src={`${IMG}/mj_shp_pageup_b.png`}
          frameW={42} frameH={26}
          x={375} y={479}
          onClick={() => setTopIdx(t => Math.max(0, t - 1))}
          disabled={!canPageUp}
          title="前へ"
        />

        {/* 閉じる at (277, 479) IDOK */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_close.png`}
          frameW={88} frameH={32}
          x={277} y={479}
          onClick={onClose}
          title="閉じる"
        />
      </div>
      </div>
    </div>
  )
}
