/**
 * CMJSelLobbyWnd 相当 — ロビー選択画面 (AP-09 §1-4)
 * レガシー: legacy/client/HgMajak2/MJSelLobbyWnd.h/cpp
 *
 * ウィンドウサイズ: MoveWindow(5,31,1014,704) → 1014×704px
 * 背景:
 *   交流戦グループ: mj_ch_bk.png (1014×704)
 *   段位戦グループ: mj_ch_bk_01.png (1014×704)
 *
 * ボタン配置は MJSelLobbyWnd::OnCreate() の Create() 呼び出しに準拠
 * テキスト配置は MJSelLobbyWnd::OnPaint() の TextOut() 呼び出しに準拠
 *
 * 交流戦 フィールドID (lpcstrKouryuFieldID):
 *   0=0082B, 1=0086B, 2=0085F, 3=0075B, 4=00T5A, 5=00000(非表示)
 * 段位戦 フィールドID (lpcstrDaniFieldID):
 *   0=0ZG6A, 1=0ZG6B, 2=0ZG6C, 3=0ZG6D, 4=0ZG7A, 5=0ZG7B, 6=0ZG7C, 7=0ZG7D
 */
import { useNavigate, useParams } from 'react-router-dom'
import { useState, useEffect } from 'react'
import { getChannels } from '../../api/channel'
import DrawMemberInfo from '../../components/DrawMemberInfo'
import { MAJAK_EXIT_REQUEST_EVENT } from '../../components/MajakFrame'
import type { MJOption } from './dialogs/OptDlg'
import { useOutgameLayoutMode } from '../../hooks/useOutgameLayoutMode'
import { showConfirm } from '../../utils/msgbox'

const IMG = '/assets/images/game'
const TRAINING_GUIDE = '４人に足りない分はＡＩが参加するので１人でも遊べます。\nGPが増減せず戦績も残りませんので練習にどうぞ。'
const TRAINING_ENTRY_TITLE = '練習広場入場'
const TRAINING_ENTRY_MESSAGE = 'ここは練習広場です。\n4人がそろわなくても対戦ができますが\n対戦した後の戦績やGPはゲーム終了時に元に戻ります。\n練習広場に入りますか？'

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src,
  frameW,
  frameH,
  x,
  y,
  onClick,
  title,
  hidden,
}: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  title?: string
  hidden?: boolean
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  if (hidden) return null
  return (
    <button
      title={title}
      onClick={onClick}
      onMouseEnter={() => setFrameIdx(2)}
      onMouseLeave={() => setFrameIdx(0)}
      onMouseDown={() => setFrameIdx(3)}
      onMouseUp={() => setFrameIdx(2)}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: frameW,
        height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-frameIdx * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        backgroundColor: 'transparent',
        border: 'none',
        padding: 0,
        cursor: 'pointer',
        outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

function MobileLobbySpriteButton({
  src,
  label,
  count,
  onClick,
  title,
}: {
  src: string
  label: string
  count: string
  onClick: () => void
  title?: string
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  const backgroundX = frameIdx === 0 ? '0%' : `${(frameIdx / 3) * 100}%`
  return (
    <div className="majak-mobile-lobby-sprite-entry">
      <button
        type="button"
        className="majak-mobile-lobby-sprite-button"
        onClick={onClick}
        onMouseEnter={() => setFrameIdx(2)}
        onMouseLeave={() => setFrameIdx(0)}
        onMouseDown={() => setFrameIdx(3)}
        onMouseUp={() => setFrameIdx(2)}
        aria-label={label}
        title={title}
        style={{
          backgroundImage: `url(${src})`,
          backgroundPosition: `${backgroundX} 0`,
        }}
      />
      <span className="majak-mobile-lobby-sprite-count">{count}</span>
    </div>
  )
}

/** ====================================================================
 * strInformation_Kouryu / strInformation_Dani (OnPaint TextOut, y=530〜635)
 * 14px Bold MS Gothic 白, 等幅
 * ==================================================================== */
const INFO_KOURYU = [
  '基本卓（場代   500 GP、レート  20、龍珠 1位:0 2位:0）',
  'ハイ卓（場代 3,000 GP、レート 100、龍珠 1位:5 2位:2）',
  '練習卓（戦績が記録されない卓、レート・場代など全て0）',
]

const INFO_DANI = [
  '通常卓　（場代 500 GP、レート 20、龍珠 1位:1 2位:0、10級～三段）',
  '段位卓　（場代 500 GP、レート 20、龍珠 1位:2 2位:0、初段～九段、所持5,000円）',
  '高段位卓（場代 500 GP、レート 20、龍珠 1位:3 2位:1、四段～九段、所持10,000円）',
  '十段位卓（場代 500 GP、レート 20、龍珠 1位:4 2位:2、七段～九段、所持30,000円）',
]

/** ====================================================================
 * デフォルト人数表示文字列 (strNumOfPeople_Default = "-----人")
 * ==================================================================== */
const DEFAULT_COUNT = '-----人'

const KOURYU_OPTION_PRESETS: Partial<MJOption>[] = [
  { nUma: 0, nSet: 1, bWar: false, bKui: false },
  { nUma: 0, nSet: 0, bWar: false },
  { nUma: 2, bWar: false },
  { nUma: 0, bWar: true },
  { nUma: 0, bWar: false },
  { nUma: 0, bWar: false },
]

const DANI_OPTION_PRESETS: Partial<MJOption>[] = [
  { nUma: 0, nSet: 0, bWar: false, bKui: false },
  { nUma: 1, nSet: 0, bWar: false, bKui: false },
  { nUma: 2, nSet: 0, bWar: false, bKui: false },
  { nUma: 3, nSet: 0, bWar: false, bKui: false },
  { nUma: 0, nSet: 1, bWar: false, bKui: false },
  { nUma: 1, nSet: 1, bWar: false, bKui: false },
  { nUma: 2, nSet: 1, bWar: false, bKui: false },
  { nUma: 3, nSet: 1, bWar: false, bKui: false },
]

const KOURYU_FIELD_IDS = ['0082B', '0086B', '0085F', '0075B', '00T5A', '00000']
const DANI_FIELD_IDS   = ['0ZG6A', '0ZG6B', '0ZG6C', '0ZG6D', '0ZG7A', '0ZG7B', '0ZG7C', '0ZG7D']

/** ====================================================================
 * CMJSelLobbyWnd 本体
 * ==================================================================== */
export default function LobbySelectScreen() {
  const { group, channelId } = useParams<{ group?: string; channelId?: string }>()
  const navigate = useNavigate()
  const layoutMode = useOutgameLayoutMode()

  const requestedGroup = group ?? getGroupFromChannelId(channelId)
  const isDani = requestedGroup === 'dani'
  const isKouryu = requestedGroup === 'kouryu'

  useEffect(() => {
    if (!isKouryu && !isDani) navigate('/channel', { replace: true })
  }, [isKouryu, isDani, navigate])

  /** ユーザー人数 (サーバーから取得するまで "-----人" で表示)
   * レガシー: MSGID_GET_NEXT_USER_CNT 相当 — /api/channels から memberCnt を取得
   */
  const [countKouryu, setCountKouryu] = useState<string[]>(Array(6).fill(DEFAULT_COUNT))
  const [countDani,   setCountDani]   = useState<string[]>(Array(8).fill(DEFAULT_COUNT))

  /** チャンネル比較から人数を読み込む */
  useEffect(() => {
    getChannels().then(channels => {
      const fmt = (n: number) => `${String(n).padStart(5, ' ')}人`
      const find = (subId: string) => {
        const ch = channels.find(c => c.subId === subId)
        return ch ? fmt(ch.memberCnt) : DEFAULT_COUNT
      }
      setCountKouryu(KOURYU_FIELD_IDS.map(id => find(id)))
      setCountDani(DANI_FIELD_IDS.map(id => find(id)))
    }).catch(() => {})
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /** OnPageBack() — 前のページ (チャンネルグループ選択) へ戻る */
  const onBack = () => navigate('/channel')

  /** OnBtnExit() — アプリ終了 */
  const onExit = () => window.dispatchEvent(new Event(MAJAK_EXIT_REQUEST_EVENT))

  /** OnTakuTypeKouryu/Dani — ToLobby(idx) 相当 → ロビー画面へ遷移
   *  fieldId はサーバーへの参加リクエストに使用する subId
   */
  const toKouryuLobby = async (idx: number) => {
    if (idx === 4) {
      const accepted = await showConfirm(
        TRAINING_ENTRY_MESSAGE,
        TRAINING_ENTRY_TITLE,
        'はい(Y)',
        'いいえ(N)',
      )
      if (!accepted) return
    }
    const fieldId = KOURYU_FIELD_IDS[idx]
    navigate(`/channel/${fieldId}/lobby`, { state: { lobbyOption: KOURYU_OPTION_PRESETS[idx] } })
  }
  const toDaniLobby = (idx: number) => {
    const fieldId = DANI_FIELD_IDS[idx]
    navigate(`/channel/${fieldId}/lobby`, { state: { lobbyOption: DANI_OPTION_PRESETS[idx] } })
  }

  if (!isKouryu && !isDani) return null

  if (layoutMode === 'mobileLandscape') {
    const mobileItems = isDani
      ? [
          { title: '通常卓', src: `${IMG}/mj_ch_bk_01_btn_l_01.png`, count: countDani[0], onClick: () => toDaniLobby(0) },
          { title: '段位卓', src: `${IMG}/mj_ch_bk_01_btn_l_02.png`, count: countDani[1], onClick: () => toDaniLobby(1) },
          { title: '高段位卓', src: `${IMG}/mj_ch_bk_01_btn_l_03.png`, count: countDani[2], onClick: () => toDaniLobby(2) },
          { title: '十段位卓', src: `${IMG}/mj_ch_bk_01_btn_l_04.png`, count: countDani[3], onClick: () => toDaniLobby(3) },
          { title: '通常卓 東南', src: `${IMG}/mj_ch_bk_01_btn_r_01.png`, count: countDani[4], onClick: () => toDaniLobby(4) },
          { title: '段位卓 東南', src: `${IMG}/mj_ch_bk_01_btn_r_02.png`, count: countDani[5], onClick: () => toDaniLobby(5) },
          { title: '高段位卓 東南', src: `${IMG}/mj_ch_bk_01_btn_r_03.png`, count: countDani[6], onClick: () => toDaniLobby(6) },
          { title: '十段位卓 東南', src: `${IMG}/mj_ch_bk_01_btn_r_04.png`, count: countDani[7], onClick: () => toDaniLobby(7) },
        ]
      : [
          { title: '基本卓（安い部屋）', src: `${IMG}/mj_ch_btn_01.png`, count: countKouryu[0], onClick: () => toKouryuLobby(0) },
          { title: '基本卓', src: `${IMG}/mj_ch_btn_03.png`, count: countKouryu[1], onClick: () => toKouryuLobby(1) },
          { title: 'ハイ卓', src: `${IMG}/mj_ch_btn_05.png`, count: countKouryu[2], onClick: () => toKouryuLobby(2) },
          { title: '基本卓（掛けあり）', src: `${IMG}/mj_ch_btn_06.png`, count: countKouryu[3], onClick: () => toKouryuLobby(3) },
          { title: '練習卓', description: TRAINING_GUIDE, src: `${IMG}/mj_ch_btn_08.png`, count: countKouryu[4], onClick: () => toKouryuLobby(4) },
        ]

    return (
      <div className="majak-mobile-screen majak-mobile-lobby-select">
        <section className="majak-mobile-hero">
          <div>
            <div className="majak-mobile-eyebrow">{isDani ? 'DAN-I' : 'KOURYU'}</div>
            <h1>{isDani ? '段位戦' : '交流戦'}</h1>
          </div>
          <button type="button" className="majak-mobile-secondary" onClick={onBack}>戻る</button>
        </section>
        <div className="majak-mobile-card-list majak-mobile-lobby-sprite-list">
          {mobileItems.map(item => (
            <MobileLobbySpriteButton key={item.title} src={item.src} label={item.title} count={item.count} onClick={item.onClick} title={'description' in item ? item.description : undefined} />
          ))}
        </div>
      </div>
    )
  }

  return (
    /* CMJSelLobbyWnd クライアント領域: 1014×704px */
    <div style={{ position: 'relative', width: 1014, height: 704, overflow: 'hidden' }}>

      {/* ── 背景 BitBlt(0,0, isDani ? m_dibBackDani : m_dibBackKouryu) ── */}
      <img
        src={isDani ? `${IMG}/mj_ch_bk_01.png` : `${IMG}/mj_ch_bk.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 1014, height: 704 }}
      />

      {/* ───────────────────────────────────────────────────────────── */}
      {/* 交流戦グループボタン (m_nGroup == IDC_CHK_STAND)              */}
      {/* ───────────────────────────────────────────────────────────── */}
      {!isDani && (
        <>
          {/* IDC_BTN_KOURYU_TAKU_1 (57,201) mj_ch_btn_01.png 306×51 — 基本卓（安い部屋） */}
          <SpriteButton src={`${IMG}/mj_ch_btn_01.png`} frameW={306} frameH={51} x={57}  y={201} onClick={() => toKouryuLobby(0)} title="基本卓（安い部屋）" />
          {/* IDC_BTN_KOURYU_TAKU_2 (57,261) mj_ch_btn_03.png 306×51 — 基本卓 */}
          <SpriteButton src={`${IMG}/mj_ch_btn_03.png`} frameW={306} frameH={51} x={57}  y={261} onClick={() => toKouryuLobby(1)} title="基本卓" />
          {/* IDC_BTN_KOURYU_TAKU_3 (57,321) mj_ch_btn_05.png 306×51 — ハイ卓 */}
          <SpriteButton src={`${IMG}/mj_ch_btn_05.png`} frameW={306} frameH={51} x={57}  y={321} onClick={() => toKouryuLobby(2)} title="ハイ卓" />
          {/* IDC_BTN_KOURYU_TAKU_4 (495,201) mj_ch_btn_06.png 306×51 — 基本卓（掛けあり） */}
          <SpriteButton src={`${IMG}/mj_ch_btn_06.png`} frameW={306} frameH={51} x={495} y={201} onClick={() => toKouryuLobby(3)} title="基本卓（掛けあり）" />
          {/* IDC_BTN_KOURYU_TAKU_5 (495,261) mj_ch_btn_08.png 306×51 — 練習卓 */}
          <SpriteButton src={`${IMG}/mj_ch_btn_08.png`} frameW={306} frameH={51} x={495} y={261} onClick={() => toKouryuLobby(4)} title={TRAINING_GUIDE} />
          {/* IDC_BTN_KOURYU_TAKU_6 (495,321) mj_ch_btn_10.png 306×51 — プロ・著名人卓 (ShowWindow(SW_HIDE)) */}
          <SpriteButton src={`${IMG}/mj_ch_btn_10.png`} frameW={306} frameH={51} x={495} y={321} onClick={() => toKouryuLobby(5)} title="プロ・著名人卓" hidden />

          {/* ── 交流戦 ユーザー人数 TextOut (18px Bold MS Gothic 白) ── */}
          {/* TextOut(380,215,...) TextOut(380,275,...) TextOut(380,335,...) */}
          <span style={cntStyle(380, 215)}>{countKouryu[0]}</span>
          <span style={cntStyle(380, 275)}>{countKouryu[1]}</span>
          <span style={cntStyle(380, 335)}>{countKouryu[2]}</span>
          {/* TextOut(820,215,...) TextOut(820,275,...) */}
          <span style={cntStyle(820, 215)}>{countKouryu[3]}</span>
          <span style={cntStyle(820, 275)}>{countKouryu[4]}</span>

          {/* ── 交流戦 説明テキスト TextOut (14px Bold MS Gothic 白 等幅) ── */}
          {INFO_KOURYU.map((line, i) => (
            <span key={i} style={infoStyle(70, 530 + i * 15)}>{line}</span>
          ))}
        </>
      )}

      {/* ───────────────────────────────────────────────────────────── */}
      {/* 段位戦グループボタン (m_nGroup == IDC_CHK_DANI)              */}
      {/* ───────────────────────────────────────────────────────────── */}
      {isDani && (
        <>
          {/* IDC_BTN_DANI_TAKU_1 (57,261) mj_ch_bk_01_btn_l_01.png 306×51 — 通常卓 */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_l_01.png`} frameW={306} frameH={51} x={57}  y={261} onClick={() => toDaniLobby(0)} title="通常卓" />
          {/* IDC_BTN_DANI_TAKU_2 (57,321) mj_ch_bk_01_btn_l_02.png 306×51 — 段位卓 */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_l_02.png`} frameW={306} frameH={51} x={57}  y={321} onClick={() => toDaniLobby(1)} title="段位卓" />
          {/* IDC_BTN_DANI_TAKU_3 (57,381) mj_ch_bk_01_btn_l_03.png 306×51 — 高段位卓 */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_l_03.png`} frameW={306} frameH={51} x={57}  y={381} onClick={() => toDaniLobby(2)} title="高段位卓" />
          {/* IDC_BTN_DANI_TAKU_4 (57,441) mj_ch_bk_01_btn_l_04.png 306×51 — 十段位卓 */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_l_04.png`} frameW={306} frameH={51} x={57}  y={441} onClick={() => toDaniLobby(3)} title="十段位卓" />
          {/* IDC_BTN_DANI_TAKU_5 (495,261) mj_ch_bk_01_btn_r_01.png 306×51 — 通常卓（右） */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_r_01.png`} frameW={306} frameH={51} x={495} y={261} onClick={() => toDaniLobby(4)} title="通常卓" />
          {/* IDC_BTN_DANI_TAKU_6 (495,321) mj_ch_bk_01_btn_r_02.png 306×51 — 段位卓（右） */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_r_02.png`} frameW={306} frameH={51} x={495} y={321} onClick={() => toDaniLobby(5)} title="段位卓" />
          {/* IDC_BTN_DANI_TAKU_7 (495,381) mj_ch_bk_01_btn_r_03.png 306×51 — 高段位卓（右） */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_r_03.png`} frameW={306} frameH={51} x={495} y={381} onClick={() => toDaniLobby(6)} title="高段位卓" />
          {/* IDC_BTN_DANI_TAKU_8 (495,441) mj_ch_bk_01_btn_r_04.png 306×51 — 十段位卓（右） */}
          <SpriteButton src={`${IMG}/mj_ch_bk_01_btn_r_04.png`} frameW={306} frameH={51} x={495} y={441} onClick={() => toDaniLobby(7)} title="十段位卓" />

          {/* ── 段位戦 ユーザー人数 TextOut (18px Bold MS Gothic 白) ── */}
          {/* TextOut(380,275..455) TextOut(820,275..455) */}
          <span style={cntStyle(380, 275)}>{countDani[0]}</span>
          <span style={cntStyle(380, 335)}>{countDani[1]}</span>
          <span style={cntStyle(380, 395)}>{countDani[2]}</span>
          <span style={cntStyle(380, 455)}>{countDani[3]}</span>
          <span style={cntStyle(820, 275)}>{countDani[4]}</span>
          <span style={cntStyle(820, 335)}>{countDani[5]}</span>
          <span style={cntStyle(820, 395)}>{countDani[6]}</span>
          <span style={cntStyle(820, 455)}>{countDani[7]}</span>

          {/* ── 段位戦 説明テキスト TextOut (14px Bold MS Gothic 白 等幅) ── */}
          {INFO_DANI.map((line, i) => (
            <span key={i} style={infoStyle(70, 530 + i * 15)}>{line}</span>
          ))}
        </>
      )}

      {/* ── 戻るボタン — IDCANCEL (821,557) mj_btn_title.png 164×55 ── */}
      <SpriteButton
        src={`${IMG}/mj_btn_title.png`}
        frameW={164} frameH={55}
        x={821} y={557}
        onClick={onBack}
        title="タイトルに戻る"
      />

      {/* ── DrawMemberInfo 相当: CMJSelLobbyWnd::OnPaint() → DrawMemberInfo() ── */}
      <DrawMemberInfo />

      {/* ── 終了ボタン — IDC_SETTING_BTN_EXT (821,625) mj_btn_exit.png 164×55 ── */}
      <SpriteButton
        src={`${IMG}/mj_btn_exit.png`}
        frameW={164} frameH={55}
        x={821} y={625}
        onClick={onExit}
        title="終了"
      />
    </div>
  )
}

function getGroupFromChannelId(channelId?: string) {
  if (!channelId) return undefined
  if (KOURYU_FIELD_IDS.includes(channelId)) return 'kouryu'
  if (DANI_FIELD_IDS.includes(channelId)) return 'dani'
  return undefined
}

/** ユーザー人数テキストスタイル (OnPaint 18px Bold MS Gothic 白 透過) */
function cntStyle(x: number, y: number): React.CSSProperties {
  return {
    position: 'absolute',
    left: x,
    top: y,
    fontFamily: 'var(--majak-font-family-ui)',
    fontSize: 'calc(18px * var(--majak-type-scale))',
    lineHeight: '18px',
    fontWeight: 'bold',
    color: '#ffffff',
    whiteSpace: 'pre',
    pointerEvents: 'none',
  }
}

/** 説明テキストスタイル (OnPaint 14px Bold MS Gothic 白 等幅) */
function infoStyle(x: number, y: number): React.CSSProperties {
  return {
    position: 'absolute',
    left: x,
    top: y,
    fontFamily: 'var(--majak-font-family-ui)',
    fontSize: 'calc(14px * var(--majak-type-scale))',
    lineHeight: '14px',
    fontWeight: 'bold',
    color: '#ffffff',
    whiteSpace: 'nowrap',
    pointerEvents: 'none',
  }
}
