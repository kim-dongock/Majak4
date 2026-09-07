import { useEffect, useState } from 'react'
import { getPlayerCollection } from '../api/collection'
import { useAuthStore } from '../store/authStore'
import { useGamePlayerStore } from '../store/gamePlayerStore'
import { getAvatarUrl, getDefaultAvatarUrl } from '../utils/resources'
import { getMajakTitleImageUrl, getTrickTitleImageUrl } from '../utils/titleImages'

interface MobileUserSummaryProps {
  gameMoney?: number
  assetTitle?: string
  achievementTitle?: string
  achievementTitleId?: string
  trickTitle?: string
  trickTitleId?: string
  showAvatar?: boolean
  showTitleArt?: boolean
  showName?: boolean
  showGameMoney?: boolean
  loadProfile?: boolean
  className?: string
}

export default function MobileUserSummary({
  gameMoney,
  assetTitle,
  achievementTitle,
  achievementTitleId,
  trickTitle,
  trickTitleId,
  showAvatar = false,
  showTitleArt = true,
  showName = true,
  showGameMoney = true,
  loadProfile = true,
  className = '',
}: MobileUserSummaryProps) {
  const player = useAuthStore(state => state.player)
  const profile = useGamePlayerStore(state => state.data)
  const fetchProfile = useGamePlayerStore(state => state.fetchProfile)
  const [profileTitles, setProfileTitles] = useState({ achievement: '', achievementId: '', trick: '', trickId: '' })

  useEffect(() => {
    if (!loadProfile || !player?.pix) return

    let active = true
    const refreshProfile = () => {
      void fetchProfile(player.pix)
      void getPlayerCollection().then(collection => {
        if (!active) return
        const equippedMajakTitle = [...collection.majakTitles, ...collection.titleTitles]
          .find(title => title.isEquipped)
        setProfileTitles({
          achievement: equippedMajakTitle?.titleName ?? '',
          achievementId: collection.equippedMajakTitle,
          trick: collection.trickTitles.find(title => title.isEquipped)?.titleName ?? '',
          trickId: collection.equippedTrickTitle,
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
  const majakTitleImage = getMajakTitleImageUrl(achievementTitleId ?? profileTitles.achievementId)
  const trickTitleImage = getTrickTitleImageUrl(trickTitleId ?? profileTitles.trickId)

  return (
    <div className={`majak-mobile-user-summary${!showName && !showGameMoney ? ' majak-mobile-user-summary--without-primary' : ''}${className ? ` ${className}` : ''}`} aria-label="ユーザー情報">
      {showAvatar && (
        <img
          className="majak-mobile-user-summary__avatar"
          src={getAvatarUrl(player.avatarId)}
          alt=""
          draggable={false}
          onError={event => {
            event.currentTarget.src = getDefaultAvatarUrl(player.sex === 'F' ? 'female' : 'male')
          }}
        />
      )}
      {showTitleArt && (showAvatar || majakTitleImage || trickTitleImage) && (
        <span className="majak-mobile-user-summary__title-art" aria-hidden="true">
          {trickTitleImage && <img className="majak-mobile-user-summary__trick-title" src={trickTitleImage} alt="" />}
          {majakTitleImage && (
            <>
              <img className="majak-mobile-user-summary__title-base" src="/assets/images/game/mj_title_base.png" alt="" />
              <img className="majak-mobile-user-summary__majak-title" src={majakTitleImage} alt="" />
            </>
          )}
        </span>
      )}
      {showName && (
        <span className="majak-mobile-user-summary__name">
          <b className="majak-mobile-user-summary__name-label">ニックネーム</b>
          <strong className="majak-mobile-user-summary__name-value" title={player.name}>{player.name}</strong>
        </span>
      )}
      <div className="majak-mobile-user-summary__fields">
        {showGameMoney && <span><b>GP</b><em>{currentGameMoney?.toLocaleString('ja-JP') ?? '-'}</em></span>}
        <span><b>資産称号</b><em>{currentAssetTitle || '-'}</em></span>
        <span><b>麻雀称号</b><em>{currentAchievementTitle || '-'}</em></span>
        <span><b>技</b><em>{currentTrickTitle || '-'}</em></span>
      </div>
    </div>
  )
}