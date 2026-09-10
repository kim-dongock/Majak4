import { useRef, useState } from 'react'
import type { MajakPlayer } from '../../../api/auth'
import { updateAccountProfile, type AccountProfileUpdate } from '../../../api/accountProfile'
import { FEMALE_AVATARS, MALE_AVATARS, normalizeAvatarUrl } from '../../../utils/resources'
import { applyMajakColorTheme, loadMajakConfig, UI_COLOR_THEMES, USER_COLOR_THEME_KEYS } from './CfgDlg'
import './ProfileEditDlg.css'

type Props = {
  player: MajakPlayer
  onComplete: (player: MajakPlayer) => void
  onClose: () => void
  onSave?: (profile: AccountProfileUpdate) => Promise<AccountProfileUpdate>
}

export default function ProfileEditDlg({ player, onComplete, onClose, onSave = updateAccountProfile }: Props) {
  const avatars = player.sex === 'F' ? FEMALE_AVATARS : MALE_AVATARS
  const normalizedAvatar = normalizeAvatarUrl(player.avatarId)
  const initialAvatarIndex = avatars.indexOf(normalizedAvatar)
  const initialBirthYear = player.birthYear ? String(player.birthYear) : ''
  const [birthDecade, setBirthDecade] = useState(initialBirthYear ? String(Math.floor(Number(initialBirthYear) / 10) * 10) : '')
  const [birthYear, setBirthYear] = useState(initialBirthYear)
  const [avatarIndex, setAvatarIndex] = useState(initialAvatarIndex >= 0 ? initialAvatarIndex : 0)
  const [userColor, setUserColor] = useState(player.userColor || '#1b6b55')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const savedRef = useRef(false)

  const currentYear = new Date().getFullYear()
  const currentDecade = Math.floor(currentYear / 10) * 10
  const birthDecades = Array.from(
    { length: (currentDecade - 1900) / 10 + 1 },
    (_, index) => currentDecade - index * 10,
  )
  const birthYears = birthDecade === ''
    ? []
    : Array.from({ length: 10 }, (_, index) => Number(birthDecade) + 9 - index)
      .filter(year => year <= currentYear && year >= 1900)

  const submit = async () => {
    if (saving || birthYear === '') return
    setSaving(true)
    setError('')
    try {
      const updated = await onSave({ birthYear: Number(birthYear), avatarId: avatars[avatarIndex], userColor })
      savedRef.current = true
      onComplete({ ...player, ...updated })
    } catch {
      setError('プロフィールの保存に失敗しました。もう一度お試しください。')
      setSaving(false)
    }
  }

  const previewUserColor = (color: string) => {
    setUserColor(color)
    applyMajakColorTheme({ ...loadMajakConfig(), themeBaseColor: color })
  }

  const close = () => {
    if (!savedRef.current) {
      applyMajakColorTheme({ ...loadMajakConfig(), themeBaseColor: player.userColor || '#1b6b55' })
    }
    onClose()
  }

  return (
    <div className="majak-popup-overlay majak-profile-edit-overlay">
      <section className="majak-popup-panel majak-profile-edit" role="dialog" aria-modal="true" aria-labelledby="majak-profile-edit-title">
        <header className="majak-popup-titlebar">
          <h2 id="majak-profile-edit-title">プロフィール編集</h2>
          <button type="button" className="majak-popup-titlebar__close" onClick={close} aria-label="閉じる">×</button>
        </header>

        <div className="majak-popup-body majak-profile-edit__body">
          <section className="majak-profile-edit__birth">
            <span className="majak-profile-edit__birth-title">出生年度</span>
            <div>
              <select
                aria-label="年代"
                value={birthDecade}
                onChange={event => {
                  setBirthDecade(event.currentTarget.value)
                  setBirthYear('')
                }}
              >
                  <option value="">年代を選択</option>
                  {birthDecades.map(decade => <option key={decade} value={decade}>{decade}年代</option>)}
              </select>
              <select
                aria-label="年"
                value={birthYear}
                disabled={birthDecade === ''}
                onChange={event => setBirthYear(event.currentTarget.value)}
              >
                  <option value="">年を選択</option>
                  {birthYears.map(year => <option key={year} value={year}>{year}年</option>)}
              </select>
            </div>
          </section>

          <section className="majak-profile-edit__color" aria-labelledby="majak-profile-edit-color-title">
            <span id="majak-profile-edit-color-title">ユーザーカラー</span>
            <div className="majak-profile-edit__color-presets" role="radiogroup" aria-label="ユーザーカラーのプリセット">
              {USER_COLOR_THEME_KEYS.map(key => {
                const theme = UI_COLOR_THEMES[key]
                return <button
                  key={key}
                  type="button"
                  className={userColor === theme.command ? 'is-selected' : undefined}
                  style={{ backgroundColor: theme.command }}
                  title={theme.label}
                  aria-label={theme.label}
                  aria-pressed={userColor === theme.command}
                  onClick={() => previewUserColor(theme.command)}
                />
              })}
            </div>
            <label className="majak-profile-edit__color-custom">
              自由選択
              <input type="color" value={userColor} onChange={event => previewUserColor(event.currentTarget.value)} aria-label="ユーザーカラーを自由選択" />
            </label>
          </section>

          <section className="majak-profile-edit__avatars">
            <div className="majak-profile-edit__avatar-grid">
              {avatars.map((avatar, index) => (
                <button
                  key={avatar}
                  type="button"
                  className={`majak-profile-edit__avatar${avatarIndex === index ? ' is-selected' : ''}`}
                  aria-label={`アバター ${index + 1}`}
                  aria-pressed={avatarIndex === index}
                  onClick={() => setAvatarIndex(index)}
                >
                  <img src={avatar} alt="" draggable={false} />
                </button>
              ))}
            </div>
          </section>

          {error && <p className="majak-profile-edit__error" role="alert">{error}</p>}
        </div>

        <footer className="majak-popup-actions">
          <button type="button" onClick={close} disabled={saving}>キャンセル</button>
          <button type="button" className="is-primary" onClick={() => void submit()} disabled={saving || birthYear === ''}>
            {saving ? '保存中...' : '保存する'}
          </button>
        </footer>
      </section>
    </div>
  )
}
