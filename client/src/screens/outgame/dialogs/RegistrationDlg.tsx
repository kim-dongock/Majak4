import { useState, useEffect, useRef } from 'react'
import type { MajakPlayer } from '../../../api/auth'
import { googleRegister, checkNickname } from '../../../api/auth'
import { FEMALE_AVATARS, MALE_AVATARS } from '../../../utils/resources'

// ── ステップ定義 ────────────────────────────────────────────────────
type Step = 'terms' | 'nickname' | 'avatar'

interface Props {
  idToken:    string
  googleInfo: MajakPlayer
  onComplete: (player: MajakPlayer) => void
}

export default function RegistrationDlg({ idToken, onComplete }: Props) {
  const [step,         setStep]         = useState<Step>('terms')
  const [termsChecked, setTermsChecked] = useState(false)
  const [nickname,     setNickname]     = useState('')
  const [nicknameMsg,  setNicknameMsg]  = useState<{ ok: boolean; text: string } | null>(null)
  const [checking,     setChecking]     = useState(false)
  const [sex,          setSex]          = useState<'M' | 'F'>('M')
  const [avatarIdx,    setAvatarIdx]    = useState(0)
  const [submitting,   setSubmitting]   = useState(false)
  const [error,        setError]        = useState('')
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  // ニックネームの入力から 600ms 後に重複チェック
  useEffect(() => {
    setNicknameMsg(null)
    if (nickname.length === 0) return
    if (nickname.length < 4) {
      setNicknameMsg({ ok: false, text: '4文字以上入力してください' })
      return
    }
    if (debounceRef.current) clearTimeout(debounceRef.current)
    setChecking(true)
    debounceRef.current = setTimeout(async () => {
      const result = await checkNickname(nickname)
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

  const submit = async () => {
    if (submitting || !canProceedNickname) return
    setSubmitting(true)
    setError('')
    try {
      const player = await googleRegister(idToken, nickname, sex, avatars[avatarIdx])
      onComplete(player)
    } catch (err: unknown) {
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
  const checkPortrait = () => typeof window !== 'undefined'
    ? window.innerHeight > window.innerWidth : false
  const [isMobile, setIsMobile] = useState(checkMobile)
  const [isPortrait, setIsPortrait] = useState(checkPortrait)
  useEffect(() => {
    const handler = () => { setIsMobile(checkMobile()); setIsPortrait(checkPortrait()) }
    window.addEventListener('resize', handler)
    return () => window.removeEventListener('resize', handler)
  }, [])

  // モバイル縦持ち → 「横向きにしてください」表示
  if (isMobile && isPortrait) {
    return (
      <div style={{ position: 'fixed', inset: 0, zIndex: 1000, background: '#d4d0c8',
        display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
        fontFamily: '"MS UI Gothic", Meiryo, sans-serif', gap: 16 }}>
        <div style={{ fontSize: 48 }}>↻</div>
        <div style={{ fontSize: 16, fontWeight: 700 }}>端末を横向きにしてください</div>
        <div style={{ fontSize: 13, color: '#555' }}>Please rotate your device</div>
      </div>
    )
  }

  // ── 共通スタイル ──────────────────────────────────────────────────
  // モバイル: 自然高さ・オーバーレイ同色で全画面に見せる。デスクトップ: ゲームサイズ固定モーダル。
  const panelBase: React.CSSProperties = isMobile ? {
    display: 'flex', flexDirection: 'column',
    width: '100vw',
    boxSizing: 'border-box',
    padding: '10px 12px',
    color: '#111', background: '#d4d0c8',
    border: 'none',
    fontFamily: '"MS UI Gothic", Meiryo, sans-serif',
  } : {
    display: 'flex', flexDirection: 'column',
    maxHeight: '88dvh',
    overflowY: 'auto',
    boxSizing: 'border-box',
    padding: '28px 36px',
    color: '#111', background: '#d4d0c8',
    border: '2px outset #f5f5f5',
    fontFamily: '"MS UI Gothic", Meiryo, sans-serif',
  }
  // モバイル: 利用規約は全画面固定(テキストスクロール用)、他は自然高さ
  const panelStyle: React.CSSProperties = isMobile
    ? { ...panelBase, height: '100dvh', overflowY: 'hidden' }
    : { ...panelBase, width: 'min(1019px, calc(100vw - 32px))', height: 'min(735px, 95dvh)', maxHeight: 'none' }
  const widePanelStyle: React.CSSProperties = isMobile
    ? panelBase
    : { ...panelBase, width: 'min(1019px, calc(100vw - 32px))', height: 'min(735px, 95dvh)', maxHeight: 'none' }

  const overlayStyle: React.CSSProperties = isMobile ? {
    position: 'fixed', inset: 0, zIndex: 1000,
    background: '#d4d0c8',
  } : {
    position: 'fixed', inset: 0, zIndex: 1000,
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    background: 'rgba(0, 0, 0, 0.72)',
  }
  const stepIndicator = (label: string, active: boolean, done: boolean) => (
    <span style={{
      padding: '2px 12px', borderRadius: 10, fontSize: isMobile ? 11 : 13,
      background: done ? '#4caf50' : active ? '#1769aa' : '#aaa',
      color: '#fff', marginRight: 6,
    }}>{label}</span>
  )

  // ── STEP 1: 利用規約 ─────────────────────────────────────────────
  if (step === 'terms') {
    return (
      <div style={overlayStyle}>
        <div style={panelStyle}>
          <div style={{ flexShrink: 0, marginBottom: isMobile ? 8 : 12 }}>
            {stepIndicator('1. 利用規約', true, false)}
            {stepIndicator('2. ニックネーム', false, false)}
            {stepIndicator('3. プロフィール', false, false)}
          </div>
          <div style={{ flexShrink: 0, marginBottom: isMobile ? 8 : 12, fontSize: isMobile ? 15 : 20, fontWeight: 700 }}>利用規約</div>

          {/* モバイル/デスクトップ共通: flex:1 で残り全高さ (panelStyle を固定高にしてあるので機能する) */}
          <div style={isMobile ? {
            flex: 1, minHeight: 0,
            overflowY: 'auto', padding: '8px 10px',
            background: '#fff', border: '1px inset #aaa',
            fontSize: 12, lineHeight: 1.85, color: '#222',
          } : {
            flex: 1, minHeight: 0,
            overflowY: 'auto', padding: '14px 18px',
            background: '#fff', border: '1px inset #aaa',
            fontSize: 14, lineHeight: 1.95, color: '#222',
          }}>
            <b>麻雀4 利用規約</b><br /><br />

            第1条（目的）<br />
            本規約は、麻雀4（以下「本サービス」）の利用に関する条件を定めるものです。
            ユーザーは本規約に同意した上で本サービスを利用するものとします。<br /><br />

            第2条（アカウント）<br />
            ユーザーは Google アカウントを利用して本サービスに登録します。
            アカウント情報の管理はユーザー自身の責任において行うものとします。
            第三者へのアカウントの譲渡・共有は禁止します。<br /><br />

            第3条（禁止事項）<br />
            ユーザーは以下の行為を行ってはなりません。<br />
            ① 他のユーザーへの嫌がらせ、誹謗中傷<br />
            ② チートツール・不正プログラムの使用<br />
            ③ サービス運営を妨害する行為<br />
            ④ 法令または公序良俗に反する行為<br />
            ⑤ 商業目的での無断利用<br /><br />

            第4条（サービスの変更・中断）<br />
            運営者は事前通知なくサービス内容の変更・停止・終了を行う場合があります。
            これによって生じた損害について、運営者は責任を負いません。<br /><br />

            第5条（個人情報の取扱い）<br />
            本サービスは Google 認証によって取得したメールアドレスおよびユーザー識別子を
            アカウント管理目的でのみ使用し、第三者への提供は行いません。<br /><br />

            第6条（免責事項）<br />
            本サービスはゲームの継続的な提供を保証するものではありません。
            サービス利用に伴う損害について運営者は一切の責任を負いません。<br /><br />

            第7条（規約の変更）<br />
            運営者は必要に応じて本規約を変更できるものとします。
            変更後も本サービスの利用を継続した場合、変更に同意したものとみなします。
          </div>

          <label style={{ flexShrink: 0, display: 'flex', alignItems: 'center', gap: 8, margin: `${isMobile ? 10 : 16}px 0 ${isMobile ? 6 : 8}px`, fontSize: isMobile ? 12 : 14 }}>
            <input type="checkbox" style={{ width: 16, height: 16, cursor: 'pointer', flexShrink: 0 }}
              checked={termsChecked} onChange={e => setTermsChecked(e.target.checked)} />
            上記の利用規約を読み、同意します
          </label>

          <div style={{ flexShrink: 0, textAlign: 'right' }}>
            <button type="button" disabled={!termsChecked} onClick={() => setStep('nickname')}
              style={{
                minWidth: isMobile ? 100 : 140, minHeight: isMobile ? 30 : 38,
                fontSize: isMobile ? 13 : 15, padding: '0 20px',
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
      <div style={overlayStyle}>
        <div style={widePanelStyle}>
          <div style={{ flexShrink: 0, marginBottom: isMobile ? 10 : 16 }}>
            {stepIndicator('1. 利用規約', false, true)}
            {stepIndicator('2. ニックネーム', true, false)}
            {stepIndicator('3. プロフィール', false, false)}
          </div>
          <div style={{ flexShrink: 0, marginBottom: isMobile ? 12 : 18, fontSize: isMobile ? 15 : 20, fontWeight: 700 }}>ニックネームの設定</div>

          <div style={{ flexShrink: 0, marginBottom: isMobile ? 6 : 10, fontSize: isMobile ? 12 : 14 }}>
            ゲーム内で表示されるニックネームを入力してください（4〜16文字）
          </div>
          <input type="text" value={nickname} onChange={e => setNickname(e.target.value)}
            maxLength={16} placeholder="ニックネーム (4〜16文字)"
            style={{
              flexShrink: 0, width: '100%', boxSizing: 'border-box',
              padding: isMobile ? '7px 10px' : '10px 14px',
              fontSize: isMobile ? 14 : 17,
              border: nicknameMsg
                ? `2px solid ${nicknameMsg.ok ? '#4caf50' : '#d32f2f'}`
                : '1px inset #aaa',
            }}
          />
          <div style={{ flexShrink: 0, minHeight: 20, marginTop: 5, fontSize: isMobile ? 11 : 13,
            color: nicknameMsg?.ok ? '#2e7d32' : '#d32f2f' }}>
            {checking ? '確認中...' : (nicknameMsg?.text ?? '')}
          </div>

          <div style={{ flexShrink: 0, display: 'flex', justifyContent: 'space-between', marginTop: 'auto', paddingTop: isMobile ? 12 : 24 }}>
            <button type="button" onClick={() => setStep('terms')}
              style={{ minWidth: isMobile ? 80 : 110, minHeight: isMobile ? 30 : 38, fontSize: isMobile ? 12 : 14, padding: '0 14px' }}>
              ← 戻る
            </button>
            <button type="button" disabled={!canProceedNickname} onClick={() => setStep('avatar')}
              style={{
                minWidth: isMobile ? 100 : 140, minHeight: isMobile ? 30 : 38,
                fontSize: isMobile ? 13 : 15, padding: '0 20px',
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
    <div style={overlayStyle}>
      <div style={widePanelStyle}>
        <div style={{ flexShrink: 0, marginBottom: isMobile ? 8 : 14 }}>
          {stepIndicator('1. 利用規約', false, true)}
          {stepIndicator('2. ニックネーム', false, true)}
          {stepIndicator('3. プロフィール', true, false)}
        </div>

        {/* タイトル: モバイルはタイトル+性別を1行 / デスクトップはタイトル単独 */}
        {isMobile ? (
          <div style={{ flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
            <div style={{ fontSize: 14, fontWeight: 700 }}>プロフィール設定</div>
            <div style={{ display: 'flex', gap: 14, fontSize: 12 }}>
              <label style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 5 }}>
                <input type="radio" checked={sex === 'M'} onChange={() => selectSex('M')} /> 男性
              </label>
              <label style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 5 }}>
                <input type="radio" checked={sex === 'F'} onChange={() => selectSex('F')} /> 女性
              </label>
            </div>
          </div>
        ) : (
          <div style={{ flexShrink: 0, marginBottom: 14, fontSize: 20, fontWeight: 700 }}>プロフィール設定</div>
        )}

        <div style={{ flexShrink: 0, marginBottom: isMobile ? 6 : 10, fontSize: isMobile ? 11 : 14, color: '#444' }}>
          ニックネーム: <b>{nickname}</b>
        </div>

        {/* デスクトップのみ: ニックネームの下に性別選択 */}
        {!isMobile && (
          <div style={{ flexShrink: 0, marginBottom: 14, fontSize: 15 }}>
            <label style={{ cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: 6, marginRight: 28 }}>
              <input type="radio" checked={sex === 'M'} onChange={() => selectSex('M')} /> 男性
            </label>
            <label style={{ cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <input type="radio" checked={sex === 'F'} onChange={() => selectSex('F')} /> 女性
            </label>
          </div>
        )}

        {/* アバターグリッド
            モバイル: 自然高さ・正方形セル / デスクトップ: 3/4 縦長セル */}
        <div style={isMobile ? {
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(clamp(66px, 15vw, 84px), 1fr))',
          gap: 'clamp(5px, 1.2vw, 8px)',
          border: '2px groove #fff',
          padding: '6px 8px',
          boxSizing: 'border-box',
          marginBottom: 8,
        } : {
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(96px, 1fr))',
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
              style={{
                width: '100%',
                aspectRatio: isMobile ? '1' : '3/4',
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
          <div style={{ flexShrink: 0, marginTop: 6, color: '#b00020', fontSize: isMobile ? 12 : 14 }}>{error}</div>
        )}

        <div style={{ flexShrink: 0, display: 'flex', justifyContent: 'space-between', marginTop: isMobile ? 8 : 0 }}>
          <button type="button" onClick={() => setStep('nickname')} disabled={submitting}
            style={{ minWidth: isMobile ? 80 : 110, minHeight: isMobile ? 30 : 38, fontSize: isMobile ? 12 : 14, padding: '0 14px' }}>
            ← 戻る
          </button>
          <button type="button" disabled={submitting} onClick={() => void submit()}
            style={{
              minWidth: isMobile ? 100 : 140, minHeight: isMobile ? 30 : 38,
              fontSize: isMobile ? 13 : 15, padding: '0 20px',
              background: '#1769aa', color: '#fff', border: 'none',
              borderRadius: 3, cursor: 'pointer',
            }}>
            {submitting ? '登録中...' : '登録する'}
          </button>
        </div>
      </div>
    </div>
  )
}
