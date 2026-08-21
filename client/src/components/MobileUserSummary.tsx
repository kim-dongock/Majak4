import { useEffect, useState } from 'react'
import { getPlayerCollection } from '../api/collection'
import { useAuthStore } from '../store/authStore'
import { useGamePlayerStore } from '../store/gamePlayerStore'
import { gradeLevelName } from '../utils/grade'

interface MobileUserSummaryProps {
  gameMoney?: number
  assetTitle?: string
  achievementTitle?: string
  trickTitle?: string
  showGrade?: boolean
  loadProfile?: boolean
  className?: string
}

export default function MobileUserSummary({
  gameMoney,
  assetTitle,
  achievementTitle,
  trickTitle,
  showGrade = false,
  loadProfile = true,
  className = '',
}: MobileUserSummaryProps) {
  const player = useAuthStore(state => state.player)
  const profile = useGamePlayerStore(state => state.data)
  const fetchProfile = useGamePlayerStore(state => state.fetchProfile)
  const [profileTitles, setProfileTitles] = useState({ achievement: '', trick: '' })

  useEffect(() => {
    if (!loadProfile || !player?.pix) return

    let active = true
    const refreshProfile = () => {
      void fetchProfile(player.pix)
      void getPlayerCollection().then(collection => {
        if (!active) return
        setProfileTitles({
          achievement: collection.majakTitles.find(title => title.isEquipped)?.titleName ?? '',
          trick: collection.trickTitles.find(title => title.isEquipped)?.titleName ?? '',
        })
      }).catch(() => {})
    }
    const refreshWhenVisible = () => {
      if (document.visibilityState === 'visible') refreshProfile()
    }

    refreshProfile()
    window.addEventListener('focus', refreshProfile)
    document.addEventListener('visibilitychange', refreshWhenVisible)
    return () => {
      active = false
      window.removeEventListener('focus', refreshProfile)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
    }
  }, [fetchProfile, loadProfile, player?.pix])

  if (!player) return null

  const currentGameMoney = gameMoney ?? profile?.gamMoney
  const currentAssetTitle = assetTitle ?? profile?.slevel
  const currentAchievementTitle = achievementTitle ?? profileTitles.achievement
  const currentTrickTitle = trickTitle ?? profileTitles.trick

  return (
    <div className={`majak-mobile-user-summary${className ? ` ${className}` : ''}`} aria-label="ユーザー情報">
      <span className="majak-mobile-user-summary__name">
        <b className="majak-mobile-user-summary__name-label majak-type-xs">ニックネーム</b>
        <strong className="majak-mobile-user-summary__name-value majak-type-lg" title={player.name}>{player.name}</strong>
      </span>
      <div className="majak-mobile-user-summary__fields">
        <span><b className="majak-type-xs">GP</b><em className="majak-type-md">{currentGameMoney?.toLocaleString('ja-JP') ?? '-'}</em></span>
        <span><b className="majak-type-xs">資産</b><em className="majak-type-md">{currentAssetTitle || '-'}</em></span>
        {showGrade && <span><b className="majak-type-xs">段位</b><em className="majak-type-md">{gradeLevelName(profile?.gradeLevel)}</em></span>}
        <span><b className="majak-type-xs">実績</b><em className="majak-type-md">{currentAchievementTitle || '-'}</em></span>
        <span><b className="majak-type-xs">技</b><em className="majak-type-md">{currentTrickTitle || '-'}</em></span>
      </div>
    </div>
  )
}