import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

interface Props {
  onClose: () => void
}

const GP_REWARDS = [
  ['100 P', '1,000 GP'],
  ['150 P', '1,500 GP'],
  ['200 P', '2,000 GP'],
  ['400 P', '2,500 GP'],
  ['500 P', '3,000 GP'],
  ['600 P', '15,000 GP'],
]

export default function MissionRewardGuideDlg({ onClose }: Props) {
  const layoutMode = useOutgameLayoutMode()
  const modeClass = layoutMode === 'desktop' ? '' : ` mission-reward-guide--${layoutMode}`

  return <div className={`majak-popup-overlay${modeClass}`}>
    <section className={`majak-popup-panel mission-reward-guide${modeClass}`} role="dialog" aria-modal="true" aria-labelledby="mission-reward-guide-title">
      <header className="majak-popup-titlebar">
        <div><h2 id="mission-reward-guide-title">GP獲得ガイド</h2></div>
        <button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button>
      </header>
      <main className="majak-popup-body mission-reward-guide__body">
        <section className="mission-reward-guide__flow" aria-label="GP獲得の流れ">
          <div><b>1</b><strong className="mission-reward-guide__step-title">デイリーミッションを達成</strong><span>達成したミッションのポイントが加算されます。</span></div>
          <div><b>2</b><strong className="mission-reward-guide__step-title">1週間のポイントをためる</strong><span>ポイントは毎週の報酬条件に使われます。</span></div>
          <div><b>3</b><strong className="mission-reward-guide__step-title">週間報酬を受け取る</strong><span>条件を満たした報酬の「受け取る」でGPが残高に反映されます。</span></div>
        </section>
        <section>
          <h3>GPがもらえる週間報酬</h3>
          <div className="mission-reward-guide__rewards">
            {GP_REWARDS.map(([points, reward]) => <div key={points}><span>{points}</span><strong>{reward}</strong></div>)}
          </div>
          <p className="mission-reward-guide__note">50 Pと300 Pの週間報酬は龍珠です。GPは「受け取る」を押した時点で付与されます。</p>
        </section>
      </main>
      <footer className="majak-popup-actions"><button type="button" onClick={onClose}>ミッションに戻る</button></footer>
    </section>
  </div>
}