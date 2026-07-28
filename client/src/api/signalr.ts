/**
 * AP-05 準拠: SignalR WebSocket 接続管理
 * エンドポイント: /hubs/majak
 *
 * 設計:
 *   - on(cmd, handler) でコマンドコードごとにハンドラーを登録
 *   - サーバーは SendAsync(commandCode, data) で送信するため、
 *     ハンドラーは connection.on(commandCode, ...) で直接登録する
 *   - invoke(method, ...args) でHubの直接メソッドを呼び出す
 *   - send(cmd, params) は SendCommand ディスパッチャー経由で送信する
 */
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { getGameAccessToken } from './authHeaders'

export type MessageHandler = (data: Record<string, unknown>) => void
export type ConnectionLostHandler = (error?: Error) => void
export type ReconnectedHandler = (connectionId?: string) => void
type StockedMessage = {
  cmd: string
  data: Record<string, unknown>
  sequence: number
}

let connection: HubConnection | null = null
let currentHubUrl: string | null = null
let connecting: Promise<void> | null = null
let intentionalStopDepth = 0
const MAX_CONSECUTIVE_HANDLER_FAILURES = 3
const STOCKED_COMMANDS = new Set(['playing', 'smmc4e', 'history', 'hc1e', 'mjkc24e'])
const PRECONNECTED_COMMANDS = new Set([...STOCKED_COMMANDS, 'c1e'])
const MAX_STOCKED_MESSAGES = 96

/** ユーザー登録ハンドラー (cmd → handlers[]) */
const handlers = new Map<string, MessageHandler[]>()
const DEBUG_SIGNALR = import.meta.env.VITE_DEBUG_SIGNALR === '1'
const stockedMessages: StockedMessage[] = []
let stockedMessageSequence = 0

/** connection.on に登録済みのディスパッチャー (cmd → dispatcher) */
const connHandlers = new Map<string, MessageHandler>()
const connectionLostHandlers = new Set<ConnectionLostHandler>()
const reconnectedHandlers = new Set<ReconnectedHandler>()
const handlerFailures = new WeakMap<MessageHandler, number>()
const disabledHandlers = new WeakSet<MessageHandler>()

async function stopConnectionSilently(conn: HubConnection): Promise<void> {
  intentionalStopDepth++
  try {
    await conn.stop()
  } finally {
    intentionalStopDepth--
  }
}

function isDebugCommand(cmd: string): boolean {
  return cmd === 'playing' || cmd === 'smmc4e' || cmd === 'smmc1e' || cmd === 'smmc2e' || cmd === 'mjkc4e' || cmd === 'history' || cmd === 'hc1e' || cmd === 'mjkc24e'
}

function stockMessage(cmd: string, data: Record<string, unknown>): void {
  if (!STOCKED_COMMANDS.has(cmd)) return
  stockedMessages.push({ cmd, data, sequence: ++stockedMessageSequence })
  if (stockedMessages.length > MAX_STOCKED_MESSAGES) stockedMessages.splice(0, stockedMessages.length - MAX_STOCKED_MESSAGES)
  if (DEBUG_SIGNALR) console.warn('[SignalR] stocked packet because no handler is ready', {
    cmd,
    stockedCount: stockedMessages.length,
    sequence: stockedMessageSequence,
    playType: data?.playType,
    openPos: data?.openPos,
    seatOrder: data?.seatOrder,
    actFlags: data?.actFlags,
    playerMode: data?.playerMode,
  })
}

function flushStockedMessages(): void {
  while (stockedMessages.length > 0) {
    const next = stockedMessages[0]
    const currentHandlers = [...(handlers.get(next.cmd) ?? [])]
    if (currentHandlers.length === 0) return

    stockedMessages.shift()
    if (DEBUG_SIGNALR) console.info('[SignalR] replay stocked packet', {
      cmd: next.cmd,
      remainingCount: stockedMessages.length,
      sequence: next.sequence,
      playType: next.data?.playType,
      openPos: next.data?.openPos,
      seatOrder: next.data?.seatOrder,
    })
    dispatchToHandlers(next.cmd, next.data, currentHandlers)
  }
}

function dispatchToHandlers(cmd: string, data: Record<string, unknown>, currentHandlers: MessageHandler[]): void {
  if (DEBUG_SIGNALR && isDebugCommand(cmd)) {
    console.info('[SignalR] dispatch', {
      cmd,
      handlerCount: currentHandlers.length,
      playType: data?.playType,
      openPos: data?.openPos,
      seatOrder: data?.seatOrder,
      action: data?.action,
      actFlags: data?.actFlags,
      playerMode: data?.playerMode,
      data,
    })
  }
  if (currentHandlers.length === 0) {
    stockMessage(cmd, data)
    return
  }
  currentHandlers.forEach(fn => {
    if (disabledHandlers.has(fn)) {
      if (DEBUG_SIGNALR) console.warn('[SignalR] handler skipped because disabled', { cmd, handler: fn.name })
      return
    }
    try {
      fn(data)
      handlerFailures.delete(fn)
    } catch (error) {
      const failures = (handlerFailures.get(fn) ?? 0) + 1
      handlerFailures.set(fn, failures)
      if (failures >= MAX_CONSECUTIVE_HANDLER_FAILURES) {
        disabledHandlers.add(fn)
        console.error(`SignalR handler for ${cmd} failed ${failures} times; disabling this handler until it is re-registered`, error)
      } else {
        console.error(`SignalR handler for ${cmd} failed (${failures}/${MAX_CONSECUTIVE_HANDLER_FAILURES})`, error)
      }
    }
  })
}

function ensureDispatcher(cmd: string): void {
  if (connHandlers.has(cmd)) return
  if (!handlers.has(cmd)) handlers.set(cmd, [])
  const dispatch: MessageHandler = (data) => {
    const currentHandlers = [...(handlers.get(cmd) ?? [])]
    dispatchToHandlers(cmd, data, currentHandlers)
  }
  connHandlers.set(cmd, dispatch)
  connection?.on(cmd, dispatch)
}

export function getConnection(): HubConnection | null {
  return connection
}

export async function connect(hubUrl = '/hubs/majak'): Promise<void> {
  // 同一 URL に既に接続済みならスキップ
  if (connection && connection.state === HubConnectionState.Connected
      && currentHubUrl === hubUrl) return

  if (connecting && currentHubUrl === hubUrl) {
    await connecting
    return
  }

  // 別 URL に接続している場合は一旦切断
  if (connection && connection.state !== HubConnectionState.Disconnected) {
    await stopConnectionSilently(connection)
    connection = null
    currentHubUrl = null
  }

  currentHubUrl = hubUrl
  const accessToken = getGameAccessToken()
  connection = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => getGameAccessToken() || accessToken,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  connection.onreconnecting(error => {
    for (const handler of connectionLostHandlers) {
      try {
        handler(error)
      } catch (handlerError) {
        console.error('SignalR reconnecting handler failed', handlerError)
      }
    }
  })

  connection.onreconnected(connectionId => {
    for (const handler of reconnectedHandlers) {
      try {
        handler(connectionId)
      } catch (handlerError) {
        console.error('SignalR reconnected handler failed', handlerError)
      }
    }
  })

  connection.onclose(error => {
    if (intentionalStopDepth > 0) return
    for (const handler of connectionLostHandlers) {
      try {
        handler(error)
      } catch (handlerError) {
        console.error('SignalR close handler failed', handlerError)
      }
    }
  })

  stockedMessages.length = 0
  const activeConnection = connection
  connection = null
  for (const cmd of PRECONNECTED_COMMANDS) ensureDispatcher(cmd)
  connection = activeConnection
  // connect() より前に on() が呼ばれた場合 (稀) の保険
  for (const [cmd, dispatch] of connHandlers) {
    connection.on(cmd, dispatch)
  }

  connecting = connection.start().finally(() => {
    connecting = null
  })
  await connecting
}

export async function disconnect(): Promise<void> {
  if (connecting) {
    await connecting.catch(() => {})
  }
  if (connection) {
    await stopConnectionSilently(connection)
    connection = null
    currentHubUrl = null
  }
  stockedMessages.length = 0
}

/**
 * コマンドハンドラーを登録
 * サーバーの SendAsync(cmd, data) に対応して connection.on(cmd, ...) で直接受信する
 */
export function on(cmd: string, handler: MessageHandler): void {
  ensureDispatcher(cmd)
  const list = handlers.get(cmd)!
  if (!list.includes(handler)) list.push(handler)
  flushStockedMessages()
}

/**
 * コマンドハンドラーを解除
 * ハンドラーが0になった場合は connection からも解除する
 */
export function off(cmd: string, handler: MessageHandler): void {
  handlerFailures.delete(handler)
  const list = handlers.get(cmd)
  if (!list) return
  const idx = list.indexOf(handler)
  if (idx !== -1) list.splice(idx, 1)
  if (list.length === 0) {
    if (PRECONNECTED_COMMANDS.has(cmd)) return
    handlers.delete(cmd)
    const dispatch = connHandlers.get(cmd)
    if (dispatch && connection) {
      connection.off(cmd, dispatch)
    }
    connHandlers.delete(cmd)
  }
}

export function onConnectionLost(handler: ConnectionLostHandler): void {
  connectionLostHandlers.add(handler)
}

export function offConnectionLost(handler: ConnectionLostHandler): void {
  connectionLostHandlers.delete(handler)
}

export function onReconnected(handler: ReconnectedHandler): void {
  reconnectedHandlers.add(handler)
}

export function offReconnected(handler: ReconnectedHandler): void {
  reconnectedHandlers.delete(handler)
}

/**
 * SendCommand ディスパッチャー経由でコマンドを送信
 * Hub.SendCommand(code, payload) に対応
 */
export async function send(cmd: string, params: Record<string, unknown> = {}): Promise<void> {
  if (!connection || connection.state !== HubConnectionState.Connected) {
    throw new Error('SignalR not connected')
  }
  await connection.invoke('SendCommand', cmd, params)
}

export function isConnected(): boolean {
  return connection?.state === HubConnectionState.Connected
}

/**
 * Hub の直接メソッドを呼び出す
 */
export async function invoke<T = void>(method: string, ...args: unknown[]): Promise<T> {
  if (!connection || connection.state !== HubConnectionState.Connected) {
    throw new Error('SignalR not connected')
  }
  return await connection.invoke<T>(method, ...args)
}
