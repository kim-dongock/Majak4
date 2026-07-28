/**
 * CMJMemberInfoDialog tournament registration mode.
 * Legacy: MajakFrame.cpp InitDialogMeeting / RequestCheckTournamentRegistContents.
 */
import { useState } from 'react'
import { showMessage } from '../../../utils/msgbox'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const IMG = '/assets/images/game'
const FONT = "'MS UI Gothic', 'MS PGothic', 'MS Gothic', sans-serif"

export interface TournamentRegistPayload {
  roomOption: string
  tournamentBaseRule: string
  tournamentMoneyRule: string
  tournamentName: string
  tournamentDate: string
  password: string
  maxViewer: number
  tournamentRegistFlag: number
}

interface Props {
  onOK: (payload: TournamentRegistPayload) => void
  onCancel: () => void
}

function frameButtonStyle(src: string, frameW: number, frameH: number, frame: number, left: number, top: number): React.CSSProperties {
  return {
    position: 'absolute',
    left,
    top,
    width: frameW,
    height: frameH,
    backgroundImage: `url(${src})`,
    backgroundPosition: `${-frame * frameW}px 0`,
    backgroundRepeat: 'no-repeat',
    backgroundColor: 'transparent',
    border: 'none',
    padding: 0,
    cursor: 'pointer',
    imageRendering: 'pixelated',
  }
}

function pad2(value: number) {
  return String(value).padStart(2, '0')
}

function defaultStartDateTime() {
  const date = new Date(Date.now() + 2 * 60 * 60 * 1000)
  date.setMinutes(Math.ceil(date.getMinutes() / 10) * 10, 0, 0)
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}T${pad2(date.getHours())}:${pad2(date.getMinutes())}`
}

function formatTournamentDate(value: string) {
  const [date, time] = value.split('T')
  return `${date.replace(/-/g, '/')} ${time || '00:00'}:00`
}

function legacyNameByteLength(value: string) {
  return Array.from(value).reduce((length, character) =>
    length + (/^[\x00-\x7f\uff61-\uff9f]$/.test(character) ? 1 : 2), 0)
}

function inputStyle(left: number, top: number, width: number): React.CSSProperties {
  return {
    position: 'absolute',
    left,
    top,
    width,
    height: 22,
    boxSizing: 'border-box',
    fontFamily: FONT,
    fontSize: 13,
    color: '#000',
    background: '#fff',
    border: '1px solid #6f8f68',
    padding: '1px 4px',
    outline: 'none',
  }
}

function selectStyle(left: number, top: number, width: number): React.CSSProperties {
  return {
    ...inputStyle(left, top, width),
    padding: '0 2px',
  }
}

function labelStyle(left: number, top: number, width = 120): React.CSSProperties {
  return {
    position: 'absolute',
    left,
    top,
    width,
    height: 18,
    fontFamily: FONT,
    fontSize: 13,
    lineHeight: '18px',
    color: '#1a401a',
    whiteSpace: 'nowrap',
  }
}

function checkStyle(left: number, top: number, width = 110): React.CSSProperties {
  return {
    position: 'absolute',
    left,
    top,
    width,
    display: 'flex',
    alignItems: 'center',
    gap: 4,
    fontFamily: FONT,
    fontSize: 13,
    color: '#1a401a',
    whiteSpace: 'nowrap',
  }
}

export default function TournamentRegistDlg({ onOK, onCancel }: Props) {
  const layoutMode = useOutgameLayoutMode()
  const [name, setName] = useState('')
  const [dateTime, setDateTime] = useState(defaultStartDateTime)
  const [hanTon, setHanTon] = useState(0)
  const [matchTime, setMatchTime] = useState(0)
  const [matchCount, setMatchCount] = useState(0)
  const [matchFormat, setMatchFormat] = useState(0)
  const [joinMoney, setJoinMoney] = useState('0')
  const [prize1, setPrize1] = useState('0')
  const [prize2, setPrize2] = useState('0')
  const [prize3, setPrize3] = useState('0')
  const [prize4, setPrize4] = useState('0')
  const [wareme, setWareme] = useState(false)
  const [kuitan, setKuitan] = useState(true)
  const [uma, setUma] = useState(0)
  const [agari, setAgari] = useState(0)
  const [red, setRed] = useState(true)
  const [speedFast, setSpeedFast] = useState(false)
  const [watcher, setWatcher] = useState(true)
  const [openHand, setOpenHand] = useState(false)
  const [viewerChat, setViewerChat] = useState(true)
  const [usePassword, setUsePassword] = useState(false)
  const [password, setPassword] = useState('')
  const [okFrame, setOkFrame] = useState(0)
  const [cancelFrame, setCancelFrame] = useState(0)

  const submit = () => {
    const trimmedName = name.trim()
    const errors: string[] = []
    const nameByteLength = legacyNameByteLength(trimmedName)
    if (nameByteLength < 8 || nameByteLength > 30) {
      errors.push('大会名は半角8～30文字（全角4～15文字）で入力してください。')
    } else if (/[\uff61-\uff9f]/.test(trimmedName)) {
      errors.push('大会名に半角カナは使用できません。')
    }

    const selectedDateTime = new Date(dateTime)
    const now = Date.now()
    if (!dateTime || Number.isNaN(selectedDateTime.getTime())) {
      errors.push('開催日時を入力してください。')
    } else if (selectedDateTime.getTime() < now + 70 * 60 * 1000) {
      errors.push('開催日時は現在から1時間10分以降に設定してください。')
    } else if (selectedDateTime.getTime() > now + 8 * 24 * 60 * 60 * 1000) {
      errors.push('開催日時は8日以内に設定してください。')
    }

    if (hanTon === 0 || matchTime === 0 || matchCount === 0 || matchFormat === 0) {
      errors.push('大会ルールをすべて選択してください。')
    }
    if ((Number(joinMoney) || 0) > 10_000) {
      errors.push('参加費は0～10,000コインで入力してください。')
    }
    if ([prize1, prize2, prize3, prize4].some(value => (Number(value) || 0) > 100_000)) {
      errors.push('各順位の賞金は0～100,000コインで入力してください。')
    }
    if (usePassword && password.trim().length === 0) {
      errors.push('パスワードを入力してください。')
    }
    if (errors.length > 0) {
      showMessage(errors.join('\n'), '入力内容を確認してください')
      return
    }

    const format = [
      { maxPlayers: 4, playMode: 1 },
      { maxPlayers: 16, playMode: 1 },
      { maxPlayers: 64, playMode: 1 },
      { maxPlayers: 8, playMode: 2 },
      { maxPlayers: 16, playMode: 2 },
      { maxPlayers: 32, playMode: 2 },
    ][matchFormat - 1]
    const roomOption = `${hanTon - 1}${uma}${speedFast ? 1 : 0}${kuitan ? 0 : 1}0${red ? 2 : 1}${openHand ? 1 : 0}${viewerChat ? 1 : 0}00${wareme ? 1 : 0}0${agari}0${viewerChat ? 1 : 0}`

    onOK({
      roomOption,
      tournamentBaseRule: `${format.maxPlayers}|${format.playMode}|${matchCount}|${matchTime}`,
      tournamentMoneyRule: `${Number(joinMoney) || 0}|${Number(prize1) || 0}|${Number(prize2) || 0}|${Number(prize3) || 0}|${Number(prize4) || 0}`,
      tournamentName: trimmedName,
      tournamentDate: formatTournamentDate(dateTime),
      password: usePassword ? password.trim() : '',
      maxViewer: watcher ? 12 : 0,
      tournamentRegistFlag: 1,
    })
  }

  if (layoutMode === 'mobileLandscape') {
    return (
      <div className="majak-mobile-tournament-regist-overlay">
        <form
          className="majak-mobile-tournament-regist"
          onSubmit={event => { event.preventDefault(); submit() }}
        >
          <header>大会登録</header>
          <div className="majak-mobile-tournament-regist__body">
            <fieldset>
              <legend>大会設定</legend>
              <label className="majak-mobile-tournament-regist__wide"><span>大会名</span><input value={name} onChange={event => setName(event.target.value.slice(0, 30))} maxLength={30} autoFocus /></label>
              <label className="majak-mobile-tournament-regist__wide"><span>開催日時</span><input type="datetime-local" value={dateTime} onChange={event => setDateTime(event.target.value)} /></label>
              <label><span>東南/東風</span><select value={hanTon} onChange={event => setHanTon(Number(event.target.value))}><option value={0}>選択</option><option value={1}>東南戦</option><option value={2}>東風戦</option></select></label>
              <label><span>1試合の時間</span><select value={matchTime} onChange={event => setMatchTime(Number(event.target.value))}><option value={0}>選択</option><option value={1}>30分</option><option value={2}>40分</option><option value={3}>50分</option><option value={4}>60分</option></select></label>
              <label><span>試合数</span><select value={matchCount} onChange={event => setMatchCount(Number(event.target.value))}><option value={0}>選択</option><option value={1}>1半荘</option><option value={2}>2半荘</option></select></label>
              <label><span>大会形式</span><select value={matchFormat} onChange={event => setMatchFormat(Number(event.target.value))}><option value={0}>選択</option><option value={1}>4人/1人勝抜</option><option value={2}>16人/1人勝抜</option><option value={3}>64人/1人勝抜</option><option value={4}>8人/2人勝抜</option><option value={5}>16人/2人勝抜</option><option value={6}>32人/2人勝抜</option></select></label>
            </fieldset>

            <fieldset>
              <legend>参加費・賞金</legend>
              <label className="majak-mobile-tournament-regist__wide"><span>参加費</span><input inputMode="numeric" value={joinMoney} onChange={event => setJoinMoney(event.target.value.replace(/\D/g, '').slice(0, 5))} /></label>
              <label><span>賞金 1位</span><input inputMode="numeric" value={prize1} onChange={event => setPrize1(event.target.value.replace(/\D/g, '').slice(0, 6))} /></label>
              <label><span>賞金 2位</span><input inputMode="numeric" value={prize2} onChange={event => setPrize2(event.target.value.replace(/\D/g, '').slice(0, 6))} /></label>
              <label><span>賞金 3位</span><input inputMode="numeric" value={prize3} onChange={event => setPrize3(event.target.value.replace(/\D/g, '').slice(0, 6))} /></label>
              <label><span>賞金 4位</span><input inputMode="numeric" value={prize4} onChange={event => setPrize4(event.target.value.replace(/\D/g, '').slice(0, 6))} /></label>
              <label><span>ウマ</span><select value={uma} onChange={event => setUma(Number(event.target.value))}><option value={0}>5-10</option><option value={1}>10-20</option><option value={2}>10-30</option></select></label>
              <label><span>アガリ</span><select value={agari} onChange={event => setAgari(Number(event.target.value))}><option value={0}>頭ハネ</option><option value={1}>ダブロン</option><option value={2}>トリロン</option></select></label>
            </fieldset>

            <fieldset className="majak-mobile-tournament-regist__rules">
              <legend>ルール設定</legend>
              <div className="majak-mobile-tournament-regist__checks">
                <label><input type="checkbox" checked={wareme} onChange={event => setWareme(event.target.checked)} />ワレメあり</label>
                <label><input type="checkbox" checked={kuitan} onChange={event => setKuitan(event.target.checked)} />クイタンあり</label>
                <label><input type="checkbox" checked={red} onChange={event => setRed(event.target.checked)} />赤牌1枚</label>
                <label><input type="checkbox" checked={speedFast} onChange={event => setSpeedFast(event.target.checked)} />高速</label>
                <label><input type="checkbox" checked={watcher} onChange={event => setWatcher(event.target.checked)} />観戦可</label>
                <label><input type="checkbox" checked={openHand} onChange={event => setOpenHand(event.target.checked)} />手牌公開</label>
                <label className="majak-mobile-tournament-regist__wide"><input type="checkbox" checked={viewerChat} onChange={event => setViewerChat(event.target.checked)} />観戦者チャット可</label>
              </div>
              <div className="majak-mobile-tournament-regist__password">
                <label><input type="checkbox" checked={usePassword} onChange={event => setUsePassword(event.target.checked)} />カギをかける</label>
                <label><span>パスワード</span><input type="password" value={password} disabled={!usePassword} maxLength={8} onChange={event => setPassword(event.target.value.slice(0, 8))} /></label>
              </div>
            </fieldset>
          </div>
          <footer>
            <button type="submit">大会登録</button>
            <button type="button" onClick={onCancel}>キャンセル</button>
          </footer>
        </form>
      </div>
    )
  }

  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 280, background: 'rgba(0,0,0,0.35)', overflowY: 'auto' }}>
      <div style={{ position: 'relative', width: 500, height: 830, margin: '4px auto 16px' }}>
        <img src={`${IMG}/mj_tournament_bg.png`} alt="" draggable={false} style={{ position: 'absolute', left: 0, top: 0, width: 500, height: 830, imageRendering: 'pixelated' }} />
        <div style={{ position: 'absolute', left: 155, top: 14, width: 190, height: 24, textAlign: 'center', fontFamily: FONT, fontSize: 15, fontWeight: 'bold', color: '#fff', lineHeight: '24px' }}>大会登録</div>

        <span style={labelStyle(45, 66)}>大会名</span>
        <input value={name} onChange={event => setName(event.target.value.slice(0, 30))} maxLength={30} autoFocus style={inputStyle(150, 64, 265)} />
        <span style={labelStyle(45, 100)}>開催日時</span>
        <input type="datetime-local" value={dateTime} onChange={event => setDateTime(event.target.value)} style={inputStyle(150, 98, 190)} />

        <span style={labelStyle(45, 138)}>東南/東風</span>
        <select value={hanTon} onChange={event => setHanTon(Number(event.target.value))} style={selectStyle(150, 136, 190)}>
          <option value={0}>選択してください</option>
          <option value={1}>東南戦</option>
          <option value={2}>東風戦</option>
        </select>
        <span style={labelStyle(45, 172)}>1試合の時間</span>
        <select value={matchTime} onChange={event => setMatchTime(Number(event.target.value))} style={selectStyle(150, 170, 190)}>
          <option value={0}>選択してください</option>
          <option value={1}>30分</option>
          <option value={2}>40分</option>
          <option value={3}>50分</option>
          <option value={4}>60分</option>
        </select>
        <span style={labelStyle(45, 206)}>試合数</span>
        <select value={matchCount} onChange={event => setMatchCount(Number(event.target.value))} style={selectStyle(150, 204, 190)}>
          <option value={0}>選択してください</option>
          <option value={1}>1半荘</option>
          <option value={2}>2半荘</option>
        </select>
        <span style={labelStyle(45, 240)}>大会形式</span>
        <select value={matchFormat} onChange={event => setMatchFormat(Number(event.target.value))} style={selectStyle(150, 238, 240)}>
          <option value={0}>選択してください</option>
          <option value={1}>4人/1人勝ち抜け</option>
          <option value={2}>16人/1人勝ち抜け</option>
          <option value={3}>64人/1人勝ち抜け</option>
          <option value={4}>8人/2人勝ち抜け</option>
          <option value={5}>16人/2人勝ち抜け</option>
          <option value={6}>32人/2人勝ち抜け</option>
        </select>

        <span style={labelStyle(45, 284)}>参加費</span>
        <input value={joinMoney} onChange={event => setJoinMoney(event.target.value.replace(/\D/g, '').slice(0, 5))} style={inputStyle(150, 282, 90)} />
        <span style={labelStyle(260, 284, 80)}>ハンコイン</span>
        <span style={labelStyle(45, 318)}>賞金 1位</span>
        <input value={prize1} onChange={event => setPrize1(event.target.value.replace(/\D/g, '').slice(0, 6))} style={inputStyle(150, 316, 90)} />
        <span style={labelStyle(260, 318, 80)}>2位</span>
        <input value={prize2} onChange={event => setPrize2(event.target.value.replace(/\D/g, '').slice(0, 6))} style={inputStyle(300, 316, 90)} />
        <span style={labelStyle(45, 352)}>賞金 3位</span>
        <input value={prize3} onChange={event => setPrize3(event.target.value.replace(/\D/g, '').slice(0, 6))} style={inputStyle(150, 350, 90)} />
        <span style={labelStyle(260, 352, 80)}>4位</span>
        <input value={prize4} onChange={event => setPrize4(event.target.value.replace(/\D/g, '').slice(0, 6))} style={inputStyle(300, 350, 90)} />

        <span style={labelStyle(45, 400)}>ルール設定</span>
        <label style={checkStyle(60, 430)}><input type="checkbox" checked={wareme} onChange={event => setWareme(event.target.checked)} />ワレメあり</label>
        <label style={checkStyle(185, 430)}><input type="checkbox" checked={kuitan} onChange={event => setKuitan(event.target.checked)} />クイタンあり</label>
        <label style={checkStyle(315, 430)}><input type="checkbox" checked={red} onChange={event => setRed(event.target.checked)} />赤牌1枚</label>
        <span style={labelStyle(60, 466, 45)}>ウマ</span>
        <select value={uma} onChange={event => setUma(Number(event.target.value))} style={selectStyle(110, 464, 110)}>
          <option value={0}>5-10</option>
          <option value={1}>10-20</option>
          <option value={2}>10-30</option>
        </select>
        <span style={labelStyle(245, 466, 55)}>アガリ</span>
        <select value={agari} onChange={event => setAgari(Number(event.target.value))} style={selectStyle(305, 464, 110)}>
          <option value={0}>頭ハネ</option>
          <option value={1}>ダブロン</option>
          <option value={2}>トリロン</option>
        </select>
        <label style={checkStyle(60, 502)}><input type="checkbox" checked={speedFast} onChange={event => setSpeedFast(event.target.checked)} />高速</label>
        <label style={checkStyle(185, 502)}><input type="checkbox" checked={watcher} onChange={event => setWatcher(event.target.checked)} />観戦可</label>
        <label style={checkStyle(315, 502)}><input type="checkbox" checked={openHand} onChange={event => setOpenHand(event.target.checked)} />手牌公開</label>
        <label style={checkStyle(60, 538, 170)}><input type="checkbox" checked={viewerChat} onChange={event => setViewerChat(event.target.checked)} />観戦者チャット可</label>

        <label style={checkStyle(45, 594, 120)}><input type="checkbox" checked={usePassword} onChange={event => setUsePassword(event.target.checked)} />カギをかける</label>
        <span style={labelStyle(165, 594, 80)}>パスワード</span>
        <input type="password" value={password} disabled={!usePassword} maxLength={8} onChange={event => setPassword(event.target.value.slice(0, 8))} style={{ ...inputStyle(250, 592, 120), opacity: usePassword ? 1 : 0.55 }} />

        <button
          type="button"
          onClick={submit}
          onMouseEnter={() => setOkFrame(2)}
          onMouseLeave={() => setOkFrame(0)}
          onMouseDown={() => setOkFrame(3)}
          onMouseUp={() => setOkFrame(2)}
          style={frameButtonStyle(`${IMG}/mj_btn_tournament_touroku.png`, 85, 29, okFrame, 135, 765)}
        />
        <button
          type="button"
          onClick={onCancel}
          onMouseEnter={() => setCancelFrame(2)}
          onMouseLeave={() => setCancelFrame(0)}
          onMouseDown={() => setCancelFrame(3)}
          onMouseUp={() => setCancelFrame(2)}
          style={frameButtonStyle(`${IMG}/mj_btn_tournament_cancel.png`, 85, 29, cancelFrame, 270, 765)}
        />
      </div>
    </div>
  )
}