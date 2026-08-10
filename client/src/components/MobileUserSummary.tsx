import { useEffect, useState } from 'react'
import { getPlayerCollection } from '../api/collection'
import { useAuthStore } from '../store/authStore'
import { useGamePlayerStore } from '../store/gamePlayerStore'

interface MobileUserSummaryProps {
  gameMoney?: number
  assetTitle?: string
  achievementTitle?: string
  trickTitle?: string
  loadProfile?: boolean
  className?: string
}

export default function MobileUserSummary({
  gameMoney,
  assetTitle,
  achievementTitle,
  trickTitle,
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
      <strong className="majak-mobile-user-summary__name" title={player.name}>{player.name}</strong>
      <div className="majak-mobile-user-summary__fields">
        <span><b>GP</b><em>{currentGameMoney?.toLocaleString('ja-JP') ?? '-'}</em></span>
        <span><b>資産</b><em>{currentAssetTitle || '-'}</em></span>
        <span><b>実績</b><em>{currentAchievementTitle || '-'}</em></span>
        <span><b>技</b><em>{currentTrickTitle || '-'}</em></span>
      </div>
    </div>
  )
}