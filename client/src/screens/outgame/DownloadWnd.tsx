/**
 * CDownloadWnd 相当 — ゲームリソースダウンロード進捗表示 (AP-09 §1-1)
 * レガシー: legacy/client/HgMajak2/MJDownload.h/cpp
 *
 * レガシーでは CMajakChannelWnd の子ウィンドウとして (15, 485) に配置 (m_DownloadWnd.Create(...))
 * Web 版でもレガシーと同じ 1024×768 座標系の (15, 485) に配置する。
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * ウィンドウサイズ: 背景画像のサイズに準拠 (int w=m_WndImg.GetWidth())
 *   mj_rm_ui_download_Bg.png: 654×79 → ウィンドウ 654×79px
 *
 * 背景 (1フレーム 654×79):
 *   mj_rm_ui_download_Bg.png  at (0, 0)
 *
 * プログレスゲージ (単一画像 200×46, 進捗に応じてクリップ):
 *   mj_rm_ui_download_gauge.png  at (PROGRESS_LEFT=23, PROGRESS_TOP=20)
 *   幅: progress% × PROGRESS_WIDTH(200)px でクリップ
 *
 * キャンセルボタン (4フレーム 94×26):
 *   mj_btn_DLcancel.png  at (CANCEL_LEFT=500, CANCEL_TOP=43)
 *   IDC_PCK_BTN_CANCEL_DOWNLOAD → OnBtnCancel
 *
 * ── 定数 (レガシーより) ──────────────────────────────────────────────────
 *   PROGRESS_LEFT   = 38-15 = 23
 *   PROGRESS_TOP    = 505-485 = 20
 *   PROGRESS_WIDTH  = 200
 *   PROGRESS_HEIGHT = 46
 *   CANCEL_LEFT     = 515-15 = 500
 *   CANCEL_TOP      = 528-485 = 43
 * ────────────────────────────────────────────────────────────────────────
 */
import { useState } from 'react'

const IMG = '/assets/images/game'

/** 定数 (レガシーより) */
const PROGRESS_LEFT   = 23
const PROGRESS_TOP    = 20
const PROGRESS_WIDTH  = 200
const PROGRESS_HEIGHT = 46
const CANCEL_LEFT     = 500
const CANCEL_TOP      = 43

interface Props {
  /** 進捗 0〜100 */
  progress: number
  /** レガシー OnPaint では未描画。互換用に受け取るだけ。 */
  message?: string
  /** OnBtnCancel 相当 */
  onCancel?: () => void
}

export default function DownloadWnd({ progress, onCancel }: Props) {
  const [fi, setFi] = useState(0)

  /** ゲージ幅: progress% × PROGRESS_WIDTH */
  const gaugeW = Math.round(Math.max(0, Math.min(100, progress)) / 100 * PROGRESS_WIDTH)

  return (
    /* レガシー: CMajakChannelWnd 内 位置 (15, 485) の子ウィンドウ
       Create(15, 485, this, PCKMSGID_DOWNLOADRESULT) */
    <div style={{
      position: 'absolute',
      left: 15, top: 485,
      width: 654, height: 79,
      zIndex: 50,
    }}>

      {/* ================================================================
          背景: mj_rm_ui_download_Bg.png (654×79) at (0,0)
          m_WndImg.Create(IDS_FILE_UPD_BG) → ウィンドウサイズ = 背景サイズ
          ================================================================ */}
      <img
        src={`${IMG}/mj_rm_ui_download_Bg.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 654, height: 79 }}
      />

      {/* ================================================================
          プログレスゲージ: mj_rm_ui_download_gauge.png (200×46) 単一画像
          at (PROGRESS_LEFT=23, PROGRESS_TOP=20)
          クリップ: width = progress% × 200px
          ================================================================ */}
      <div style={{
        position: 'absolute',
        left: PROGRESS_LEFT,
        top: PROGRESS_TOP,
        width: gaugeW,
        height: PROGRESS_HEIGHT,
        overflow: 'hidden',
      }}>
        <img
          src={`${IMG}/mj_rm_ui_download_gauge.png`}
          alt=""
          draggable={false}
          style={{
            width: PROGRESS_WIDTH,
            height: PROGRESS_HEIGHT,
            imageRendering: 'pixelated',
          }}
        />
      </div>

      {/* ================================================================
          キャンセルボタン: mj_btn_DLcancel.png (376×26, 4フレーム 94×26)
          at (CANCEL_LEFT=500, CANCEL_TOP=43)
          IDC_PCK_BTN_CANCEL_DOWNLOAD → OnBtnCancel
          ================================================================ */}
      <button
        type="button"
        onClick={() => onCancel?.()}
        onMouseEnter={() => setFi(2)}
        onMouseLeave={() => setFi(0)}
        onMouseDown={() => setFi(3)}
        onMouseUp={() => setFi(2)}
        style={{
          position: 'absolute',
          left: CANCEL_LEFT, top: CANCEL_TOP,
          width: 94, height: 26,
          backgroundImage: `url(${IMG}/mj_btn_DLcancel.png)`,
          backgroundPosition: `${-fi * 94}px 0`,
          backgroundRepeat: 'no-repeat',
          border: 'none', padding: 0,
          cursor: 'pointer', outline: 'none',
          imageRendering: 'pixelated',
        }}
      />
    </div>
  )
}
