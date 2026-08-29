import { describe, expect, it } from 'vitest'
import { getHangeLoginUrl, isHangeClientHost } from '../api/auth'
import { getAvatarUrl, getGameAvatarUrl, getHighResolutionGameAvatarUrl, getShortAvatarUrl, getWebHalfAvatarUrl, handleShortAvatarError } from './resources'

describe('Hangame avatar URLs', () => {
  it('builds v2 full, game, half, and short URLs', () => {
    const avatarId = '1121NNA-4_P_3_V_U_L0FU_F1GT'

    expect(getAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AWFA_${avatarId}.GIF`)
    expect(getGameAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AGFA_${avatarId}.GIF`)
    expect(getHighResolutionGameAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AWFA_${avatarId}.GIF`)
    expect(getWebHalfAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AWHA_${avatarId}.GIF`)
    expect(getShortAvatarUrl(avatarId)).toBe(`https://alpha-avatar.hange.jp/IMG_AVTR/ACHA_${avatarId}.GIF`)
  })

  it('builds v3 supported-animation URLs', () => {
    const avatarId = '2XXXXX'

    expect(getAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AWFS_${avatarId}.GIF`)
    expect(getGameAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AGBS_${avatarId}.GIF`)
    expect(getHighResolutionGameAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AWFS_${avatarId}.GIF`)
    expect(getWebHalfAvatarUrl(avatarId)).toBe(`https://avatar.hange.jp/IMG_AVTR/AWHS_${avatarId}.GIF`)
    expect(getShortAvatarUrl(avatarId)).toBe(`https://alpha-avatar.hange.jp/IMG_AVTR/ACHS_${avatarId}.GIF`)
  })

  it('keeps local selected avatars unchanged', () => {
    const localAvatar = '/assets/images/characters/thumbnail_03f.png'

    expect(getAvatarUrl(localAvatar)).toBe(localAvatar)
    expect(getGameAvatarUrl(localAvatar)).toBe(localAvatar)
    expect(getHighResolutionGameAvatarUrl(localAvatar)).toBe(localAvatar)
    expect(getShortAvatarUrl(localAvatar)).toBe(localAvatar)
  })

  it('retries a missing short avatar with web-half before using the default', () => {
    const avatarId = '1121NNA-4_P_3_V_U_L0FU_F1GT'
    const image = { src: '', dataset: {} } as unknown as HTMLImageElement

    handleShortAvatarError(image, avatarId, 'female')
    expect(image.src).toBe(`https://avatar.hange.jp/IMG_AVTR/AWHA_${avatarId}.GIF`)

    handleShortAvatarError(image, avatarId, 'female')
    expect(image.src).toBe('/assets/images/characters/thumbnail_01f.png')
  })
})

describe('Hangame login routing', () => {
  it('matches only hange.jp and its subdomains', () => {
    expect(isHangeClientHost('game.hange.jp')).toBe(true)
    expect(isHangeClientHost('HANGE.JP')).toBe(true)
    expect(isHangeClientHost('hange.jp.example.com')).toBe(false)
    expect(isHangeClientHost('not-hange.jp')).toBe(false)
  })

  it('keeps the entire current URL in nexturl', () => {
    const currentUrl = 'https://game.hange.jp/channel?mode=1#room'
    const loginUrl = new URL(getHangeLoginUrl(currentUrl))

    expect(loginUrl.origin + loginUrl.pathname).toBe('https://alpha-top.hange.jp/login/index')
    expect(loginUrl.searchParams.get('nexturl')).toBe(currentUrl)
  })
})