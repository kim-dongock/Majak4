import { useState, useEffect, useRef } from 'react'
import type { MajakPlayer } from '../../../api/auth'
import { googleRegister, checkNickname, AuthError } from '../../../api/auth'
import { FEMALE_AVATARS, MALE_AVATARS } from '../../../utils/resources'
import './RegistrationDlg.css'

// ── ステップ定義 ────────────────────────────────────────────────────
type Step = 'terms' | 'nickname' | 'avatar'

interface Props {
  idToken:    string
  googleInfo: MajakPlayer
  onComplete: (player: MajakPlayer) => void
  onAuthExpired: () => void
}

export default function RegistrationDlg({ idToken, onComplete, onAuthExpired }: Props) {
  const [step,         setStep]         = useState<Step>('terms')
  const [termsChecked, setTermsChecked] = useState(false)
  const [nickname,     setNickname]     = useState('')
  const [nicknameMsg,  setNicknameMsg]  = useState<{ ok: boolean; text: string } | null>(null)
  const [checking,     setChecking]     = useState(false)
  const [sex,          setSex]          = useState<'M' | 'F'>('M')
  const [birthDecade,  setBirthDecade]  = useState('')
  const [birthYear,    setBirthYear]    = useState('')
  const [avatarIdx,    setAvatarIdx]    = useState(0)
  const [submitting,   setSubmitting]   = useState(false)
  const [error,        setError]        = useState('')
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  // ニックネームの入力から 600ms 後に重複チェック
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
      debounceRef.current = null
    }
    setNicknameMsg(null)
    const normalizedNickname = nickname.trim()
    if (normalizedNickname.length === 0) {
      setChecking(false)
      return
    }
    if (normalizedNickname.length < 4) {
      setChecking(false)
      setNicknameMsg({ ok: false, text: '4文字以上入力してください' })
      return
    }
    setChecking(true)
    debounceRef.current = setTimeout(async () => {
      const result = await checkNickname(normalizedNickname)
      setChecking(false)
      if (result.available) {
        setNicknameMsg({ ok: true,  text: '使用できます ✓' })
      } else if (result.reason === 'LENGTH') {
        setNicknameMsg({ ok: false, text: '4〜16文字で入力してください' })
      } else {
        setNicknameMsg({ ok: false, text: 'このニックネームは既に使用されています' })
      }
    }, 600)
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current) }
  }, [nickname])

  const canProceedNickname = nicknameMsg?.ok === true && !checking

  const selectSex = (next: 'M' | 'F') => { setSex(next); setAvatarIdx(0) }
  const avatars   = sex === 'M' ? MALE_AVATARS : FEMALE_AVATARS
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
    if (submitting || !canProceedNickname) return
    setSubmitting(true)
    setError('')
    try {
      const player = await googleRegister(idToken, nickname, sex, Number(birthYear), avatars[avatarIdx])
      onComplete(player)
    } catch (err: unknown) {
      if (err instanceof AuthError && err.message === 'GOOGLE_REGISTRATION_AUTH_EXPIRED') {
        onAuthExpired()
        return
      }
      const msg = err instanceof Error ? err.message : String(err)
      if (msg === 'NICKNAME_TAKEN') {
        setNicknameMsg({ ok: false, text: 'このニックネームは既に使用されています' })
        setStep('nickname')
      } else {
        setError('登録に失敗しました。もう一度お試しください。')
      }
      setSubmitting(false)
    }
  }

  // ── モバイル判定 (640px 未満) ─────────────────────────────────────
  // 縦横どちらか短い辺が640px未満 → スマートフォン（縦持ち・横持ち両対応）
  const checkMobile = () => typeof window !== 'undefined'
    ? Math.min(window.innerWidth, window.innerHeight) < 640 : false
  const [isMobile, setIsMobile] = useState(checkMobile)
  useEffect(() => {
    const handler = () => { setIsMobile(checkMobile()) }
    window.addEventListener('resize', handler)
    return () => window.removeEventListener('resize', handler)
  }, [])

  // ── 共通スタイル ──────────────────────────────────────────────────
  // モバイル: 自然高さ・オーバーレイ同色で全画面に見せる。デスクトップ: ゲームサイズ固定モーダル。
  const panelBase: React.CSSProperties = isMobile ? {
    display: 'flex', flexDirection: 'column',
    width: '100vw',
    boxSizing: 'border-box',
    padding: '10px 12px',
    border: 'none',
  } : {
    display: 'flex', flexDirection: 'column',
    maxHeight: '88dvh',
    overflowY: 'auto',
    boxSizing: 'border-box',
    padding: '28px 36px',
  }
  // モバイル: 利用規約は全画面固定(テキストスクロール用)、他は自然高さ
  const panelStyle: React.CSSProperties = isMobile
    ? { ...panelBase, height: '100dvh', overflowY: 'hidden' }
    : { ...panelBase, width: 'min(1019px, calc(100vw - 32px))', height: 'min(680px, calc(100dvh - 32px))', maxHeight: 'none' }
  const widePanelStyle: React.CSSProperties = isMobile
    ? { ...panelBase, height: '100dvh', overflowY: 'auto' }
    : { ...panelBase, width: 'min(1019px, calc(100vw - 32px))', height: 'min(680px, calc(100dvh - 32px))', maxHeight: 'none', overflowY: 'auto' }

  const overlayStyle: React.CSSProperties = isMobile ? {} : { padding: 16 }
  const stepIndicator = (label: string, active: boolean, done: boolean) => (
    <span className={`registration-step${active ? ' is-active' : ''}${done ? ' is-done' : ''}`}>
      <span>{label.slice(3)}</span>
    </span>
  )

  // ── STEP 1: 利用規約 ─────────────────────────────────────────────
  if (step === 'terms') {
    return (
      <div className="registration-overlay majak-screen-surface" style={overlayStyle}>
        <div className="registration-panel registration-panel--flow registration-panel--terms" style={panelStyle}>
          <div className="registration-steps">
            {stepIndicator('1. 利用規約', true, false)}
            {stepIndicator('2. ニックネーム', false, false)}
            {stepIndicator('3. プロフィール', false, false)}
          </div>
          <div className="registration-heading">利用規約</div>

          <article className="registration-terms">
            <h2>麻雀4 利用規約</h2>
            <section>
              <h3>第1条（目的）</h3>
              <p>本規約は、麻雀4（以下「本サービス」）の利用に関する条件を定めるものです。ユーザーは本規約に同意した上で本サービスを利用するものとします。</p>
            </section>
            <section>
              <h3>第2条（アカウント）</h3>
              <p>ユーザーは Google アカウントを利用して本サービスに登録します。アカウント情報の管理はユーザー自身の責任において行うものとします。第三者へのアカウントの譲渡・共有は禁止します。</p>
            </section>
            <section>
              <h3>第3条（禁止事項）</h3>
              <p>ユーザーは以下の行為を行ってはなりません。</p>
              <ol>
                <li>他のユーザーへの嫌がらせ、誹謗中傷</li>
                <li>チートツール・不正プログラムの使用</li>
                <li>サービス運営を妨害する行為</li>
                <li>法令または公序良俗に反する行為</li>
                <li>商業目的での無断利用</li>
              </ol>
            </section>
            <section>
              <h3>第4条（サービスの変更・中断）</h3>
              <p>運営者は事前通知なくサービス内容の変更・停止・終了を行う場合があります。これによって生じた損害について、運営者は責任を負いません。</p>
            </section>
            <section>
              <h3>第5条（個人情報の取扱い）</h3>
              <p>本サービスは Google 認証によって取得したメールアドレスおよびユーザー識別子と、登録時に入力された出生年をアカウント管理およびロビーの年齢帯フィルターに使用し、第三者への提供は行いません。</p>
            </section>
            <section>
              <h3>第6条（免責事項）</h3>
              <p>本サービスはゲームの継続的な提供を保証するものではありません。サービス利用に伴う損害について運営者は一切の責任を負いません。</p>
            </section>
            <section>
              <h3>第7条（規約の変更）</h3>
              <p>運営者は必要に応じて本規約を変更できるものとします。変更後も本サービスの利用を継続した場合、変更に同意したものとみなします。</p>
            </section>
          </article>

          <div className="registration-terms-footer">
            <label className="registration-consent">
              <input type="checkbox"
                checked={termsChecked} onChange={e => setTermsChecked(e.target.checked)} />
              <span>上記の利用規約を読み、同意します</span>
            </label>
            <button type="button" disabled={!termsChecked} onClick={() => setStep('nickname')}
              className="registration-command registration-command--primary"
              style={{
                minWidth: isMobile ? 100 : 140, minHeight: isMobile ? 30 : 38,
                fontSize: 'var(--majak-popup-font-emphasis)', padding: '0 20px',
                background: termsChecked ? '#1769aa' : '#aaa',
                color: '#fff', border: 'none', borderRadius: 3,
                cursor: termsChecked ? 'pointer' : 'default',
              }}>
              次へ →
            </button>
          </div>
        </div>
      </div>
    )
  }

  // ── STEP 2: ニックネーム ─────────────────────────────────────────
  if (step === 'nickname') {
    return (
      <div className="registration-overlay majak-screen-surface" style={overlayStyle}>
        <div className="registration-panel registration-panel--flow registration-panel--nickname" style={widePanelStyle}>
          <div className="registration-steps">
            {stepIndicator('1. 利用規約', false, true)}
            {stepIndicator('2. ニックネーム', true, false)}
            {stepIndicator('3. プロフィール', false, false)}
          </div>
          <div className="registration-heading">ニックネームの設定</div>

          <div className="registration-nickname-field">
            <div style={{ marginBottom: isMobile ? 6 : 10, fontSize: 'var(--majak-popup-font-body)', lineHeight: 'var(--majak-popup-leading-body)' }}>
              ゲーム内で表示されるニックネームを入力してください（4〜16文字）
            </div>
            <input type="text" value={nickname} onChange={e => setNickname(e.target.value)}
              maxLength={16} placeholder="ニックネーム (4〜16文字)"
              className="registration-nickname-input"
              style={{
                width: '100%', boxSizing: 'border-box',
                padding: isMobile ? '7px 10px' : '10px 14px',
                fontSize: 'var(--majak-popup-font-emphasis)',
                border: nicknameMsg
                  ? `2px solid ${nicknameMsg.ok ? '#4caf50' : '#d32f2f'}`
                  : '1px inset #aaa',
              }}
            />
            <div style={{ minHeight: 20, marginTop: 5, fontSize: 'var(--majak-popup-font-body)', lineHeight: 'var(--majak-popup-leading-body)',
              color: nicknameMsg?.ok ? '#2e7d32' : '#d32f2f' }}>
              {checking && nickname.trim().length > 0 ? '確認中...' : (nicknameMsg?.text ?? '')}
            </div>
          </div>

          <div className="registration-actions" style={{ marginTop: 'auto', paddingTop: isMobile ? 12 : 24 }}>
            <button type="button" onClick={() => setStep('terms')}
              className="registration-command registration-command--secondary"
              style={{ minWidth: isMobile ? 80 : 110, minHeight: isMobile ? 30 : 38, fontSize: 'var(--majak-popup-font-emphasis)', padding: '0 14px' }}>
              ← 戻る
            </button>
            <button type="button" disabled={!canProceedNickname} onClick={() => setStep('avatar')}
              className="registration-command registration-command--primary"
              style={{
                minWidth: isMobile ? 100 : 140, minHeight: isMobile ? 30 : 38,
                fontSize: 'var(--majak-popup-font-emphasis)', padding: '0 20px',
                background: canProceedNickname ? '#1769aa' : '#aaa',
                color: '#fff', border: 'none', borderRadius: 3,
                cursor: canProceedNickname ? 'pointer' : 'default',
              }}>
              次へ →
            </button>
          </div>
        </div>
      </div>
    )
  }

  // ── STEP 3: 性別・アバター ───────────────────────────────────────
  return (
    <div className="registration-overlay majak-screen-surface" style={overlayStyle}>
      <div className="registration-panel registration-panel--flow registration-panel--profile" style={widePanelStyle}>
        <div className="registration-steps">
          {stepIndicator('1. 利用規約', false, true)}
          {stepIndicator('2. ニックネーム', false, true)}
          {stepIndicator('3. プロフィール', true, false)}
        </div>

        <div className="registration-heading">プロフィール設定</div>

        <div style={{ flexShrink: 0, marginBottom: isMobile ? 6 : 10, fontSize: 'var(--majak-popup-font-body)', lineHeight: 'var(--majak-popup-leading-body)', color: '#444' }}>
          ニックネーム: <b>{nickname}</b>
        </div>

        <div className="registration-profile-controls">
          <fieldset className="registration-profile-field" aria-label="性別">
            <div className="registration-sex-control" role="radiogroup" aria-label="性別">
              <button type="button" role="radio" aria-checked={sex === 'M'}
                className={sex === 'M' ? 'is-selected' : ''} onClick={() => selectSex('M')}>
                <img src={MALE_AVATARS[0]} alt="" />
                <span>男性</span>
              </button>
              <button type="button" role="radio" aria-checked={sex === 'F'}
                className={sex === 'F' ? 'is-selected' : ''} onClick={() => selectSex('F')}>
                <img src={FEMALE_AVATARS[0]} alt="" />
                <span>女性</span>
              </button>
            </div>
          </fieldset>

          <fieldset className="registration-profile-field" aria-label="出生年">
            <div className="registration-birth-control">
              <label>
                <select aria-label="年代" value={birthDecade} onChange={event => {
                  setBirthDecade(event.currentTarget.value)
                  setBirthYear('')
                }}>
                  <option value="">出生年代を選択</option>
                  {birthDecades.map(decade => <option key={decade} value={decade}>{decade}年代</option>)}
                </select>
              </label>
              <label>
                <select aria-label="年" value={birthYear} disabled={birthDecade === ''}
                  onChange={event => setBirthYear(event.currentTarget.value)}>
                  <option value="">年を選択</option>
                  {birthYears.map(year => <option key={year} value={year}>{year}年</option>)}
                </select>
              </label>
            </div>
          </fieldset>
        </div>

        {/* アバターグリッド
            モバイル: 自然高さ・正方形セル / デスクトップ: 3/4 縦長セル */}
        <div className="registration-avatar-grid" style={isMobile ? {
          display: 'grid',
          gridTemplateColumns: 'repeat(8, minmax(0, 1fr))',
          gridTemplateRows: 'repeat(2, minmax(0, 1fr))',
          gap: 'clamp(5px, 1.2vw, 8px)',
          border: '2px groove #fff',
          padding: '6px 8px',
          boxSizing: 'border-box',
          marginBottom: 4,
        } : {
          display: 'grid',
          gridTemplateColumns: 'repeat(8, minmax(0, 1fr))',
          gridTemplateRows: 'repeat(2, minmax(0, 1fr))',
          gap: 12,
          border: '2px groove #fff',
          padding: '14px 16px',
          boxSizing: 'border-box',
          marginBottom: 16,
        }}>
          {avatars.map((url, index) => (
            <button key={url} type="button"
              aria-label={`アバター ${index + 1}`} aria-pressed={avatarIdx === index}
              onClick={() => setAvatarIdx(index)}
              className={avatarIdx === index ? 'is-selected' : ''}
              style={{
                width: '100%',
                height: isMobile ? '100%' : undefined,
                minHeight: 0,
                aspectRatio: isMobile ? 'auto' : '3/4',
                padding: isMobile ? 2 : 4,
                border: avatarIdx === index ? '3px solid #1769aa' : '2px outset #eee',
                background: '#fff', cursor: 'pointer', overflow: 'hidden',
              }}>
              <img src={url} alt="" draggable={false}
                style={{ width: '100%', height: '100%', objectFit: 'contain' }} />
            </button>
          ))}
        </div>

        {error && (
          <div style={{ flexShrink: 0, marginTop: 6, color: '#b00020', fontSize: 'var(--majak-popup-font-body)', lineHeight: 'var(--majak-popup-leading-body)' }}>{error}</div>
        )}

        <div className="registration-actions" style={{ marginTop: isMobile ? 8 : 0 }}>
          <button type="button" onClick={() => setStep('nickname')} disabled={submitting}
            className="registration-command registration-command--secondary"
            style={{ minWidth: isMobile ? 80 : 110, minHeight: isMobile ? 30 : 38, fontSize: 'var(--majak-popup-font-emphasis)', padding: '0 14px' }}>
            ← 戻る
          </button>
          <button type="button" disabled={submitting || birthYear === ''} onClick={() => void submit()}
            className="registration-command registration-command--primary"
            style={{
              minWidth: isMobile ? 100 : 140, minHeight: isMobile ? 30 : 38,
              fontSize: 'var(--majak-popup-font-emphasis)', padding: '0 20px',
              background: birthYear === '' ? '#aaa' : '#1769aa', color: '#fff', border: 'none',
              borderRadius: 3, cursor: birthYear === '' ? 'default' : 'pointer',
            }}>
            {submitting ? '登録中...' : '登録する'}
          </button>
        </div>
      </div>
    </div>
  )
}
