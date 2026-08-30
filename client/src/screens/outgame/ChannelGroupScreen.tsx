/**
 * CMJSelGroupWnd 相当 — チャンネルグループ選択画面 (AP-09 §1-3)
 * レガシー: legacy/client/HgMajak2/MJSelGroupWnd.h/cpp
 *
 * レガシーのカテゴリと遷移先を維持しつつ、
 * デスクトップはレスポンシブな Web コントロールとして表示する。
 */
import { useNavigate } from 'react-router-dom'
import { useEffect } from 'react'
import MobileUserSummary from '../../components/MobileUserSummary'
import * as SignalR from '../../api/signalr'

const MOBILE_MAIN_VISUAL_SRC = 'https://images.hange.jp/hangame/easy/majak4/client/bnr/top_majak_20220329.png'

/** ====================================================================
 * CMJSelGroupWnd 本体
 * ==================================================================== */

const descriptions = [
  'ルール、対戦相手、レートを選んで対戦！',
  '基本ルールで実力を競え！',
  '公式大会の参加はこちら！詳細は公式お知らせにて',
  '牌譜を見直して雀力を高めよう！',
]

export default function ChannelGroupScreen() {
  const navigate = useNavigate()

  useEffect(() => {
    void SignalR.disconnect().catch(() => {})
  }, [])

  /** OnCommand() → GetParent()->SendMessage(WM_COMMAND) に相当するナビゲーション */
  const onKouryu  = () => navigate('/channel/select/kouryu')   // IDC_BTN_CATEGORY_KOURYU → EnterCustom(IDC_CHK_STAND)
  const onDani    = () => navigate('/channel/select/dani')     // IDC_BTN_CATEGORY_DANI   → EnterCustom(IDC_CHK_DANI)
  const onTaikai  = () => navigate('/channel/00H8A/lobby')     // IDC_BTN_CATEGORY_TAIKAI → EnterQuick(TOURNAMENT_SUBID)
  const onPaifu   = () => navigate('/paifu')                   // サーバー保管の自分の牌譜一覧を開く
  const items = [
    { title: '交流戦', description: descriptions[0], onClick: onKouryu },
    { title: '段位戦', description: descriptions[1], onClick: onDani },
    { title: '大会', description: descriptions[2], onClick: onTaikai },
    { title: '牌譜再生', description: descriptions[3], onClick: onPaifu },
  ]

  return (
    <div className="majak-desktop-channel-group majak-screen-surface">
      <header className="majak-desktop-channel-group__header">
        <img className="majak-desktop-channel-group__logo" src={MOBILE_MAIN_VISUAL_SRC} alt="麻雀4" draggable={false} />
        <MobileUserSummary className="majak-desktop-channel-group__user-summary" showGrade showAvatar showName={false} showGameMoney={false} />
      </header>
      <main className="majak-desktop-channel-group__menu" aria-label="対戦メニュー">
        {items.map(item => (
          <div key={item.title} className="majak-desktop-channel-group__menu-entry">
            <button type="button" className="majak-responsive-control-button majak-responsive-menu-button majak-desktop-channel-group__action" onClick={item.onClick}>
              <strong>{item.title}</strong>
            </button>
            <p className="majak-desktop-channel-group__description">{item.description}</p>
          </div>
        ))}
      </main>
    </div>
  )
}

