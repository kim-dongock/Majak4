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

  return <div className={`majak-popup-overlay mission-reward-guide-overlay${modeClass}`}>
    <section className={`majak-popup-panel mission-reward-guide${modeClass}`} role="dialog" aria-modal="true" aria-labelledby="mission-reward-guide-title">
      <header className="majak-popup-titlebar">
        <div><h2 id="mission-reward-guide-title">GP獲得ガイド</h2></div>
        <button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button>
      </header>
      <main className="majak-popup-body">
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
    <style>{`
      .mission-reward-guide-overlay { position: absolute; inset: 0; z-index: 270; display: grid; place-items: center; padding: 20px; overflow: hidden; box-sizing: border-box; background: rgba(8,16,20,.76); font-family: var(--majak-font-family-ui); }
      .mission-reward-guide { width: min(650px, 100%); color: #172323; border: 1px solid #7d8e80; background: #f5f2e9; box-shadow: 0 24px 72px rgba(0,0,0,.42); }
      .mission-reward-guide header { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 15px 20px; color: #fff; background: #174b43; }
      .mission-reward-guide header h2 { margin: 0; font-size: var(--majak-dialog-title-font-size); }
      .mission-reward-guide main { display: grid; gap: 22px; padding: 22px; }
      .mission-reward-guide__flow { display: grid; gap: 1px; border: 1px solid #c8d0c2; background: #c8d0c2; }
      .mission-reward-guide__flow div { display: grid; grid-template-columns: 27px 1fr; column-gap: 10px; padding: 11px 12px; background: #fffdf8; }
      .mission-reward-guide__flow b { display: grid; grid-row: span 2; place-items: center; width: 25px; height: 25px; border-radius: 50%; color: #fff; background: #1c5a4d; }
      .mission-reward-guide__flow strong { margin-left: -8px; color: #1f4d42; font-size: var(--majak-font-14); font-weight: 700 !important; }
      .mission-reward-guide__flow span { margin-top: 3px; color: #52645d; font-size: var(--majak-font-12); line-height: 1.45; }
      .mission-reward-guide h3 { margin: 0 0 10px; color: #31473f; font-size: var(--majak-font-16); }
      .mission-reward-guide__rewards { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 7px; }
      .mission-reward-guide__rewards div { display: grid; gap: 4px; padding: 10px; border: 1px solid #d4c58d; background: #fffdf2; text-align: center; }
      .mission-reward-guide__rewards span { color: #8b6b20; font-size: var(--majak-font-11); }
      .mission-reward-guide__rewards strong { color: #a64a27; font-size: var(--majak-font-15); }
      .mission-reward-guide__note { margin: 11px 0 0; color: #607069; font-size: var(--majak-font-12); line-height: 1.5; }
      .mission-reward-guide footer { display: flex; justify-content: flex-end; padding: 12px 20px; border-top: 1px solid #c8d0c2; background: #e8ede4; }
      .mission-reward-guide footer button { width: var(--majak-popup-command-width); height: var(--majak-popup-command-height); border: 1px solid #839087; border-radius: 3px; color: #32453e; background: transparent; font: 700 var(--majak-popup-command-font-size)/1 var(--majak-font-family-ui); cursor: pointer; }
      .mission-reward-guide--mobileLandscape, .mission-reward-guide--mobilePortrait { width: 100%; max-height: 100%; overflow: auto; }
      .mission-reward-guide-overlay--mobileLandscape, .mission-reward-guide-overlay--mobilePortrait { padding: 0; }
      .mission-reward-guide--mobileLandscape main, .mission-reward-guide--mobilePortrait main { gap: 5px; padding: 8px; }
      .mission-reward-guide--mobileLandscape .mission-reward-guide__flow div, .mission-reward-guide--mobilePortrait .mission-reward-guide__flow div { grid-template-columns: 23px 1fr; column-gap: 7px; padding: 3px 8px; }
      .mission-reward-guide--mobileLandscape .mission-reward-guide__flow b, .mission-reward-guide--mobilePortrait .mission-reward-guide__flow b { width: 21px; height: 21px; }
      .mission-reward-guide--mobileLandscape .mission-reward-guide__flow span, .mission-reward-guide--mobilePortrait .mission-reward-guide__flow span { margin-top: 1px; }
      .mission-reward-guide--mobileLandscape h3, .mission-reward-guide--mobilePortrait h3 { margin-bottom: 4px; }
      .mission-reward-guide--mobileLandscape main > section, .mission-reward-guide--mobilePortrait main > section { min-width: 0; }
      .mission-reward-guide--mobileLandscape .mission-reward-guide__rewards, .mission-reward-guide--mobilePortrait .mission-reward-guide__rewards { width: 100%; min-width: 0; max-width: 100%; grid-template-columns: repeat(6, minmax(72px, 1fr)); gap: 3px; overflow-x: auto; }
      .mission-reward-guide--mobileLandscape .mission-reward-guide__rewards div, .mission-reward-guide--mobilePortrait .mission-reward-guide__rewards div { gap: 0; padding: 2px 6px; }
      .mission-reward-guide--mobileLandscape .mission-reward-guide__note, .mission-reward-guide--mobilePortrait .mission-reward-guide__note { margin-top: 4px; }
    `}</style>
  </div>
}