/**
 * HgMessageBox 相当 — ユーザー通知ユーティリティ
 * レガシー: HgMessageBox(hwnd, message, title, MB_OK | MB_ICONINFORMATION)
 *
 * Legacy C++ との対応:
 *   HgMessageBox(parent, msg, "エラー",        MB_OK) → showError(msg)
 *   HgMessageBox(parent, msg, "お知らせ",      MB_OK) → showMessage(msg)
 *   HgMessageBox(parent, msg, "シリアルコード賞", MB_OK) → showMessage(msg, 'シリアルコード賞')
 *   HgMessageBox(parent, msg, "ミッション賞",   MB_OK) → showMessage(msg, 'ミッション賞')
 */

import { useAuthStore } from '../store/authStore'

export type MessageBoxKind = 'message' | 'confirm'

export interface MessageBoxRequest {
  id: number
  kind: MessageBoxKind
  title: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  resolve: (value: boolean) => void
}

type MessageBoxListener = (requests: MessageBoxRequest[]) => void

let nextMessageBoxId = 1
let pendingRequests: MessageBoxRequest[] = []
const listeners = new Set<MessageBoxListener>()

function notifyMessageBoxListeners(): void {
  const snapshot = [...pendingRequests]
  listeners.forEach(listener => listener(snapshot))
}

function enqueueMessageBox(
  kind: MessageBoxKind,
  message: string,
  title: string,
  confirmLabel?: string,
  cancelLabel?: string,
): Promise<boolean> {
  return new Promise<boolean>(resolve => {
    pendingRequests = [...pendingRequests, {
      id: nextMessageBoxId++,
      kind,
      title,
      message,
      confirmLabel,
      cancelLabel,
      resolve,
    }]
    notifyMessageBoxListeners()
  })
}

export function subscribeMessageBox(listener: MessageBoxListener): () => void {
  listeners.add(listener)
  listener([...pendingRequests])
  return () => listeners.delete(listener)
}

export function resolveMessageBox(id: number, value: boolean): void {
  const request = pendingRequests.find(item => item.id === id)
  if (!request) return
  pendingRequests = pendingRequests.filter(item => item.id !== id)
  request.resolve(value)
  notifyMessageBoxListeners()
}

/** サーバー result 値: 整数 1 or 文字列 "success" = 成功 */
export const RESULT_OK = 1

/**
 * result フィールドが成功値かどうかを判定する
 * サーバーは命令によって整数 (1=成功, 0=失敗) と
 * 文字列 ("success"/"failure") の両方を使う:
 *   整数方式: mjkc32e〜mjkc34e, mjkc20e〜mjkc22e, room:enter 等
 *   文字列方式: mjkc17e (MoneyReplenishment), mjkc18e (ApplyEarnedMoney)
 */
export function isOk(result: unknown): boolean {
  return result === 1 || result === 'success' || result === 'v1e'
}

/**
 * 汎用メッセージ表示 (MB_OK 相当)
 * @param message  表示するメッセージ本文
 * @param title    タイトルバー文字列 (省略時: 'お知らせ')
 */
export function showMessage(message: string, title = 'お知らせ'): Promise<boolean> {
  return enqueueMessageBox('message', message, title)
}

/**
 * 確認メッセージ表示 (MB_OKCANCEL / MB_YESNO 相当)
 * @returns true=OK, false=キャンセル
 */
export function showConfirm(
  message: string,
  title = '確認',
  confirmLabel = 'OK',
  cancelLabel = 'キャンセル',
): Promise<boolean> {
  return enqueueMessageBox('confirm', message, title, confirmLabel, cancelLabel)
}

/**
 * エラーメッセージ表示
 * HgMessageBox(parent, msg, "エラー", MB_OK | MB_ICONINFORMATION) 相当
 */
export function showError(message: string): Promise<boolean> {
  if (message.trim() === 'エラー') console.error('[MessageBox] generic error body', { message, stack: new Error().stack })
  return showMessage(message, 'エラー')
}

/**
 * result 値を確認してエラー時のみ表示する汎用ヘルパー
 * @returns true=成功, false=失敗
 */
export function checkResult(
  data: Record<string, unknown>,
  errorMsg?: string,
): boolean {
  const result = data.k1e ?? data.result
  if (!isOk(result)) {
    const rawMessage = data.k2e ?? data.message ?? data.error
    const failCode = data.failCode ?? data.failcode ?? data.errorCode ?? data.code
    const errorCode = data.error ?? failCode
    const rawText = typeof rawMessage === 'string' ? rawMessage.trim() : ''
    const msg = rawText && rawText !== 'エラー'
      ? rawText
      : failCode != null
        ? `${errorMsg ?? 'サーバーエラーが発生しました'} (${String(failCode)})`
        : errorMsg ?? 'サーバーエラーが発生しました'
    console.error('[MessageBox] checkResult failed', { result, failCode, message: rawMessage, payload: data })
    showError(msg)
      if (errorCode === 'AUTH_REQUIRED') useAuthStore.getState().requireLogin()
    return false
  }
  return true
}

// ─── コマンド別 failCode → メッセージ変換 ──────────────────────────────────

/**
 * mjkc20e (BuyMajItem) failCode 文字列 → 日本語メッセージ
 * レガシー ProcessBuyItemCommand → ErrorBuyItemCommand(message) 相当
 * サーバーは string failCode を返す (MajItemService 由来)
 */
export function buyMajItemErrorMessage(failCode: string): string {
  const map: Record<string, string> = {
    '0': '龍珠が足りません',
    '1': '麻雀コインが足りません',
    '2': 'DBエラー',
    '3': '未登録のSELLCODEです',
    '7': '内部エラー',
    '8': 'すでに持っています',
    '10': '必要な称号を持っていません',
    'SELL_CODE_NOT_FOUND':    '販売コードが見つかりません',
    'REQUIRED_TITLE_NOT_MET': '必要な称号を所持していません',
    'GEM_NOT_ENOUGH':         '龍珠が不足しています',
    'MONEY_NOT_ENOUGH':       '麻雀コインが不足しています',
    'UNKNOWN_CATEGORY':       'アイテムカテゴリが不明です',
  }
  if (failCode.startsWith('AVATAR_BUY_ERROR'))
    return `アバター購入エラーが発生しました (${failCode})`
  if (failCode.startsWith('BILLING_BUY_ERROR'))
    return `課金処理エラーが発生しました (${failCode})`
  return map[failCode] ?? `購入に失敗しました (${failCode})`
}

/**
 * mjkc21e (SelectMajItem) failCode 文字列 → 日本語メッセージ
 * レガシー ProcessCommand_SelectItem → ErrorSelectItemCommand(message) 相当
 */
export function selectMajItemErrorMessage(failCode: string): string {
  const map: Record<string, string> = {
    'ITEM_NOT_FOUND':         '指定のアイテムを所持していません',
    'ITEM_EXPIRED_OR_EMPTY':  'アイテムの期限が切れているか数量がありません',
  }
  return map[failCode] ?? `アイテム設定に失敗しました (${failCode})`
}

/**
 * mjkc28e/mjkc29e (TournamentJoin/Cancel) failCode 整数 → 日本語メッセージ
 */
export function tournamentErrorMessage(failCode: number, cmd: 'join' | 'cancel' | 'regist'): string {
  if (cmd === 'join') {
    const map: Record<number, string> = {
      2001: 'トーナメントが存在しません',
      2002: '参加受付時間外です',
      2003: '定員に達しています',
      2004: '既に他のトーナメントに参加中です',
      2005: '参加費が不足しています',
      2006: 'パスワードが違います',
      9999: 'データベースへの参加登録に失敗しました',
    }
    return map[failCode] ?? `参加に失敗しました (code: ${failCode})`
  }
  if (cmd === 'cancel') {
    const map: Record<number, string> = {
      3001: 'トーナメントが存在しません',
      3002: 'キャンセル期限が過ぎています',
      3003: '参加情報が見つかりません',
    }
    return map[failCode] ?? `キャンセルに失敗しました (code: ${failCode})`
  }
  const map: Record<number, string> = {
    1001: 'ゲームルールの設定が不正です',
    1002: '参加費は0～10,000コイン、各順位の賞金は0～100,000コインで設定してください',
    1003: '大会名はShift-JIS換算で8～30バイト（全角4～15文字）にしてください',
    1004: '開催日時は現在から1時間10分後～8日以内に設定してください',
    1005: 'パスワードは8文字以内にしてください',
    1006: 'この大会名は既に使われています',
    1007: '既に他の大会を主催中です',
    1008: 'ルーム数の上限に達しています',
    1009: 'メンテナンスなどの開催禁止時間帯と重なっています',
    1010: '大会賞金と開催手数料を支払うためのコインが不足しています',
    9999: 'データベースへの登録に失敗しました',
  }
  return map[failCode] ?? `登録に失敗しました (code: ${failCode})`
}

export function tournamentRegistErrorMessage(failCodes: unknown): string {
  const codes = String(failCodes ?? '')
    .split(/[\s,|]+/)
    .map(value => Number(value))
    .filter(value => Number.isInteger(value) && value > 0)

  if (codes.length === 0) return '大会登録に失敗しました。'
  return codes
    .map(code => `・${tournamentErrorMessage(code, 'regist')} (${code})`)
    .join('\n')
}
