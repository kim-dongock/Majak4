import { GAME_LOAD_STEPS, type GameLoadStep } from '../game/gameLoadProgress'

export default function GameReconnectLoading({
  visible,
  currentStep,
  complete,
  fixed = false,
}: {
  visible: boolean
  currentStep: GameLoadStep
  complete: boolean
  fixed?: boolean
}) {
  if (!visible) return null
  const currentIndex = GAME_LOAD_STEPS.findIndex(step => step.id === currentStep)
  const totalSteps = GAME_LOAD_STEPS.length
  const currentCount = complete ? totalSteps : Math.max(1, currentIndex + 1)
  const progressPercent = Math.round((currentCount / totalSteps) * 100)

  return (
    <div className={`majak-game-loading${fixed ? ' is-fixed' : ''}`} role="status" aria-live="polite">
      <div className="majak-game-loading__panel" aria-busy={!complete} aria-label="対局状況を同期中">
        <img className="majak-game-loading__logo" src="/assets/images/common/ico_big_majak2.jpg" alt="" draggable={false} />
        <div
          className="majak-game-loading__progress"
          role="progressbar"
          aria-label="ゲーム開始準備"
          aria-valuemin={0}
          aria-valuemax={totalSteps}
          aria-valuenow={currentCount}
          aria-valuetext={`${currentCount} / ${totalSteps}`}
        >
          <div className="majak-game-loading__progress-fill" style={{ width: `${progressPercent}%` }} />
        </div>
      </div>
    </div>
  )
}