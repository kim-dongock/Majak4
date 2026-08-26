/**
 * CMJSelLobbyWnd 相当 — ロビー選択画面 (AP-09 §1-4)
 * レガシー: legacy/client/HgMajak2/MJSelLobbyWnd.h/cpp
 *
 * レガシーのロビー種別、入場条件、人数表示を維持しつつ、
 * デスクトップはレスポンシブな Web コントロールとして表示する。
 *
 * 交流戦 フィールドID (lpcstrKouryuFieldID):
 *   0=0082B, 1=0086B, 2=0085F, 3=0075B, 4=00T5A, 5=00000(非表示)
 * 段位戦 フィールドID (lpcstrDaniFieldID):
 *   0=0ZG6A, 1=0ZG6B, 2=0ZG6C, 3=0ZG6D, 4=0ZG7A, 5=0ZG7B, 6=0ZG7C, 7=0ZG7D
 */
import { useNavigate, useParams } from 'react-router-dom'
import { useState, useEffect } from 'react'
import { getChannels } from '../../api/channel'
import MobileUserSummary from '../../components/MobileUserSummary'
import type { MJOption } from './dialogs/OptDlg'
import { showConfirm } from '../../utils/msgbox'

const TRAINING_ENTRY_TITLE = '練習広場入場'
const TRAINING_ENTRY_MESSAGE = 'ここは練習広場です。\n4人がそろわなくても対戦ができますが\n対戦した後の戦績やGPはゲーム終了時に元に戻ります。\n練習広場に入りますか？'

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

interface LobbyItem {
  title: string
  match?: '東風戦' | '半荘戦'
  description: string
  entryRequirement?: string
  count: string
  tableFee: string
  rate: string
  dragonBall: string
  onClick: () => void | Promise<void>
}

/** ====================================================================
 * CMJSelLobbyWnd 本体
 * ==================================================================== */
export default function LobbySelectScreen() {
  const { group, channelId } = useParams<{ group?: string; channelId?: string }>()
  const navigate = useNavigate()

  const requestedGroup = group ?? getGroupFromChannelId(channelId)
  const isDani = requestedGroup === 'dani'
  const isKouryu = requestedGroup === 'kouryu'
  const [selectedDaniMatch, setSelectedDaniMatch] = useState<'東風戦' | '半荘戦'>('東風戦')

  useEffect(() => {
    if (!isKouryu && !isDani) navigate('/channel', { replace: true })
  }, [isKouryu, isDani, navigate])

  /** ユーザー人数 (サーバーから取得するまで "-----人" で表示)
   * レガシー: MSGID_GET_NEXT_USER_CNT 相当 — /api/channels から memberCnt を取得
   */
  const [countKouryu, setCountKouryu] = useState<string[]>(Array(6).fill(DEFAULT_COUNT))
  const [countDani,   setCountDani]   = useState<string[]>(Array(8).fill(DEFAULT_COUNT))
  const [channelNames, setChannelNames] = useState<Record<string, string>>({})

  /** チャンネル比較から人数を読み込む */
  useEffect(() => {
    getChannels().then(channels => {
      const fmt = (n: number) => `${String(n).padStart(5, ' ')}人`
      const find = (subId: string) => {
        const ch = channels.find(c => c.subId === subId)
        return ch ? fmt(ch.memberCnt) : DEFAULT_COUNT
      }
      setChannelNames(Object.fromEntries(
        channels
          .filter(channel => channel.subId && channel.chanelName)
          .map(channel => [channel.subId, channel.chanelName]),
      ))
      setCountKouryu(KOURYU_FIELD_IDS.map(id => find(id)))
      setCountDani(DANI_FIELD_IDS.map(id => find(id)))
    }).catch(() => {})
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /** OnPageBack() — 前のページ (チャンネルグループ選択) へ戻る */
  const onBack = () => navigate('/channel')
  const channelTitle = (subId: string, fallback: string) => channelNames[subId] || fallback

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

  const lobbyItems: LobbyItem[] = isDani
    ? [
        { title: '通常卓', match: '東風戦', description: INFO_DANI[0], entryRequirement: '10級～三段 / GP 500以上', count: countDani[0], tableFee: '500 GP', rate: '20', dragonBall: '1位 1 / 2位 0', onClick: () => toDaniLobby(0) },
        { title: '段位卓', match: '東風戦', description: INFO_DANI[1], entryRequirement: '初段～九段 / GP 5,000以上', count: countDani[1], tableFee: '500 GP', rate: '20', dragonBall: '1位 2 / 2位 0', onClick: () => toDaniLobby(1) },
        { title: '高段位卓', match: '東風戦', description: INFO_DANI[2], entryRequirement: '四段～九段 / GP 10,000以上', count: countDani[2], tableFee: '500 GP', rate: '20', dragonBall: '1位 3 / 2位 1', onClick: () => toDaniLobby(2) },
        { title: '十段位卓', match: '東風戦', description: INFO_DANI[3], entryRequirement: '七段～九段 / GP 30,000以上', count: countDani[3], tableFee: '500 GP', rate: '20', dragonBall: '1位 4 / 2位 2', onClick: () => toDaniLobby(3) },
        { title: '通常卓', match: '半荘戦', description: INFO_DANI[0], entryRequirement: '10級～三段 / GP 500以上', count: countDani[4], tableFee: '500 GP', rate: '20', dragonBall: '1位 1 / 2位 0', onClick: () => toDaniLobby(4) },
        { title: '段位卓', match: '半荘戦', description: INFO_DANI[1], entryRequirement: '初段～九段 / GP 5,000以上', count: countDani[5], tableFee: '500 GP', rate: '20', dragonBall: '1位 2 / 2位 0', onClick: () => toDaniLobby(5) },
        { title: '高段位卓', match: '半荘戦', description: INFO_DANI[2], entryRequirement: '四段～九段 / GP 10,000以上', count: countDani[6], tableFee: '500 GP', rate: '20', dragonBall: '1位 3 / 2位 1', onClick: () => toDaniLobby(6) },
        { title: '十段位卓', match: '半荘戦', description: INFO_DANI[3], entryRequirement: '七段～九段 / GP 30,000以上', count: countDani[7], tableFee: '500 GP', rate: '20', dragonBall: '1位 4 / 2位 2', onClick: () => toDaniLobby(7) },
      ]
    : [
        { title: channelTitle('0082B', '基本卓（安い部屋）'), description: INFO_KOURYU[0], count: countKouryu[0], tableFee: '500 GP', rate: '20', dragonBall: '1位 0 / 2位 0', onClick: () => toKouryuLobby(0) },
        { title: channelTitle('0086B', '基本卓'), description: INFO_KOURYU[0], count: countKouryu[1], tableFee: '500 GP', rate: '20', dragonBall: '1位 0 / 2位 0', onClick: () => toKouryuLobby(1) },
        { title: channelTitle('0085F', 'ハイ卓'), description: INFO_KOURYU[1], count: countKouryu[2], tableFee: '3,000 GP', rate: '100', dragonBall: '1位 5 / 2位 2', onClick: () => toKouryuLobby(2) },
        { title: channelTitle('0075B', '基本卓（掛けあり）'), description: INFO_KOURYU[0], count: countKouryu[3], tableFee: '500 GP', rate: '20', dragonBall: '1位 0 / 2位 0', onClick: () => toKouryuLobby(3) },
        { title: channelTitle('00T5A', '練習卓'), description: INFO_KOURYU[2], count: countKouryu[4], tableFee: '0 GP', rate: '0', dragonBall: 'なし', onClick: () => toKouryuLobby(4) },
      ]
  const visibleLobbyItems = isDani
    ? lobbyItems.filter(item => item.match === selectedDaniMatch)
    : lobbyItems

  return (
    <div className="majak-desktop-lobby-select majak-screen-surface">
      <header className="majak-desktop-lobby-select__header">
        <div>
          <p className="majak-type-xs">ロビー選択</p>
          <h1 className="majak-type-display">{isDani ? '段位戦' : '交流戦'}</h1>
        </div>
        <MobileUserSummary className="majak-desktop-lobby-select__user-summary" showGrade />
      </header>
      {isDani && (
        <div className="majak-desktop-lobby-select__rule-tabs" role="tablist" aria-label="対局形式">
          {(['東風戦', '半荘戦'] as const).map(match => (
            <button
              key={match}
              type="button"
              role="tab"
              aria-selected={selectedDaniMatch === match}
              className={`majak-responsive-control-button majak-desktop-lobby-select__rule-tab${selectedDaniMatch === match ? ' is-active' : ''}`}
              onClick={() => setSelectedDaniMatch(match)}
            >
              <span>{match}</span>
            </button>
          ))}
        </div>
      )}
      <main className={`majak-desktop-lobby-select__list${isDani ? ' is-dani' : ''}`} aria-label={`${isDani ? '段位戦' : '交流戦'}ロビー一覧`}>
        <div className="majak-desktop-lobby-select__comparison-header" aria-hidden="true">
          <span>ロビー</span><span>接続</span><span>場代</span><span>レート</span><span>龍珠</span><span>入場条件</span>
        </div>
        {visibleLobbyItems.map(item => (
          <div key={`${item.title}-${item.match ?? ''}`} className="majak-desktop-lobby-select__entry">
            <button type="button" className="majak-responsive-control-button majak-responsive-menu-button majak-desktop-lobby-select__card" onClick={item.onClick}>
              <span className="majak-desktop-lobby-select__card-title">{item.title}</span>
            </button>
            <dl className="majak-desktop-lobby-select__stats" aria-label={`${item.title} 条件`}>
              <div><dt>接続</dt><dd>{item.count}</dd></div>
              <div><dt>場代</dt><dd>{item.tableFee}</dd></div>
              <div><dt>レート</dt><dd>{item.rate}</dd></div>
              <div><dt>龍珠</dt><dd>{item.dragonBall}</dd></div>
              <div className="majak-desktop-lobby-select__entry-condition"><dt>入場条件</dt><dd>{item.entryRequirement ?? '-'}</dd></div>
            </dl>
          </div>
        ))}
      </main>
      <footer className="majak-desktop-lobby-select__footer">
        <button type="button" className="majak-responsive-control-button majak-type-md" onClick={onBack}>戻る</button>
      </footer>
    </div>
  )
}

function getGroupFromChannelId(channelId?: string) {
  if (!channelId) return undefined
  if (KOURYU_FIELD_IDS.includes(channelId)) return 'kouryu'
  if (DANI_FIELD_IDS.includes(channelId)) return 'dani'
  return undefined
}
