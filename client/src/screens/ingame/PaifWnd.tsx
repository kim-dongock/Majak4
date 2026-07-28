/**
 * CMJPaifWnd 相当 — 牌譜 (リプレイ) 再生画面 (AP-09 §2-11)
 * レガシー: legacy/client/HgMajak2/MJPaifWnd.h/cpp
 *
 * レガシーでは CMJGameWnd (Phaser に相当) が PANELMODE_PAIF モードで動作する。
 * Web 版では GameInstance を replay モードで起動し、PANELMODE_PAIF の
 * スプライトボタンを React オーバーレイとして配置する。
 *
 * ── CMJPaifWnd の実装 (レガシー) ────────────────────────────────────
 *   - OnPaint(): m_Screen.Draw(&dc) — Phaser 描画に相当
 *   - OnClose(): ShowWindow(SW_HIDE) — 閉じる
 *   実質 Phaser GameScene の薄いラッパー
 *
 * ── PANELMODE_PAIF 操作 UI (レガシー CMJGameWnd) ───────────────────
 *   MJWindow1.cpp: m_btnPaifu*.Create("mj_btPaifu*", X_REP*, Y_REP*)
 *   MJWindow2.cpp: PANELMODE_PAIF で各ボタン ShowWindow(SW_SHOW)
 * ─────────────────────────────────────────────────────────────────────
 */
import { useEffect, useRef, useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { createGame, destroyGame } from '../../game/GameInstance'
import { useDesktopScreenScale } from '../../hooks/useDesktopScreenScale'
import * as SignalR from '../../api/signalr'
import SelPaifuDlg, { type PaifuEntry } from '../outgame/dialogs/SelPaifuDlg'
import PaifuSaveDlg from '../outgame/dialogs/PaifuSaveDlg'

const IMG = '/assets/images/game'
const CMD_REPLAY_NAVI = 'repnavi'
const PAIFU_KEND = 999
const PAIFU_PANEL = { x: 102, y: 644, w: 580, h: 60 }
const PAIFU_ROTATE_EVENT = 'majak:paifu-rotate'
const PAIFU_HAND_OPEN_EVENT = 'majak:paifu-hand-open'
const PAIFU_GRAPH_EVENT = 'majak:paifu-graph'

/** CMJBmpBtnEx 相当 — AP-06 §2 4フレームスプライトボタン */
function PaifuSpriteButton({
  src,
  frameW,
  frameH,
  x,
  y,
  onClick,
  title,
  disabled,
  active,
}: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  title?: string
  disabled?: boolean
  active?: boolean
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  const displayFrame = disabled ? 1 : active ? 3 : frameIdx

  return (
    <button
      title={title}
      disabled={disabled}
      onClick={onClick}
      onMouseEnter={() => !disabled && setFrameIdx(2)}
      onMouseLeave={() => !disabled && setFrameIdx(0)}
      onMouseDown={() => !disabled && setFrameIdx(3)}
      onMouseUp={() => !disabled && setFrameIdx(2)}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: frameW,
        height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-displayFrame * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none',
        padding: 0,
        cursor: disabled ? 'default' : 'pointer',
        outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

/** 牌譜ソース */
export interface PaifuSource {
  /** ローカルファイル or サーバーから取得した牌譜 JSON */
  data: unknown
  /** 牌譜タイトル */
  title?: string
  /** CMJPaifu::GetComment 相当 */
  comment?: string
}

interface ReplayNaviPayload {
  /** MakePaifData()[1] = bJoin */
  join: boolean
  /** MakePaifData()[2] = bPaif */
  paif: boolean
  /** MakePaifData()[3] = bSkip */
  skip: boolean
  /** MakePaifData()[4] = nSkip */
  nSkip: number
  /** MakePaifData() の nav+5 以降。Web では JSON 化済み牌譜をそのまま中継する。 */
  data?: unknown
}

export default function PaifWnd() {
  const desktopScale = useDesktopScreenScale()
  const containerRef = useRef<HTMLDivElement>(null)
  const lastSentReplayNaviRef = useRef<ReplayNaviPayload | null>(null)
  const navigate     = useNavigate()
  const location     = useLocation()
  const navState = location.state as { paifu?: PaifuSource; paifuEntries?: PaifuEntry[] } | null
  const initialSource = navState?.paifu
  const paifuEntries = navState?.paifuEntries ?? []

  /** m_btnPaifuPlay / m_btnPaifuHide のチェック状態 */
  const [isPlaying, setIsPlaying] = useState(false)
  const [handHidden, setHandHidden] = useState(false)
  const [showLoadDlg, setShowLoadDlg] = useState(false)
  const [showSaveDlg, setShowSaveDlg] = useState(false)
  const [naviEnabled, setNaviEnabled] = useState(true)
  const [fileActionsEnabled, setFileActionsEnabled] = useState(Boolean(initialSource?.data))
  const [paifuStep, setPaifuStep] = useState(0)
  const [paifuKPos, setPaifuKPos] = useState(0)
  const [paifuKEnd, setPaifuKEnd] = useState(false)
  const [paifuKCount, setPaifuKCount] = useState<number | null>(null)

  const [source, setSource] = useState<PaifuSource | undefined>(initialSource)
  const hasPaifu = Boolean(source?.data)

  useEffect(() => {
    if (!containerRef.current) return
    // PANELMODE_PAIF 相当: リプレイモードで Phaser を起動
    createGame(containerRef.current, { mode: 'replay', paifu: source?.data })
    return () => destroyGame()
  }, [source?.data])

  useEffect(() => {
    const onReplayNavi = (data: Record<string, unknown>) => {
      const lastSent = lastSentReplayNaviRef.current
      if (lastSent &&
          data.join === lastSent.join &&
          data.paif === lastSent.paif &&
          data.skip === lastSent.skip &&
          data.nSkip === lastSent.nSkip) {
        lastSentReplayNaviRef.current = null
        setNaviEnabled(true)
        setFileActionsEnabled(Boolean(source?.data))
        return
      }

      const nSkip = Number(data.nSkip ?? 0)
      const nKPos = Number(data.kPos ?? data.paifuKPos)
      const nKCount = Number(data.kCount ?? data.paifuKCount)
      const step = Boolean(data.step ?? true)
      setPaifuStep(Number.isFinite(nSkip) ? nSkip : 0)
      if (Number.isFinite(nKPos)) setPaifuKPos(Math.max(0, nKPos))
      if (Number.isFinite(nKCount) && nKCount > 0) setPaifuKCount(nKCount)
      setIsPlaying(!step)
      setPaifuKEnd(typeof data.kEnd === 'boolean' ? data.kEnd : nSkip >= PAIFU_KEND)
      setNaviEnabled(true)
      setFileActionsEnabled(Boolean(source?.data))
    }
    SignalR.on(CMD_REPLAY_NAVI, onReplayNavi)
    return () => SignalR.off(CMD_REPLAY_NAVI, onReplayNavi)
  }, [source?.data])

  const sendReplayNavi = (payload: ReplayNaviPayload) => {
    lastSentReplayNaviRef.current = payload
    SignalR.send(CMD_REPLAY_NAVI, payload as unknown as Record<string, unknown>).catch(() => {
      lastSentReplayNaviRef.current = null
      setNaviEnabled(true)
      setFileActionsEnabled(Boolean(source?.data))
    })
  }

  /** CMJGameWnd::PaifuJump → CMJTblPaif::PaifuReplay → SendNavi */
  const paifuJump = (nSkipParam: number, step: boolean, skip: boolean) => {
    setNaviEnabled(false)
    setFileActionsEnabled(false)

    let nextKPos = paifuKPos
    let nSkip = nSkipParam

    if (nSkip < 0) {
      nextKPos -= 1
      nSkip = PAIFU_KEND
    } else if (nSkip > paifuStep && paifuKEnd) {
      nextKPos += 1
      nSkip = step && nSkip === PAIFU_KEND ? 1 : 0
    }
    const finalKPos = Math.max(0, nextKPos)

    setPaifuKPos(finalKPos)
    setPaifuStep(nSkip)
    setPaifuKEnd(nSkip >= PAIFU_KEND)

    sendReplayNavi({
      join: finalKPos !== paifuKPos,
      paif: finalKPos !== paifuKPos,
      skip,
      nSkip,
      data: finalKPos !== paifuKPos ? source?.data : undefined,
    })
  }

  /** OnPaifuLoad — CMJSelPaifuDlg を開いて選択牌譜をロード */
  const loadPaifu = (nextSource: PaifuSource) => {
    setSource(nextSource)
    setIsPlaying(false)
    setPaifuKPos(0)
    setPaifuStep(0)
    setPaifuKEnd(false)
    setPaifuKCount(null)
    setNaviEnabled(false)
    setFileActionsEnabled(true)
    sendReplayNavi({ join: true, paif: true, skip: true, nSkip: 0, data: nextSource.data })
  }

  const handleLoad = () => {
    setShowLoadDlg(true)
  }

  const handleSelectPaifu = (entry: PaifuEntry) => {
    setShowLoadDlg(false)
    loadPaifu({
      data: entry.data ?? entry,
      title: entry.roomName || entry.fieldName || String(entry.id),
      comment: 'comment' in entry ? String((entry as PaifuEntry & { comment?: unknown }).comment ?? '') : '',
    })
  }

  /** OnPaifuSave — CPaifuSaveDlg を開いてブラウザダウンロード */
  const handleSave = () => {
    if (!hasPaifu) return
    setShowSaveDlg(true)
  }

  const savePaifu = (fileName: string, bKyoku: boolean, comment: string) => {
    const paifuBody = typeof source?.data === 'string'
      ? source.data
      : JSON.stringify({ bKyoku, paifu: source?.data }, null, 2)
    const body = `<${comment}\r\n${paifuBody}`
    const blob = new Blob([body], { type: 'text/plain' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName
    anchor.click()
    URL.revokeObjectURL(url)
    setShowSaveDlg(false)
  }

  /** OnPaifuGrph — ShowPaifuWnd(true) 相当 */
  const handleGraph = () => {
    if (!hasPaifu) return
    window.dispatchEvent(new CustomEvent(PAIFU_GRAPH_EVENT, { detail: { visible: true } }))
    sendReplayNavi({ join: false, paif: false, skip: true, nSkip: PAIFU_KEND })
    sendReplayNavi({ join: false, paif: false, skip: true, nSkip: paifuStep })
  }

  /** OnPaifuHide — 手牌表示 OPEN/HAND 切替 */
  const handleHide = () => setHandHidden(v => {
    const nextHidden = !v
    window.dispatchEvent(new CustomEvent(PAIFU_HAND_OPEN_EVENT, { detail: { open: !nextHidden } }))
    return nextHidden
  })

  /** OnRotate1 / OnRotate3 */
  const handleRotate = (delta: 1 | 3) => {
    window.dispatchEvent(new CustomEvent(PAIFU_ROTATE_EVENT, { detail: { delta } }))
  }

  const handlePrev = () => paifuJump(paifuStep > 1 ? 1 : -1, true, true)

  const handleBack = () => paifuJump(paifuStep - 1, true, true)

  const handleStep = () => paifuJump(paifuStep + 1, true, paifuStep === 0)

  const handleNext = () => paifuJump(paifuStep === 0 ? 1 : PAIFU_KEND, true, true)

  /** CMJGameWnd::OnMouseWheel — wheel navigates replay, Shift jumps by kyoku. */
  const handleWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    if (!hasPaifu) return
    if (event.deltaY > 0) {
      if (event.shiftKey) {
        if (canNext) handleNext()
      } else if (canNext) {
        handleStep()
      }
    } else if (event.deltaY < 0) {
      if (event.shiftKey) {
        if (canPrev) handlePrev()
      } else if (canBack) {
        handleBack()
      }
    }
  }

  /** OnPaifuPlay */
  const handlePlay = () => {
    if (isPlaying) {
      setIsPlaying(false)
      setNaviEnabled(false)
      setFileActionsEnabled(false)
      sendReplayNavi({ join: false, paif: false, skip: true, nSkip: paifuStep })
      return
    }
    setIsPlaying(true)
    paifuJump(PAIFU_KEND, false, false)
  }

  /** 閉じる — OnClose() ShowWindow(SW_HIDE) 相当 */
  const handleClose = () => navigate(-1)

  const canUseNavi = hasPaifu && naviEnabled
  const canPrev = canUseNavi && (paifuStep > 1 || paifuKPos > 0)
  const canBack = canUseNavi && (paifuStep > 0 || paifuKPos > 0)
  const hasKnownLastKyo = paifuKCount !== null && paifuKPos >= paifuKCount - 1
  const canNext = canUseNavi && (!paifuKEnd || !hasKnownLastKyo)
  const playDisabled = !hasPaifu || (isPlaying ? !naviEnabled : !canNext)

  return (
    <div style={{
      width: '100vw', height: '100vh',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: '#000', overflow: 'hidden',
    }}>

      {/* CMJGameWnd / AP-09 §5 インゲーム解像度: 1019×735 */}
      <div style={{ position: 'relative', width: 1019, height: 735, flex: '0 0 auto', transform: desktopScale === 1 ? undefined : `scale(${desktopScale})`, transformOrigin: 'center center' }} onWheel={handleWheel}>
        {/* ── Phaser コンテナ: CMJPaifWnd::OnPaint() m_Screen.Draw(&dc) 相当 ── */}
        <div
          ref={containerRef}
          style={{ position: 'absolute', inset: 0, width: 1019, height: 735 }}
        />

        {/* PANELMODE_PAIF: MJWindow1.cpp Create / MajakDef.h X_REP*, Y_REP* */}
        <img
          src={`${IMG}/mj_PaifuBoard.png`}
          alt=""
          draggable={false}
          style={{
            position: 'absolute',
            left: PAIFU_PANEL.x,
            top: PAIFU_PANEL.y,
            width: PAIFU_PANEL.w,
            height: PAIFU_PANEL.h,
            imageRendering: 'pixelated',
            pointerEvents: 'none',
          }}
        />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuGrph.png`} frameW={111} frameH={40} x={118} y={647} onClick={handleGraph} title="牌譜ウィンドウ" disabled={!fileActionsEnabled} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuRot3.png`} frameW={46} frameH={31} x={202} y={647} onClick={() => handleRotate(3)} title="回転3" />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuRot1.png`} frameW={46} frameH={31} x={248} y={647} onClick={() => handleRotate(1)} title="回転1" />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuPrev.png`} frameW={54} frameH={40} x={305} y={649} onClick={handlePrev} title="局先頭/前局" disabled={!canPrev} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuBack.png`} frameW={54} frameH={40} x={341} y={649} onClick={handleBack} title="一手戻る" disabled={!canBack} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuPlay.png`} frameW={54} frameH={40} x={377} y={649} onClick={handlePlay} title="再生" disabled={playDisabled} active={isPlaying} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuStep.png`} frameW={54} frameH={40} x={413} y={649} onClick={handleStep} title="一手進む" disabled={!canNext} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuNext.png`} frameW={54} frameH={40} x={449} y={649} onClick={handleNext} title="局末尾/次局" disabled={!canNext} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuLoad.png`} frameW={138} frameH={28} x={496} y={647} onClick={handleLoad} title="牌譜ロード" disabled={!naviEnabled} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuSave.png`} frameW={138} frameH={28} x={496} y={673} onClick={handleSave} title="牌譜保存" disabled={!fileActionsEnabled} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuHide.png`} frameW={92} frameH={25} x={202} y={676} onClick={handleHide} title="手牌表示切替" active={handHidden} />
        <PaifuSpriteButton src={`${IMG}/mj_btPaifuExit.png`} frameW={114} frameH={40} x={598} y={647} onClick={handleClose} title="閉じる" />
      </div>

      {showLoadDlg && (
        <SelPaifuDlg
          entries={paifuEntries}
          onSelect={handleSelectPaifu}
          onCancel={() => setShowLoadDlg(false)}
        />
      )}

      {showSaveDlg && (
        <PaifuSaveDlg
          defaultFileName={source?.title ? `${source.title}.txt` : 'Majak2Paifu.txt'}
          initialComment={source?.comment ?? ''}
          onSave={savePaifu}
          onCancel={() => setShowSaveDlg(false)}
        />
      )}
    </div>
  )
}
